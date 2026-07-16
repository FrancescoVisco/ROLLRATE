using System.Collections.Generic;
using UnityEngine;
using Rollrate.Core;
using Rollrate.Data;
using Rollrate.Shop;

namespace Rollrate.Archive
{
    /// <summary>
    /// Logic for the Archive Node: one random Test (Resonance/Tribute/
    /// Ambition, per design doc) is drawn per visit - the player doesn't
    /// choose which. Resolution is two explicit steps, mirroring Combat's
    /// Roll/Resolve rhythm instead of doing everything on one click:
    ///
    /// 1. RollDice() - rolls whatever this Test needs (Core alone, entire
    ///    Pool, or Core + best Pool die) and stores the raw values, WITHOUT
    ///    yet deciding success/failure or applying any effect.
    /// 2. ResolveTest() - using the already-rolled values, decides success/
    ///    failure and applies the Scrap/die/HP effect.
    /// </summary>
    public class ArchiveController : MonoBehaviour
    {
        [SerializeField] private ArchiveTestTable testTable;
        [SerializeField] private ShopOfferPools offerPools; // reused only for Ambition's "die one Grade above current" reward

        public ArchiveTestType CurrentTest { get; private set; }
        public bool HasRolled { get; private set; }
        public bool HasResolved { get; private set; }
        public bool LastResultWasSuccess { get; private set; }
        public string LastRollSummary { get; private set; } = "";
        public string LastResultSummary { get; private set; } = "";

        // Stored roll results, filled by RollDice(), read by ResolveTest().
        private int _coreRoll;
        private int _poolSum;
        private int _poolBest;
        private DieData _tributeTargetDie; // the random owned die at stake for Tribute

        private void Awake()
        {
            CurrentTest = (ArchiveTestType)Random.Range(0, 3);
        }

        public string GetTestDescription()
        {
            var state = RunManager.Instance.State;
            int grade = state.currentEchelon;

            switch (CurrentTest)
            {
                case ArchiveTestType.Resonance:
                    return $"Test di Risonanza\nLancia il Dado Core: devi ottenere {testTable.GetResonanceThreshold(grade)}+.\n" +
                           $"Successo: +{testTable.GetResonanceReward(grade)} Scrap. Fallimento: -{testTable.GetResonancePenalty(grade)} Scrap.";
                case ArchiveTestType.Tribute:
                    return $"Test di Tributo\nLancia l'intero Pool: la somma deve essere {testTable.GetTributeThreshold(grade)}+.\n" +
                           "Successo: evolvi un dado casuale posseduto. Fallimento: perdi permanentemente un dado casuale.";
                case ArchiveTestType.Ambition:
                    return $"Test di Ambizione\nLancia il Dado Core + il miglior dado del Pool: la somma deve essere {testTable.GetAmbitionThreshold(grade)}+.\n" +
                           $"Successo: +{testTable.GetAmbitionRewardScrap(grade)} Scrap oppure un dado di Grado superiore. Fallimento: perdi il 20% dei PV massimi.";
                default:
                    return "";
            }
        }

        /// <summary>Step 1: rolls the dice this Test needs and stores the raw values. Doesn't apply any effect yet.</summary>
        public void RollDice()
        {
            if (HasRolled) return;
            HasRolled = true;

            var state = RunManager.Instance.State;

            switch (CurrentTest)
            {
                case ArchiveTestType.Resonance:
                    _coreRoll = Random.Range(1, state.coreDie.faces + 1);
                    LastRollSummary = $"Core: {_coreRoll}";
                    break;

                case ArchiveTestType.Tribute:
                    var rolls = new List<int>();
                    _poolSum = 0;
                    foreach (DieData die in state.dicePool)
                    {
                        int roll = Random.Range(1, die.faces + 1);
                        rolls.Add(roll);
                        _poolSum += roll;
                    }
                    _tributeTargetDie = state.dicePool.Count > 0 ? state.dicePool[Random.Range(0, state.dicePool.Count)] : null;
                    LastRollSummary = rolls.Count > 0 ? $"Pool: {string.Join(" + ", rolls)} = {_poolSum}" : "Il Pool è vuoto.";
                    break;

                case ArchiveTestType.Ambition:
                    _coreRoll = Random.Range(1, state.coreDie.faces + 1);
                    _poolBest = 0;
                    foreach (DieData die in state.dicePool)
                    {
                        int roll = Random.Range(1, die.faces + 1);
                        if (roll > _poolBest) _poolBest = roll;
                    }
                    LastRollSummary = $"Core: {_coreRoll} | Miglior dado Pool: {_poolBest} | Totale: {_coreRoll + _poolBest}";
                    break;
            }
        }

        /// <summary>Step 2: uses the already-rolled values to decide success/failure and apply the effect. Requires RollDice() first.</summary>
        public void ResolveTest()
        {
            if (!HasRolled || HasResolved) return;
            HasResolved = true;

            var state = RunManager.Instance.State;
            int grade = state.currentEchelon;

            switch (CurrentTest)
            {
                case ArchiveTestType.Resonance: ResolveResonance(state, grade); break;
                case ArchiveTestType.Tribute: ResolveTribute(state, grade); break;
                case ArchiveTestType.Ambition: ResolveAmbition(state, grade); break;
            }
        }

        private void ResolveResonance(GameState state, int grade)
        {
            int threshold = testTable.GetResonanceThreshold(grade);
            LastResultWasSuccess = _coreRoll >= threshold;

            if (LastResultWasSuccess)
            {
                int reward = testTable.GetResonanceReward(grade);
                state.scrap += reward;
                LastResultSummary = $"Serviva {threshold}+. Successo! +{reward} Scrap.";
            }
            else
            {
                int penalty = testTable.GetResonancePenalty(grade);
                state.scrap = Mathf.Max(0, state.scrap - penalty);
                LastResultSummary = $"Serviva {threshold}+. Fallito. -{penalty} Scrap.";
            }
        }

        private void ResolveTribute(GameState state, int grade)
        {
            if (_tributeTargetDie == null)
            {
                LastResultSummary = "Non hai dadi nel Pool.";
                LastResultWasSuccess = false;
                return;
            }

            int threshold = testTable.GetTributeThreshold(grade);
            LastResultWasSuccess = _poolSum >= threshold;

            if (LastResultWasSuccess)
            {
                if (_tributeTargetDie.nextTier != null)
                {
                    state.ReplaceDieEverywhere(_tributeTargetDie, _tributeTargetDie.nextTier);
                    LastResultSummary = $"Serviva {threshold}+. Successo! {_tributeTargetDie.displayName} evoluto in {_tributeTargetDie.nextTier.displayName}.";
                }
                else
                {
                    LastResultSummary = $"Serviva {threshold}+. Successo! Ma {_tributeTargetDie.displayName} è già al Grado massimo - nessun effetto.";
                }
            }
            else
            {
                state.RemoveDiePermanently(_tributeTargetDie);
                LastResultSummary = $"Serviva {threshold}+. Fallito. {_tributeTargetDie.displayName} perso permanentemente.";
            }
        }

        private void ResolveAmbition(GameState state, int grade)
        {
            int total = _coreRoll + _poolBest;
            int threshold = testTable.GetAmbitionThreshold(grade);
            LastResultWasSuccess = total >= threshold;

            if (LastResultWasSuccess)
            {
                if (Random.value < 0.5f || grade >= 5 || offerPools == null)
                {
                    int reward = testTable.GetAmbitionRewardScrap(grade);
                    state.scrap += reward;
                    LastResultSummary = $"Serviva {threshold}+. Successo! +{reward} Scrap.";
                }
                else
                {
                    GradeOfferPool higherGradePool = offerPools.GetForGradeWithUnlocks(grade + 1);
                    if (higherGradePool.dice.Length > 0)
                    {
                        DieData bonusDie = higherGradePool.dice[Random.Range(0, higherGradePool.dice.Length)];
                        state.AddDieToPool(bonusDie);
                        LastResultSummary = $"Serviva {threshold}+. Successo! Ottieni un {bonusDie.displayName} (Grado superiore).";
                    }
                    else
                    {
                        int reward = testTable.GetAmbitionRewardScrap(grade);
                        state.scrap += reward;
                        LastResultSummary = $"Serviva {threshold}+. Successo! +{reward} Scrap.";
                    }
                }
            }
            else
            {
                int hpLoss = Mathf.CeilToInt(state.maxHp * 0.2f);
                state.currentHp = Mathf.Max(0, state.currentHp - hpLoss);
                LastResultSummary = $"Serviva {threshold}+. Fallito. -{hpLoss} PV.";

                if (state.IsDefeated)
                {
                    RunManager.Instance.HandleDefeat(); // now performs the full scene transition to Meta itself
                    LastResultSummary += " I PV sono arrivati a 0 - Frammentazione.";
                }
            }
        }
    }
}

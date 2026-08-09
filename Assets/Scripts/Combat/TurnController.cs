using System.Collections.Generic;
using UnityEngine;
using Rollrate.Data;
using Rollrate.Core;

namespace Rollrate.Combat
{
    /// <summary>
    /// The core, UI-agnostic engine for one combat turn's Roll & Keep
    /// loop (design doc Section 4).
    ///
    /// ALL 16 EFFECTS NOW COVERED (design doc Section 5), split across
    /// three places depending on WHEN each one's trigger naturally fires:
    /// - TurnContext: Bulwark, Overclock, Amplify, Cleanse, Chain (pure
    ///   single-turn math, no special timing).
    /// - RollAll below: Reverb (applies a value stored from a PREVIOUS
    ///   turn onto a die, the moment it's drawn again).
    /// - Reroll below: Reflect (doubles every reroll this turn if any
    ///   held Flow die carries it), SafetyNet (Scrap when a rerolled die
    ///   improves), Cascade (adds to Attack when a rerolled die improves).
    /// - ResolveCheck below: Overkill, Breach, Backlash, Cushion,
    ///   Overflow, Drain, Suppress (needs the enemy's numbers and/or the
    ///   final summed Attack/Defense).
    /// </summary>
    public class TurnController
    {
        public TurnContext Context { get; private set; }
        public int RerollsUsedThisTurn { get; private set; }

        /// <summary>Rerolls still available right now, given current held state.</summary>
        public int RerollsRemaining => Mathf.Max(0, Context.ComputeAvailableRerolls() - RerollsUsedThisTurn);

        private GameState _state;

        /// <summary>
        /// SET-UP + ROLL (design doc Section 4): rolls the Core Die once
        /// and draws+rolls a Hand of `handSize` dice from GameState's
        /// Draw Pile. Discards the PREVIOUS turn's hand first. Applies
        /// any pending Reverb bonus (design doc Section 5) onto a die
        /// the moment it's drawn - Reverb stores its value against the
        /// specific DieInstance, so it only actually lands if that same
        /// die happens to be drawn again (by chance, from the shuffled
        /// deck), same as any other die.
        /// </summary>
        public TurnContext RollAll(GameState state, int handSize, int? inhibitedValue)
        {
            _state = state;

            if (_currentHand != null)
            {
                state.DiscardHand(_currentHand);
            }

            Context = new TurnContext
            {
                coreDie = state.coreDie,
                hasInhibitedValue = inhibitedValue.HasValue,
                inhibitedValue = inhibitedValue ?? 0
            };

            if (state.coreDie != null)
            {
                Context.coreValue = Random.Range(1, state.coreDie.faces + 1);
                Context.coreIsEven = state.coreDie.IsEven(Context.coreValue);
                Context.coreRange = state.coreDie.GetRange(Context.coreValue);
            }

            _currentHand = state.DrawHand(handSize);
            foreach (var instance in _currentHand)
            {
                var d = new HeldDieState(instance);
                if (instance.data != null)
                {
                    d.rolledValue = Random.Range(1, instance.data.faces + 1);
                }

                if (state.pendingNextTurnBonus.TryGetValue(instance, out float pending))
                {
                    d.rolledValue += Mathf.RoundToInt(pending);
                    state.pendingNextTurnBonus.Remove(instance);
                }

                Context.dice.Add(d);
            }

            RerollsUsedThisTurn = 0;
            return Context;
        }

        private List<DieInstance> _currentHand;

        /// <summary>Single click on a die toggles whether it's marked for the next Reroll - does NOT affect whether the die counts toward Attacco/Difesa/rilanci, which every die in the Hand always does.</summary>
        public void ToggleRerollMark(HeldDieState die)
        {
            if (die == null) return;
            die.markedForReroll = !die.markedForReroll;
        }

        /// <summary>ECHO SYSTEM: assigns a SINGLE other die in the Hand to receive this Echo die's value. Only valid from an Echo-type die, to any OTHER non-Echo die. Clears any previous Chain (type-wide) assignment on this same die.</summary>
        public bool AssignEchoTarget(HeldDieState echoDie, HeldDieState target)
        {
            if (echoDie?.instance == null || echoDie.instance.type != DieType.Echo) return false;
            if (target == echoDie) return false;
            if (target != null && target.instance?.type == DieType.Echo) return false;

            echoDie.echoTarget = target;
            echoDie.echoTargetType = null;
            return true;
        }

        /// <summary>ECHO SYSTEM + Chain (design doc Section 5): assigns ALL non-Echo dice of the given Type in the Hand to receive this Echo die's value, instead of a single die. Only valid if the Echo die carries Chain.</summary>
        public bool AssignEchoTargetType(HeldDieState echoDie, DieType targetType)
        {
            if (echoDie?.instance == null || echoDie.instance.type != DieType.Echo) return false;
            if (!Context.HasEffect(echoDie, EffectId.Chain)) return false;
            if (targetType == DieType.Echo) return false;

            echoDie.echoTargetType = targetType;
            echoDie.echoTarget = null;
            return true;
        }

        public struct RerollResult
        {
            public List<HeldDieState> rerolledDice;
            public int scrapGained; // SafetyNet
        }

        /// <summary>
        /// KEEP &amp; REROLL (design doc Section 4): consumes exactly 1
        /// reroll and rerolls every die the player has MARKED. If any
        /// held Flow die carries Reflect, each rerolled die is rolled
        /// TWICE and keeps the better result (design doc Section 5).
        /// SafetyNet awards Scrap, Cascade adds to this turn's Attack,
        /// for any rerolled die carrying them whose value improved.
        /// </summary>
        public RerollResult Reroll()
        {
            var result = new RerollResult { rerolledDice = new List<HeldDieState>(), scrapGained = 0 };
            if (RerollsRemaining <= 0) return result;

            bool hasReflect = Context.GetOfType(DieType.Flow).Exists(d => Context.HasEffect(d, EffectId.Reflect));

            foreach (var d in Context.dice)
            {
                if (!d.markedForReroll) continue;
                if (d.instance?.data == null) continue;

                int oldValue = d.rolledValue;
                int faces = d.instance.data.faces;

                int rollA = Random.Range(1, faces + 1);
                int newValue = rollA;
                if (hasReflect)
                {
                    int rollB = Random.Range(1, faces + 1);
                    newValue = Mathf.Max(rollA, rollB);
                }

                d.rolledValue = newValue;
                d.markedForReroll = false;
                result.rerolledDice.Add(d);

                if (newValue > oldValue)
                {
                    int improvement = newValue - oldValue;
                    if (Context.HasEffect(d, EffectId.SafetyNet)) result.scrapGained += improvement;
                    if (Context.HasEffect(d, EffectId.Cascade)) Context.cascadeBonusThisTurn += improvement;
                }
            }

            if (result.rerolledDice.Count > 0) RerollsUsedThisTurn++;
            return result;
        }

        /// <summary>Result of one turn's CHECK, with every applicable Effect already applied.</summary>
        public struct CheckResult
        {
            public float attack;
            public bool attackSucceeded;
            public int excess;
            public float defense;
            public bool defenseHeld;
            public int damageTaken;
            public int bonusDamageToEnemy;    // Backlash
            public int scrapGained;           // Cushion
            public int hpHealed;              // Drain
            public int defenseBonusFromOverflow; // already folded into 'defense' below, reported separately for the result text
            public int enemyAttackReductionNextTurn; // Suppress - caller (EnemyController) should store this for its NEXT turn's Attack
        }

        /// <summary>
        /// CHECK (Risoluzione, doppia e indipendente) - design doc
        /// Section 4, with every Effect that has a single-turn trigger
        /// applied: Overkill/Breach/Overflow/Drain (Power), Backlash/
        /// Cushion/Suppress (Stability). Reverb's cross-turn storage
        /// happens separately (see StoreReverbPending, called by the UI
        /// layer right after this, for any Echo die with Reverb).
        /// </summary>
        public CheckResult ResolveCheck(float enemyThreshold, float enemyAttack, float attackAdjustment = 0f, float defenseAdjustment = 0f)
        {
            float attack = Context.ComputeAttack() + attackAdjustment;

            float excessMultiplier = 1f;
            float enemyAttackMultiplier = 1f;
            float overflowDefenseBonus = 0f;
            float drainHeal = 0f;

            foreach (var d in Context.GetOfType(DieType.Power))
            {
                float value = Context.GetEffectiveValue(d);
                bool qualifiesForThreshold = attack > 0f && value >= attack / 2f;
                if (qualifiesForThreshold)
                {
                    if (Context.HasEffect(d, EffectId.Overkill)) excessMultiplier *= 2f;
                    if (Context.HasEffect(d, EffectId.Breach)) enemyAttackMultiplier *= 0.5f;
                }
            }

            bool attackSucceeded = attack >= enemyThreshold;
            // Un Attacco riuscito fa SEMPRE almeno 1 danno, anche se il margine e' cosi'
            // piccolo che l'Eccesso arrotondato darebbe 0 (es. Attacco 7.2 vs Soglia 7 ->
            // Eccesso 0.2 -> arrotondato a 0 senza questo Max) - deciso col designer:
            // "successo" deve sempre corrispondere a un effetto visibile.
            int excess = attackSucceeded ? Mathf.Max(1, Mathf.RoundToInt((attack - enemyThreshold) * excessMultiplier)) : 0;

            // Overflow / Drain (design doc Section 5): each qualifying die's
            // own effective value converts to bonus Defense / healing,
            // capped at the turn's total Excess (a simplification - the
            // design doc doesn't specify exact per-die attribution when
            // several dice qualify at once).
            if (attackSucceeded && excess > 0)
            {
                foreach (var d in Context.GetOfType(DieType.Power))
                {
                    float value = Context.GetEffectiveValue(d);
                    float capped = Mathf.Min(value, excess);
                    if (Context.HasEffect(d, EffectId.Overflow)) overflowDefenseBonus += capped;
                    if (Context.HasEffect(d, EffectId.Drain)) drainHeal += capped;
                }
            }

            float effectiveEnemyAttack = enemyAttack * enemyAttackMultiplier;
            float defense = Context.ComputeDefense() + overflowDefenseBonus + defenseAdjustment;
            bool defenseHeld = defense >= effectiveEnemyAttack;
            int damageTaken = defenseHeld ? 0 : Mathf.RoundToInt(effectiveEnemyAttack - defense);

            int bonusDamageToEnemy = 0;
            int scrapGained = 0;
            int enemyAttackReductionNextTurn = 0;
            if (defenseHeld)
            {
                int defenseExcess = Mathf.RoundToInt(defense - effectiveEnemyAttack);
                var stabilityDice = Context.GetOfType(DieType.Stability);
                if (stabilityDice.Exists(d => Context.HasEffect(d, EffectId.Backlash))) bonusDamageToEnemy = defenseExcess;
                if (stabilityDice.Exists(d => Context.HasEffect(d, EffectId.Cushion))) scrapGained = defenseExcess;
                if (stabilityDice.Exists(d => Context.HasEffect(d, EffectId.Suppress))) enemyAttackReductionNextTurn = defenseExcess;
            }

            return new CheckResult
            {
                attack = attack,
                attackSucceeded = attackSucceeded,
                excess = excess,
                defense = defense,
                defenseHeld = defenseHeld,
                damageTaken = damageTaken,
                bonusDamageToEnemy = bonusDamageToEnemy,
                scrapGained = scrapGained,
                hpHealed = Mathf.RoundToInt(drainHeal),
                defenseBonusFromOverflow = Mathf.RoundToInt(overflowDefenseBonus),
                enemyAttackReductionNextTurn = enemyAttackReductionNextTurn
            };
        }

        /// <summary>
        /// Reverb (design doc Section 5): for every Echo die that
        /// carries it and has a target assigned, stores its effective
        /// value against the TARGET die instance in GameState, to be
        /// applied automatically next time that instance is drawn (see
        /// RollAll). Call once per turn, after ResolveCheck (Reverb
        /// doesn't affect THIS turn's math beyond the normal Echo
        /// transfer already folded into GetEffectiveValue).
        /// </summary>
        public void StoreReverbPending(GameState state)
        {
            foreach (var d in Context.GetOfType(DieType.Echo))
            {
                if (!Context.HasEffect(d, EffectId.Reverb)) continue;
                if (d.echoTarget?.instance == null) continue;

                float value = d.rolledValue * Context.GetNetMultiplier(d);
                if (Context.HasEffect(d, EffectId.Amplify)) value *= 2f;

                state.pendingNextTurnBonus.TryGetValue(d.echoTarget.instance, out float existing);
                state.pendingNextTurnBonus[d.echoTarget.instance] = existing + value;
            }
        }
    }
}

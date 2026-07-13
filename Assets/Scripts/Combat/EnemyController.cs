using System.Collections.Generic;
using UnityEngine;
using Rollrate.Data;

namespace Rollrate.Combat
{
    /// <summary>
    /// Holds the runtime state of the enemy in the current fight: current HP,
    /// the Inhibitor Die roll for this turn, the Threshold calculation, and
    /// any persistent state its ability needs across turns (e.g. Gatekeeper's
    /// growing Threshold bonus, Prism's remembered target slot, Judge's
    /// growing set of permanently inhibited values, Sovereign's locked
    /// destroy-value). This persistent state lives here - not on the shared
    /// ability instance in EnemyAbilityRegistry - because the same ability
    /// class instance is reused across every enemy of that type.
    /// </summary>
    public class EnemyController : MonoBehaviour
    {
        /// <summary>
        /// Set to false by the Balance Simulator before running thousands of
        /// fights, to avoid the significant overhead of millions of Debug.Log
        /// calls (string formatting + console writes) slowing the run down.
        /// Always true during normal gameplay.
        /// </summary>
        public static bool VerboseLogging = true;

        [Header("Enemy Data")]
        [SerializeField] private EnemyData enemyData;

        public int CurrentHp { get; private set; }
        public int MaxHp => enemyData != null ? enemyData.maxHp : 0;
        public int LastInhibitedValue { get; private set; }
        public DieData InhibitorDieType => enemyData != null ? enemyData.inhibitorDie : null;
        public bool IsDefeated => CurrentHp <= 0;

        /// <summary>The full EnemyData asset for this fight - used by the enemy info tooltip.</summary>
        public EnemyData Data => enemyData;

        // --- Persistent per-enemy ability state ---
        private int _permanentThresholdBonus;
        private SlotType? _prismTargetSlot;
        private readonly HashSet<int> _permanentlyInhibitedValues = new HashSet<int>();
        public int PersistentDestroyValue { get; private set; } = -1; // Sovereign: -1 = not yet locked in

        private void Awake()
        {
            // Populated here (not in Start()) so enemy HP/data is guaranteed
            // ready before any other script's Start() runs.
            StartFight(enemyData);
        }

        /// <summary>Resets this controller for a fresh fight against the given enemy.</summary>
        public void StartFight(EnemyData data)
        {
            enemyData = data;
            CurrentHp = enemyData != null ? enemyData.maxHp : 0;
            LastInhibitedValue = 0;
            _permanentThresholdBonus = 0;
            _prismTargetSlot = null;
            _permanentlyInhibitedValues.Clear();
            PersistentDestroyValue = -1;
            if (VerboseLogging) Debug.Log($"[EnemyController] Fight started against {enemyData?.displayName}, HP: {CurrentHp}");
        }

        /// <summary>
        /// Rolls this enemy's Inhibitor Die and triggers its ability's
        /// OnTurnStart hook. Call this at the start of a turn, alongside
        /// the player's own dice roll.
        /// </summary>
        public void RollInhibitor()
        {
            if (enemyData != null)
            {
                EnemyAbilityRegistry.Get(enemyData.abilityId).OnTurnStart(this);
            }

            if (enemyData == null || enemyData.inhibitorDie == null)
            {
                LastInhibitedValue = 0;
                return;
            }

            LastInhibitedValue = Random.Range(1, enemyData.inhibitorDie.faces + 1);
            if (VerboseLogging) Debug.Log($"[EnemyController] Inhibitor rolled: {LastInhibitedValue} (dice showing this value are inhibited this turn)");
        }

        /// <summary>Computes this turn's effective Threshold: base + permanent bonus + this ability's modifier.</summary>
        public int GetEffectiveThreshold(EnemyAbilityContext abilityCtx)
        {
            if (enemyData == null) return 0;
            var ability = EnemyAbilityRegistry.Get(enemyData.abilityId);
            int baseThreshold = enemyData.baseThreshold + _permanentThresholdBonus;
            return ability.ApplyThresholdModifier(baseThreshold, abilityCtx);
        }

        public int GetExtraInhibitedValue(EnemyAbilityContext abilityCtx)
        {
            if (enemyData == null) return -1;
            return EnemyAbilityRegistry.Get(enemyData.abilityId).GetExtraInhibitedValue(abilityCtx);
        }

        public int GetBonusDamageOnFailure(EnemyAbilityContext abilityCtx)
        {
            if (enemyData == null) return 0;
            return EnemyAbilityRegistry.Get(enemyData.abilityId).GetBonusDamageOnFailure(abilityCtx);
        }

        public int ModifyDieValue(SlotType slot, int rawValue, DieData dieType, EnemyAbilityContext abilityCtx)
        {
            if (enemyData == null) return rawValue;
            return EnemyAbilityRegistry.Get(enemyData.abilityId).ModifyPlacedDieValue(slot, rawValue, dieType, abilityCtx);
        }

        /// <summary>Notifies this enemy's ability that the turn has fully resolved.</summary>
        public void NotifyTurnEnd(EnemyAbilityContext abilityCtx)
        {
            if (enemyData == null) return;
            EnemyAbilityRegistry.Get(enemyData.abilityId).OnTurnEnd(this, abilityCtx);
        }

        /// <summary>
        /// Notifies this enemy's ability that Second Chance's reroll was
        /// actually used this turn. Returns any extra direct HP damage the
        /// ability inflicts as a reaction (e.g. Warden's Stasis).
        /// </summary>
        public int NotifyFlowRerollUsed(EnemyAbilityContext abilityCtx)
        {
            if (enemyData == null) return 0;
            return EnemyAbilityRegistry.Get(enemyData.abilityId).OnFlowRerollUsed(abilityCtx);
        }

        /// <summary>Returns a copy of the permanently inhibited values (Judge).</summary>
        public HashSet<int> GetPermanentlyInhibitedValues() => new HashSet<int>(_permanentlyInhibitedValues);

        // --- Methods abilities call (via the EnemyController passed into EnemyAbilityContext) to store persistent state ---
        public void AddPermanentThresholdBonus(int amount) => _permanentThresholdBonus += amount;
        public void SetPrismTargetSlot(SlotType slot) => _prismTargetSlot = slot;
        public SlotType? GetPrismTargetSlot() => _prismTargetSlot;
        public void AddPermanentInhibitedValue(int value) => _permanentlyInhibitedValues.Add(value);
        public void SetPersistentDestroyValue(int value) => PersistentDestroyValue = value;

        /// <summary>Applies damage to the enemy, clamped at 0.</summary>
        public void ApplyDamage(int amount)
        {
            CurrentHp = Mathf.Max(0, CurrentHp - amount);
            if (VerboseLogging) Debug.Log($"[EnemyController] Took {amount} damage, HP now {CurrentHp}/{MaxHp}");

            if (IsDefeated)
            {
                if (VerboseLogging) Debug.Log($"[EnemyController] {enemyData?.displayName} defeated!");
            }
        }
    }
}

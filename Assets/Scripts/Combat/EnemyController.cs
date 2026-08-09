using UnityEngine;
using Rollrate.Data;

namespace Rollrate.Combat
{
    /// <summary>
    /// Holds the runtime state of the enemy in the current fight: current
    /// HP, the Inhibitor Die roll for this turn, and every piece of
    /// persistent state the 15 Abilities need (permanent bonuses,
    /// next-turn pending bonuses, a permanently-inhibited value, etc.).
    /// The actual ability BEHAVIOR (what each one does and when) lives in
    /// EnemyAbilityResolver, not here - this class only stores the state
    /// and exposes the final Threshold/Attack for the turn.
    /// </summary>
    public class EnemyController : MonoBehaviour
    {
        [Header("Enemy Data")]
        [SerializeField] private EnemyData enemyData;

        public int CurrentHp { get; private set; }
        public int MaxHp => enemyData != null ? enemyData.maxHp : 0;
        public int BaseThreshold => enemyData != null ? enemyData.baseThreshold : 0;
        public int BaseAttack => enemyData != null ? enemyData.baseAttack : 0;
        public int LastInhibitedValue { get; private set; }
        public DieData InhibitorDieType => enemyData != null ? enemyData.inhibitorDie : null;
        public bool IsDefeated => CurrentHp <= 0;
        public EnemyAbilityId AbilityId => enemyData != null ? enemyData.abilityId : EnemyAbilityId.None;

        /// <summary>The full EnemyData asset for this fight - used by the enemy info tooltip.</summary>
        public EnemyData Data => enemyData;

        // --- Per-turn state (Suppress, Inhibitor Parity) ---
        private int _pendingAttackReduction; // Suppress (design doc Section 5) - applied to the NEXT turn's Attack, then cleared
        private bool _inhibitorBoostsAttackThisTurn; // true = Odd roll (boosts Attack), false = Even roll (boosts Threshold) - design doc Section 4

        /// <summary>PUNTO APERTO: magnitudine non specificata dal design doc - +2 flat, provvisorio, coerente con gli altri bonus del gioco.</summary>
        public const int InhibitorParityBoost = 2;

        // --- Ability persistent state (design doc Section 8) ---
        /// <summary>Gatekeeper [Clockwork]: accumulates forever, +2 every turn start.</summary>
        public int PermanentThresholdBonus { get; private set; }
        /// <summary>Cantor [Discord] / Tracer [Pressure]: queued at the end of a turn, consumed at the start of the NEXT.</summary>
        public int PendingNextTurnThresholdBonus { get; private set; }
        public int PendingNextTurnAttackBonus { get; private set; }
        /// <summary>Eraser [Backlash]: reset every turn, +2 per Reroll used THIS turn only.</summary>
        public int RerollThresholdBonusThisTurn { get; private set; }
        /// <summary>Judge [Sentence]: chosen once (first turn) and kept for the rest of the fight.</summary>
        public int? PermanentExtraInhibitedValue { get; private set; }
        /// <summary>Prism [Refraction]: re-rolled every turn - which Type gets halved this turn.</summary>
        public DieType RefractionTargetTypeThisTurn { get; private set; }

        public void AddPermanentThresholdBonus(int amount) => PermanentThresholdBonus += amount;
        public void QueueNextTurnThresholdBonus(int amount) => PendingNextTurnThresholdBonus += amount;
        public void QueueNextTurnAttackBonus(int amount) => PendingNextTurnAttackBonus += amount;
        public void AddRerollThresholdBonusThisTurn(int amount) => RerollThresholdBonusThisTurn += amount;
        public void SetPermanentExtraInhibitedValueOnce(int value)
        {
            if (!PermanentExtraInhibitedValue.HasValue) PermanentExtraInhibitedValue = value;
        }
        public void RollRefractionTargetType()
        {
            var types = new[] { DieType.Power, DieType.Stability, DieType.Flow, DieType.Echo };
            RefractionTargetTypeThisTurn = types[Random.Range(0, types.Length)];
        }

        /// <summary>This turn's Threshold: base + Inhibitor Parity boost (if Even) + permanent + pending-from-last-turn + this-turn's-reroll bonuses. Clears the two per-turn/pending components that are spent once read.</summary>
        public int GetThresholdForThisTurn()
        {
            int threshold = BaseThreshold
                + (!_inhibitorBoostsAttackThisTurn ? InhibitorParityBoost : 0)
                + PermanentThresholdBonus
                + PendingNextTurnThresholdBonus
                + RerollThresholdBonusThisTurn;
            return threshold;
        }

        /// <summary>Suppress (design doc Section 5): queues a reduction to apply to this enemy's Attack starting NEXT turn.</summary>
        public void QueuePendingAttackReduction(int amount)
        {
            if (amount > 0) _pendingAttackReduction += amount;
        }

        /// <summary>This turn's actual Attack: base + Inhibitor Parity boost (if Odd) + pending-from-last-turn - Suppress reduction. Clamped at 0.</summary>
        public int GetAttackForThisTurnAndClearPending()
        {
            int attack = BaseAttack
                + (_inhibitorBoostsAttackThisTurn ? InhibitorParityBoost : 0)
                + PendingNextTurnAttackBonus;
            attack = Mathf.Max(0, attack - _pendingAttackReduction);
            _pendingAttackReduction = 0;
            return attack;
        }

        /// <summary>Call once per turn, right after ResolveCheck, to clear the "pending from last turn" bonuses and the per-turn reroll bonus - they've been read and applied already.</summary>
        public void ClearPerTurnAbilityState()
        {
            PendingNextTurnThresholdBonus = 0;
            PendingNextTurnAttackBonus = 0;
            RerollThresholdBonusThisTurn = 0;
        }

        private void Awake()
        {
            // Only capture WHICH enemy to fight here - do NOT touch GameState yet.
            // Unity does not guarantee Awake() order between different GameObjects:
            // if RunManager's own Awake() (which populates the dice pool) happened
            // to run AFTER this one, ResetForNewFight() would build an empty deck
            // for the whole fight - permanently 0 dice drawable, Attack/Defense
            // always 0, indistinguishable from "damage doesn't work". Start() is
            // guaranteed by Unity to run only after EVERY object's Awake() has
            // already completed, so it's the safe place for this.
            _pendingEnemyData = Rollrate.Core.CombatNodeContext.PendingEnemy != null
                ? Rollrate.Core.CombatNodeContext.PendingEnemy
                : enemyData;

            Rollrate.Core.CombatNodeContext.PendingEnemy = null; // consumed
        }

        private EnemyData _pendingEnemyData;

        private void Start()
        {
            StartFight(_pendingEnemyData);
        }

        /// <summary>Resets this controller for a fresh fight against the given enemy.</summary>
        public void StartFight(EnemyData data)
        {
            enemyData = data;
            CurrentHp = enemyData != null ? enemyData.maxHp : 0;
            LastInhibitedValue = 0;
            _pendingAttackReduction = 0;
            _inhibitorBoostsAttackThisTurn = false;
            PermanentThresholdBonus = 0;
            PendingNextTurnThresholdBonus = 0;
            PendingNextTurnAttackBonus = 0;
            RerollThresholdBonusThisTurn = 0;
            PermanentExtraInhibitedValue = null;
            RefractionTargetTypeThisTurn = DieType.Power;

            if (Rollrate.Core.RunManager.Instance != null)
            {
                var state = Rollrate.Core.RunManager.Instance.State;
                state.ResetForNewFight();
                Debug.Log($"[EnemyController] Deck built for this fight: {state.drawPile.Count} dice in Draw Pile, {state.dicePool.Count} owned total.");
                if (state.dicePool.Count == 0)
                {
                    Debug.LogWarning("[EnemyController] Dice Pool is EMPTY - RunManager's Debug Starting Pool is probably not configured. Attack/Defense will always be 0 with no dice to draw.");
                }
            }
            else
            {
                Debug.LogWarning("[EnemyController] RunManager.Instance is null at fight start - no run in progress? The deck was NOT built, Attack/Defense will be 0 all fight.");
            }

            Debug.Log($"[EnemyController] Fight started against {enemyData?.displayName}, HP: {CurrentHp}, Threshold: {BaseThreshold}, Attack: {BaseAttack}, Ability: {AbilityId}");
        }

        /// <summary>Rolls this enemy's Inhibitor Die and computes the Parity boost (design doc Section 4).</summary>
        public void RollInhibitor()
        {
            if (enemyData == null || enemyData.inhibitorDie == null)
            {
                LastInhibitedValue = 0;
                _inhibitorBoostsAttackThisTurn = false;
                return;
            }

            LastInhibitedValue = Random.Range(1, enemyData.inhibitorDie.faces + 1);
            _inhibitorBoostsAttackThisTurn = LastInhibitedValue % 2 != 0; // Odd -> boosts Attack, Even -> boosts Threshold
            Debug.Log($"[EnemyController] Inhibitor rolled: {LastInhibitedValue} ({(_inhibitorBoostsAttackThisTurn ? "Odd, +2 Attack" : "Even, +2 Threshold")} this turn).");
        }

        /// <summary>Applies damage to the enemy, clamped at 0.</summary>
        public void ApplyDamage(int amount)
        {
            CurrentHp = Mathf.Max(0, CurrentHp - amount);
            Debug.Log($"[EnemyController] Took {amount} damage, HP now {CurrentHp}/{MaxHp}");

            if (IsDefeated)
            {
                Debug.Log($"[EnemyController] {enemyData?.displayName} defeated!");
            }
        }
    }
}

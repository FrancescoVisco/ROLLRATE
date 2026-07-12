using UnityEngine;
using Rollrate.Core;
using Rollrate.Data;
using Rollrate.Combat;

namespace Rollrate.UI
{
    /// <summary>
    /// Orchestrates one CHECK phase: reads the Core Die roll, reads what's
    /// placed in each of the 4 slots, resolves each slot's Static + Frequency
    /// effect via ModuleResolver, and sums the Power slot total to log a
    /// pass/fail against a debug Threshold.
    ///
    /// This is a first working version for testing - enemy Threshold and
    /// Inhibition are hardcoded debug fields for now, to be replaced once
    /// the enemy system exists.
    /// </summary>
    public class CombatController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DiceRoller diceRoller;
        [SerializeField] private SlotDropZone powerSlot;
        [SerializeField] private SlotDropZone stabilitySlot;
        [SerializeField] private SlotDropZone flowSlot;
        [SerializeField] private SlotDropZone echoSlot;

        [Header("Debug Only - Enemy Turn Values")]
        [Tooltip("Hardcoded for testing. Replace with real enemy data once the enemy system exists.")]
        [SerializeField] private int debugThreshold = 10;
        [SerializeField] private int debugInhibitedValue = 0; // 0 = no value inhibited

        /// <summary>
        /// Call this after the player has placed dice in the slots and wants
        /// to resolve the turn (the CHECK phase).
        ///
        /// Runs in two passes: Power is resolved first (its total doesn't
        /// depend on the Threshold comparison), then Stability/Echo are
        /// resolved with ctx.PointsExceedingThreshold filled in, since some
        /// of their modules (Changeover, Sync, Overload) need that number.
        /// </summary>
        public void ResolveTurn()
        {
            var state = RunManager.Instance.State;
            CombatContext ctx = BuildContext(state);

            // Apply Overload's pending bonus (if any) to the first Power die
            // placed this turn, then clear it - it's consumed on use.
            int powerDieBonus = state.pendingNextTurnPowerBonus;
            state.pendingNextTurnPowerBonus = 0;

            // --- Pass 1: Power ---
            state.installedModules.TryGetValue(SlotType.Power, out ModuleData powerModule);
            int? powerDieValue = powerSlot != null && powerSlot.placedDie != null ? powerSlot.placedDie.rolledValue : (int?)null;

            ModuleResult powerResult = ModuleResolver.ResolveSlot(powerModule, powerDieValue, ctx);
            int powerTotal = powerResult.ValueBonus + powerDieBonus;
            Debug.Log($"[CombatController] Power: {powerResult.DebugLog} (+{powerDieBonus} carried Overload bonus) = {powerTotal}");

            bool success = powerTotal >= debugThreshold;
            int excess = Mathf.Max(0, powerTotal - debugThreshold);
            ctx.PointsExceedingThreshold = excess;

            // --- Pass 2: Stability, Flow, Echo (now that excess is known) ---
            ModuleResult stabilityResult = ResolveAndLog(SlotType.Stability, stabilitySlot, state, ctx);
            ModuleResult flowResult = ResolveAndLog(SlotType.Flow, flowSlot, state, ctx);
            ModuleResult echoResult = ResolveAndLog(SlotType.Echo, echoSlot, state, ctx);

            // --- Apply all deferred/direct effects to GameState ---
            ApplyResultToState(state, stabilityResult);
            ApplyResultToState(state, flowResult);
            ApplyResultToState(state, echoResult);

            // Aiming: only applies if this turn was a success.
            if (success && stabilityResult.NextThresholdReductionPercent > 0f)
            {
                state.pendingThresholdReductionPercent = stabilityResult.NextThresholdReductionPercent;
                Debug.Log($"[CombatController] Aiming applied: next Threshold will drop by {stabilityResult.NextThresholdReductionPercent:P0}");
            }

            Debug.Log($"[CombatController] Power Total = {powerTotal} vs Threshold {debugThreshold} -> {(success ? "SUCCESS" : "FAILURE")}");
        }

        /// <summary>
        /// Applies a slot's result to persistent GameState: HP, Scrap,
        /// Changeover Charges (converting to a die every 10), and Overload's
        /// bonus for next turn.
        /// </summary>
        private void ApplyResultToState(GameState state, ModuleResult result)
        {
            if (result.HpRecovered > 0)
            {
                state.currentHp = Mathf.Min(state.maxHp, state.currentHp + result.HpRecovered);
            }

            if (result.ScrapGained > 0)
            {
                state.scrap += result.ScrapGained;
            }

            if (result.ChargesGenerated > 0)
            {
                state.changeoverCharges += result.ChargesGenerated;
                while (state.changeoverCharges >= 10)
                {
                    state.changeoverCharges -= 10;
                    Debug.Log("[CombatController] Changeover: 10 Charges reached - TODO add a real +1 Die to the pool (needs a die-choice UI or a default rule)");
                }
            }

            if (result.NextTurnPowerBonus > 0)
            {
                state.pendingNextTurnPowerBonus += result.NextTurnPowerBonus;
            }
        }

        /// <summary>
        /// Sends every die currently placed in any slot back to the hand,
        /// clearing the board. Call this if the player wants to redo their
        /// placement before resolving the turn.
        /// </summary>
        public void ResetPlacement()
        {
            powerSlot?.ReturnPlacedDie();
            stabilitySlot?.ReturnPlacedDie();
            flowSlot?.ReturnPlacedDie();
            echoSlot?.ReturnPlacedDie();

            Debug.Log("[CombatController] Placement reset - all dice returned to hand.");
        }

        private CombatContext BuildContext(GameState state)
        {
            int coreValue = diceRoller.LastCoreRolledValue;
            ValueRange coreRange = state.coreDie != null ? state.coreDie.GetRange(coreValue) : ValueRange.DeadZone;
            bool coreIsEven = state.coreDie != null && state.coreDie.IsEven(coreValue);

            // Aiming's effect from a previous turn: reduce this turn's Threshold, then consume it.
            int effectiveThreshold = debugThreshold;
            if (state.pendingThresholdReductionPercent > 0f)
            {
                effectiveThreshold = Mathf.RoundToInt(debugThreshold * (1f - state.pendingThresholdReductionPercent));
                Debug.Log($"[CombatController] Aiming consumed: Threshold {debugThreshold} -> {effectiveThreshold} (-{state.pendingThresholdReductionPercent:P0})");
                state.pendingThresholdReductionPercent = 0f;
            }
            debugThreshold = effectiveThreshold;
            // NOTE: this permanently overwrites the debug Threshold field.
            // Fine for now since there's no real enemy system yet (each
            // fight would normally reset the Threshold to a fresh base
            // value). Revisit once a real Enemy system exists.

            return new CombatContext
            {
                CoreValue = coreValue,
                CoreRange = coreRange,
                CoreIsEven = coreIsEven,
                InhibitedValue = debugInhibitedValue,
                PlayerHp = state.currentHp,
                PlayerHpMax = state.maxHp,
                EnemyThreshold = effectiveThreshold,
                FullResonanceActive = false // ResonanceDetector not implemented yet
            };
        }

        private ModuleResult ResolveAndLog(SlotType slot, SlotDropZone dropZone, GameState state, CombatContext ctx)
        {
            state.installedModules.TryGetValue(slot, out ModuleData module);

            int? placedValue = null;
            if (dropZone != null && dropZone.placedDie != null)
            {
                placedValue = dropZone.placedDie.rolledValue;
            }

            ModuleResult result = ModuleResolver.ResolveSlot(module, placedValue, ctx);
            Debug.Log($"[CombatController] {slot}: {result.DebugLog} (ValueBonus={result.ValueBonus}, Hp+={result.HpRecovered}, Scrap+={result.ScrapGained})");

            return result;
        }
    }
}

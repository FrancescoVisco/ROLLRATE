using UnityEngine;

namespace Rollrate.Combat.Modules
{
    /// <summary>
    /// Shift (Flow) - Static: the chosen target die's value increases by 1
    /// (clamped to the die's face range). Simplified from the design's
    /// "increase or decrease by 1" - direction choice isn't wired up yet,
    /// always increases for now.
    /// Frequency (Odd): the target's value can shift by up to the Core
    /// Die's value instead of just 1 (still clamped to the die's range).
    /// </summary>
    public class ShiftLogic : ModuleLogicBase
    {
        public override ModuleResult ApplyStaticEffect(int dieValue, CombatContext ctx)
        {
            if (!ctx.TargetDieValue.HasValue)
            {
                return new ModuleResult { DebugLog = "Shift: no target die chosen - no effect" };
            }

            int newValue = Mathf.Clamp(ctx.TargetDieValue.Value + 1, 1, ctx.TargetDieFaces);
            return new ModuleResult
            {
                NewTargetValue = newValue,
                DebugLog = $"Shift: target die {ctx.TargetDieValue.Value} -> {newValue} (+1, clamped to 1-{ctx.TargetDieFaces})"
            };
        }

        public override bool IsFrequencyConditionMet(CombatContext ctx)
        {
            return !ctx.CoreIsEven; // Odd
        }

        public override ModuleResult ApplyFrequencyEffect(int dieValue, CombatContext ctx)
        {
            if (!ctx.TargetDieValue.HasValue)
            {
                return new ModuleResult { DebugLog = "Shift Frequency: no target die chosen - no effect" };
            }

            int newValue = Mathf.Clamp(ctx.TargetDieValue.Value + ctx.CoreValue, 1, ctx.TargetDieFaces);
            return new ModuleResult
            {
                NewTargetValue = newValue,
                DebugLog = $"Shift Frequency: target die {ctx.TargetDieValue.Value} -> {newValue} (+{ctx.CoreValue} up to Core, clamped to 1-{ctx.TargetDieFaces})"
            };
        }
    }
}

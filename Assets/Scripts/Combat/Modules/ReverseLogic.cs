namespace Rollrate.Combat.Modules
{
    /// <summary>
    /// Reverse (Flow) - Static: the chosen target die's value is flipped
    /// (e.g. on a D6: 1&lt;-&gt;6, 2&lt;-&gt;5), using the formula
    /// newValue = faces + 1 - currentValue.
    /// Frequency (Low DCD): Total Inversion - flips every occupied slot's
    /// die instead of just the chosen target. This board-wide part is
    /// applied by CombatController directly (it needs access to all 4
    /// slots, not just this one's target), which also skips applying this
    /// Static single-target flip when Total Inversion triggers, so the
    /// target die isn't flipped twice.
    /// </summary>
    public class ReverseLogic : ModuleLogicBase
    {
        public override ModuleResult ApplyStaticEffect(int dieValue, CombatContext ctx)
        {
            if (!ctx.TargetDieValue.HasValue)
            {
                return new ModuleResult { DebugLog = "Reverse: no target die chosen - no effect" };
            }

            int newValue = ctx.TargetDieFaces + 1 - ctx.TargetDieValue.Value;
            return new ModuleResult
            {
                NewTargetValue = newValue,
                DebugLog = $"Reverse: target die {ctx.TargetDieValue.Value} -> {newValue} (flipped on {ctx.TargetDieFaces} faces)"
            };
        }

        public override bool IsFrequencyConditionMet(CombatContext ctx)
        {
            return ctx.CoreRange == Data.ValueRange.Low;
        }

        public override ModuleResult ApplyFrequencyEffect(int dieValue, CombatContext ctx)
        {
            // No NewTargetValue here on purpose - the board-wide flip is
            // handled separately by CombatController once it sees
            // FrequencyTriggered = true for this module.
            return new ModuleResult
            {
                DebugLog = "Reverse Frequency: Total Inversion - every occupied slot's die is flipped by CombatController"
            };
        }
    }
}

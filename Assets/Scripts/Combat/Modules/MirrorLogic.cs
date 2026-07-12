namespace Rollrate.Combat.Modules
{
    /// <summary>
    /// Mirror (Flow) - Static: the chosen target die's value becomes
    /// identical to the Core Die's value.
    /// Frequency (Even): the die placed in Mirror returns to the pool for
    /// the next turn instead of being discarded (no target needed for this part).
    /// </summary>
    public class MirrorLogic : ModuleLogicBase
    {
        public override ModuleResult ApplyStaticEffect(int dieValue, CombatContext ctx)
        {
            if (!ctx.TargetDieValue.HasValue)
            {
                return new ModuleResult { DebugLog = "Mirror: no target die chosen - no effect" };
            }

            return new ModuleResult
            {
                NewTargetValue = ctx.CoreValue,
                DebugLog = $"Mirror: target die's value becomes {ctx.CoreValue} (Core's value)"
            };
        }

        public override bool IsFrequencyConditionMet(CombatContext ctx)
        {
            return ctx.CoreIsEven;
        }

        public override ModuleResult ApplyFrequencyEffect(int dieValue, CombatContext ctx)
        {
            return new ModuleResult
            {
                ReturnDieToPoolNextTurn = true,
                DebugLog = "Mirror Frequency: the die placed here returns to the pool next turn"
            };
        }
    }
}

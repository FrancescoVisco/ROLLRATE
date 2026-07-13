namespace Rollrate.Combat.Modules
{
    /// <summary>
    /// Charge (Power) - Static: the placed die hits twice.
    /// Frequency (Even): adds TWICE the Core die's value to the final total.
    ///
    /// The x2 multiplier (originally x1) was introduced to make Core Die
    /// evolution matter more where it's most visible and explainable: a
    /// real per-turn dice mechanic tied to a named module, rather than a
    /// hidden passive bonus. Revisit this number after testing with
    /// variable pool sizes - if Core evolution still feels too flat,
    /// extending similar Core-value scaling to Constitution's Frequency
    /// Effect (currently HP-loss-only) is the next natural candidate.
    /// </summary>
    public class ChargeLogic : ModuleLogicBase
    {
        public override ModuleResult ApplyStaticEffect(int dieValue, CombatContext ctx)
        {
            return new ModuleResult
            {
                ValueBonus = dieValue * 2, // hits twice
                DebugLog = $"Charge: {dieValue} x2 = {dieValue * 2}"
            };
        }

        public override bool IsFrequencyConditionMet(CombatContext ctx)
        {
            return ctx.CoreIsEven;
        }

        public override ModuleResult ApplyFrequencyEffect(int dieValue, CombatContext ctx)
        {
            int bonus = ctx.CoreValue * 2;
            return new ModuleResult
            {
                ValueBonus = bonus,
                DebugLog = $"Charge Frequency: +{bonus} (2x Core Value, Core Even)"
            };
        }
    }
}

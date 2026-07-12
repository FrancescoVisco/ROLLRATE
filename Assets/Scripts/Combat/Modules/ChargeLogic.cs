namespace Rollrate.Combat.Modules
{
    /// <summary>
    /// Charge (Power) - Static: the placed die hits twice.
    /// Frequency (Even): adds the Core die's value to the final total.
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
            return new ModuleResult
            {
                ValueBonus = ctx.CoreValue,
                DebugLog = $"Charge Frequency: +{ctx.CoreValue} (Core Even)"
            };
        }
    }
}

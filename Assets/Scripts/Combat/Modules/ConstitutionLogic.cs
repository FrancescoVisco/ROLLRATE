namespace Rollrate.Combat.Modules
{
    /// <summary>
    /// Constitution (Power) - Static: die value + (+2 per HP lost this turn).
    /// Frequency (High DCD): the bonus per HP lost rises to +5 (total).
    /// </summary>
    public class ConstitutionLogic : ModuleLogicBase
    {
        public override ModuleResult ApplyStaticEffect(int dieValue, CombatContext ctx)
        {
            int bonus = dieValue + (ctx.HpLostThisTurn * 2);
            return new ModuleResult
            {
                ValueBonus = bonus,
                DebugLog = $"Constitution: {dieValue} + (2 x {ctx.HpLostThisTurn} HP lost) = {bonus}"
            };
        }

        public override bool IsFrequencyConditionMet(CombatContext ctx)
        {
            return ctx.CoreRange == Data.ValueRange.High;
        }

        public override ModuleResult ApplyFrequencyEffect(int dieValue, CombatContext ctx)
        {
            // Static already used x2 per HP lost; add x3 more to reach x5 total.
            int extra = ctx.HpLostThisTurn * 3;
            return new ModuleResult
            {
                ValueBonus = extra,
                DebugLog = $"Constitution Frequency: +3 more per HP lost (total +5) = {extra}"
            };
        }
    }
}

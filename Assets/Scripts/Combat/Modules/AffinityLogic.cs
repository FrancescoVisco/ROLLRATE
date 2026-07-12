namespace Rollrate.Combat.Modules
{
    /// <summary>
    /// Affinity (Power) - Static: +1 for every other slot activated this turn.
    /// Frequency (Odd): the bonus per activated slot rises to +2.
    /// </summary>
    public class AffinityLogic : ModuleLogicBase
    {
        public override ModuleResult ApplyStaticEffect(int dieValue, CombatContext ctx)
        {
            int bonus = ctx.SlotsActivatedThisTurn * 1;
            return new ModuleResult
            {
                ValueBonus = bonus,
                DebugLog = $"Affinity: +1 x {ctx.SlotsActivatedThisTurn} activated slots = {bonus}"
            };
        }

        public override bool IsFrequencyConditionMet(CombatContext ctx)
        {
            return !ctx.CoreIsEven; // Odd
        }

        public override ModuleResult ApplyFrequencyEffect(int dieValue, CombatContext ctx)
        {
            // Static already granted +1 per slot; add +1 more per slot to reach +2 total.
            int extra = ctx.SlotsActivatedThisTurn * 1;
            return new ModuleResult
            {
                ValueBonus = extra,
                DebugLog = $"Affinity Frequency: +1 more per slot (total +2) = {extra}"
            };
        }
    }
}

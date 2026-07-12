namespace Rollrate.Combat.Modules
{
    /// <summary>
    /// Solitary (Power) - Static: the bonus per activated slot is +2.
    /// Frequency (Low DCD): the bonus per empty slot rises to +5.
    /// </summary>
    public class SolitaryLogic : ModuleLogicBase
    {
        public override ModuleResult ApplyStaticEffect(int dieValue, CombatContext ctx)
        {
            int bonus = ctx.SlotsActivatedThisTurn * 2;
            return new ModuleResult
            {
                ValueBonus = bonus,
                DebugLog = $"Solitary: +2 x {ctx.SlotsActivatedThisTurn} activated slots = {bonus}"
            };
        }

        public override bool IsFrequencyConditionMet(CombatContext ctx)
        {
            return ctx.CoreRange == Data.ValueRange.Low;
        }

        public override ModuleResult ApplyFrequencyEffect(int dieValue, CombatContext ctx)
        {
            int bonus = ctx.EmptySlotsThisTurn * 5;
            return new ModuleResult
            {
                ValueBonus = bonus,
                DebugLog = $"Solitary Frequency: +5 x {ctx.EmptySlotsThisTurn} empty slots = {bonus}"
            };
        }
    }
}

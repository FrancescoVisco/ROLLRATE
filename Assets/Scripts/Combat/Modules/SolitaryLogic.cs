namespace Rollrate.Combat.Modules
{
    /// <summary>
    /// Solitary (Power) - Static: +2 for every EMPTY slot this turn.
    /// Frequency (Low DCD): the bonus per empty slot rises to +5.
    ///
    /// Fixed: the Static effect previously copied Affinity's "reward
    /// activated slots" formula, contradicting both its own name
    /// ("Isolamento") and its own Frequency effect, which already
    /// correctly scaled EmptySlotsThisTurn.
    /// </summary>
    public class SolitaryLogic : ModuleLogicBase
    {
        public override ModuleResult ApplyStaticEffect(int dieValue, CombatContext ctx)
        {
            int bonus = ctx.EmptySlotsThisTurn * 2;
            return new ModuleResult
            {
                ValueBonus = bonus,
                DebugLog = $"Solitary: +2 x {ctx.EmptySlotsThisTurn} empty slots = {bonus}"
            };
        }

        public override bool IsFrequencyConditionMet(CombatContext ctx)
        {
            return ctx.CoreRange == Data.ValueRange.Low;
        }

        public override ModuleResult ApplyFrequencyEffect(int dieValue, CombatContext ctx)
        {
            // Static already grants +2 per empty slot; add +3 more to reach +5 total.
            int extra = ctx.EmptySlotsThisTurn * 3;
            return new ModuleResult
            {
                ValueBonus = extra,
                DebugLog = $"Solitary Frequency: +3 more per empty slot (total +5) = {extra}"
            };
        }
    }
}

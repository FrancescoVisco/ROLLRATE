namespace Rollrate.Combat.Modules
{
    /// <summary>
    /// Second Chance (Flow) - Static: you may reroll one die of your choice
    /// from the pool.
    /// Frequency (High DCD): you may reroll up to 3 dice and pick the better
    /// result between old and new for each.
    /// </summary>
    public class SecondChanceLogic : ModuleLogicBase
    {
        public override ModuleResult ApplyStaticEffect(int dieValue, CombatContext ctx)
        {
            return new ModuleResult
            {
                DiceToReroll = 1,
                DebugLog = "Second Chance: may reroll 1 die from the pool"
            };
        }

        public override bool IsFrequencyConditionMet(CombatContext ctx)
        {
            return ctx.CoreRange == Data.ValueRange.High;
        }

        public override ModuleResult ApplyFrequencyEffect(int dieValue, CombatContext ctx)
        {
            return new ModuleResult
            {
                DiceToReroll = 3, // resolver takes the max, so this replaces the static 1
                RerollKeepsBetterResult = true,
                DebugLog = "Second Chance Frequency: may reroll up to 3 dice, keeping the better result"
            };
        }
    }
}

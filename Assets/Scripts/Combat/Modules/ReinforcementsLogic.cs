namespace Rollrate.Combat.Modules
{
    /// <summary>
    /// Reinforcements (Echo) - Static: add a temporary D4 to the pool for
    /// the next turn.
    /// Frequency (Even): the added die is not a D4, but a copy of the same
    /// type as the Core Die.
    /// </summary>
    public class ReinforcementsLogic : ModuleLogicBase
    {
        public override ModuleResult ApplyStaticEffect(int dieValue, CombatContext ctx)
        {
            return new ModuleResult
            {
                TempDiceToAddNextTurn = 1,
                DebugLog = "Reinforcements: +1 temporary D4 added to the pool next turn"
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
                AddedDiceCopyCoreType = true,
                DebugLog = "Reinforcements Frequency: the added die copies the Core Die's type instead of being a D4"
            };
        }
    }
}

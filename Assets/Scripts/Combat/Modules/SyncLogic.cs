namespace Rollrate.Combat.Modules
{
    /// <summary>
    /// Sync (Echo) - Static: if you exceed the Threshold, recover 1 HP.
    /// If already at max HP, gain 5 Scrap instead.
    /// Frequency (Low DCD): Total Sync - recover 3 HP AND gain 10 extra Scrap.
    ///
    /// Note: relies on CombatContext.PointsExceedingThreshold (see
    /// ChangeoverLogic) to know whether the Threshold was exceeded.
    /// </summary>
    public class SyncLogic : ModuleLogicBase
    {
        public override ModuleResult ApplyStaticEffect(int dieValue, CombatContext ctx)
        {
            bool exceededThreshold = ctx.PointsExceedingThreshold > 0;
            if (!exceededThreshold)
            {
                return new ModuleResult { DebugLog = "Sync: Threshold not exceeded, no effect" };
            }

            bool atMaxHp = ctx.PlayerHp >= ctx.PlayerHpMax;
            if (atMaxHp)
            {
                return new ModuleResult { ScrapGained = 5, DebugLog = "Sync: at max HP, +5 Scrap instead" };
            }

            return new ModuleResult { HpRecovered = 1, DebugLog = "Sync: +1 HP recovered" };
        }

        public override bool IsFrequencyConditionMet(CombatContext ctx)
        {
            return ctx.CoreRange == Data.ValueRange.Low;
        }

        public override ModuleResult ApplyFrequencyEffect(int dieValue, CombatContext ctx)
        {
            return new ModuleResult
            {
                HpRecovered = 3,
                ScrapGained = 10,
                DebugLog = "Sync Frequency: Total Sync - +3 HP and +10 extra Scrap"
            };
        }
    }
}

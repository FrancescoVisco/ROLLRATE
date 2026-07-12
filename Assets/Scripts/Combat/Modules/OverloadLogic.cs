namespace Rollrate.Combat.Modules
{
    /// <summary>
    /// Overload (Echo) - Static: if you exceed the Threshold, the first die
    /// in the Power Slot next turn gets +4.
    /// Frequency (High DCD): the Power Slot bonus next turn becomes +8.
    ///
    /// Note: relies on CombatContext.PointsExceedingThreshold (see
    /// ChangeoverLogic) to know whether the Threshold was exceeded.
    /// </summary>
    public class OverloadLogic : ModuleLogicBase
    {
        public override ModuleResult ApplyStaticEffect(int dieValue, CombatContext ctx)
        {
            bool exceededThreshold = ctx.PointsExceedingThreshold > 0;
            if (!exceededThreshold)
            {
                return new ModuleResult { DebugLog = "Overload: Threshold not exceeded, no effect" };
            }

            return new ModuleResult
            {
                NextTurnPowerBonus = 4,
                DebugLog = "Overload: +4 to next turn's first Power die"
            };
        }

        public override bool IsFrequencyConditionMet(CombatContext ctx)
        {
            return ctx.CoreRange == Data.ValueRange.High;
        }

        public override ModuleResult ApplyFrequencyEffect(int dieValue, CombatContext ctx)
        {
            // Static already grants +4; add +4 more to reach +8 total.
            return new ModuleResult
            {
                NextTurnPowerBonus = 4,
                DebugLog = "Overload Frequency: +4 more to next turn's Power bonus (total +8)"
            };
        }
    }
}

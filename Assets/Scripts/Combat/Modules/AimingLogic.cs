namespace Rollrate.Combat.Modules
{
    /// <summary>
    /// Aiming (Stability) - Static: if you win the turn, the next enemy
    /// Threshold drops by 20%.
    /// Frequency (Low DCD): the next enemy Threshold drops by 50% instead.
    ///
    /// Note: "if you win the turn" is only known after the Power slot is
    /// resolved, so CombatController should only apply this result if the
    /// turn was actually a success.
    /// </summary>
    public class AimingLogic : ModuleLogicBase
    {
        public override ModuleResult ApplyStaticEffect(int dieValue, CombatContext ctx)
        {
            return new ModuleResult
            {
                NextThresholdReductionPercent = 0.20f,
                DebugLog = "Aiming: -20% next enemy Threshold (only if this turn is a win)"
            };
        }

        public override bool IsFrequencyConditionMet(CombatContext ctx)
        {
            return ctx.CoreRange == Data.ValueRange.Low;
        }

        public override ModuleResult ApplyFrequencyEffect(int dieValue, CombatContext ctx)
        {
            return new ModuleResult
            {
                NextThresholdReductionPercent = 0.50f, // replaces the static 20% (resolver takes the max)
                DebugLog = "Aiming Frequency: -50% next enemy Threshold instead of -20%"
            };
        }
    }
}

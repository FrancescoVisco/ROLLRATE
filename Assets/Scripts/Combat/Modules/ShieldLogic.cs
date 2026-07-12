namespace Rollrate.Combat.Modules
{
    /// <summary>
    /// Shield (Stability) - Static: a chosen slot ignores enemy Inhibition
    /// (here: this slot, the one Shield is placed in).
    /// Frequency (High DCD): the entire board ignores enemy Inhibition.
    /// </summary>
    public class ShieldLogic : ModuleLogicBase
    {
        public override ModuleResult ApplyStaticEffect(int dieValue, CombatContext ctx)
        {
            return new ModuleResult
            {
                IgnoresInhibition = true,
                DebugLog = "Shield: this slot ignores enemy Inhibition"
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
                IgnoresInhibitionBoardWide = true,
                DebugLog = "Shield Frequency: the entire board ignores enemy Inhibition"
            };
        }
    }
}

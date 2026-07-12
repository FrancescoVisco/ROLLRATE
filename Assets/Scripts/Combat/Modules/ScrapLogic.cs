namespace Rollrate.Combat.Modules
{
    /// <summary>
    /// Scrap (Echo) - Static: gain Scrap equal to the placed die's value.
    /// Frequency (Odd): the next module or die bought at the Shop gets a
    /// 25% discount (the side effect is handled outside combat; here we
    /// only report the raw turn result).
    /// </summary>
    public class ScrapLogic : ModuleLogicBase
    {
        public override ModuleResult ApplyStaticEffect(int dieValue, CombatContext ctx)
        {
            return new ModuleResult
            {
                ScrapGained = dieValue,
                DebugLog = $"Scrap: +{dieValue} Scrap"
            };
        }

        public override bool IsFrequencyConditionMet(CombatContext ctx)
        {
            return !ctx.CoreIsEven; // Odd
        }

        public override ModuleResult ApplyFrequencyEffect(int dieValue, CombatContext ctx)
        {
            // The discount is written to a persistent flag on ShopState;
            // here we only return the "raw" combat-turn result.
            return new ModuleResult
            {
                DebugLog = "Scrap Frequency: 25% discount on next purchase (flag ShopState.NextPurchaseDiscount = 0.25f)"
            };
        }
    }
}

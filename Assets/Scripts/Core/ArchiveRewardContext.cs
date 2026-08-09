namespace Rollrate.Core
{
    /// <summary>
    /// Static bridge (same pattern as CombatNodeContext) carrying the
    /// Archive's "Acquisto Gratis" / "Fusione Gratuita" rewards across
    /// the scene transition into Shop / Furnace: each consumed there for
    /// exactly one free action.
    /// </summary>
    public static class ArchiveRewardContext
    {
        public static bool FreeDiePurchasePending;
        public static bool FreeFusionPending;
    }
}

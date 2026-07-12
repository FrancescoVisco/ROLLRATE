namespace Rollrate.Combat.Enemies
{
    /// <summary>
    /// Cantor (Grade III Base, D12) - Discord: if you achieve Resonance,
    /// the Threshold increases by +4 this turn.
    ///
    /// Simplification: interpreted here as Full Resonance specifically
    /// (not Legame), since Full Resonance already ignores the Threshold
    /// entirely via Automatic Critical Success - so in practice this only
    /// matters if that interpretation changes later.
    /// </summary>
    public class CantorAbility : EnemyAbilityBase
    {
        public override int ApplyThresholdModifier(int baseThreshold, EnemyAbilityContext ctx)
        {
            return ctx.Ctx.FullResonanceActive ? baseThreshold + 4 : baseThreshold;
        }
    }
}

using Rollrate.Combat;

namespace Rollrate.Combat.Enemies
{
    /// <summary>
    /// Cantor (Grade III Base, D12) - Discord: if Legame (partial
    /// Resonance) is active this turn, the Threshold increases by +4.
    ///
    /// Deliberately NOT triggered by Full Resonance: that state already
    /// grants Automatic Critical Success, bypassing the Threshold entirely
    /// - a +4 on top of an already-irrelevant Threshold would never do
    /// anything. Legame only grants Inhibition-ignoring on the involved
    /// slots, NOT a Threshold bypass, so the Threshold still matters there
    /// - this is where Cantor's punishment actually has an effect.
    /// </summary>
    public class CantorAbility : EnemyAbilityBase
    {
        public override int ApplyThresholdModifier(int baseThreshold, EnemyAbilityContext ctx)
        {
            if (ctx.Ctx.FullResonanceActive) return baseThreshold; // already bypassed - would never matter

            bool legameActive = ctx.PlacedValues != null && ResonanceDetector.DetectLegameSlots(ctx.PlacedValues).Count > 0;
            return legameActive ? baseThreshold + 4 : baseThreshold;
        }
    }
}

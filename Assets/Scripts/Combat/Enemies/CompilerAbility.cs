namespace Rollrate.Combat.Enemies
{
    /// <summary>
    /// Compiler (Grade I Elite, D6) - Lockdown: if you place a die in the
    /// FLOW slot, the enemy Threshold increases by +2.
    ///
    /// Was +4 - lowered after Run Simulator data showed Compiler causing a
    /// disproportionate share of Grade I deaths (2584/4000, vs 1154 for
    /// Fragment) once "Uscita Forzata" made Elite encounters guaranteed
    /// once per Page instead of random. At +4 on top of already-higher
    /// base stats (12 HP/12 Threshold vs Fragment's 8/10), a starting D4-
    /// only kit needed near-maximum rolls just to match Fragment's
    /// baseline. +2 keeps the same qualitative distinction from Fragment
    /// (an active, avoidable choice vs a passive, random one) without
    /// stacking severity on top of already-higher base stats.
    /// </summary>
    public class CompilerAbility : EnemyAbilityBase
    {
        public override int ApplyThresholdModifier(int baseThreshold, EnemyAbilityContext ctx)
        {
            return ctx.Ctx.FlowSlotOccupied ? baseThreshold + 2 : baseThreshold;
        }
    }
}

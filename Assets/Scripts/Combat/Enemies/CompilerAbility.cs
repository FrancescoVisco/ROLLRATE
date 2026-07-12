namespace Rollrate.Combat.Enemies
{
    /// <summary>
    /// Compiler (Grade I Elite, D6) - Lockdown: if you place a die in the
    /// FLOW slot, the enemy Threshold increases by +4.
    /// </summary>
    public class CompilerAbility : EnemyAbilityBase
    {
        public override int ApplyThresholdModifier(int baseThreshold, EnemyAbilityContext ctx)
        {
            return ctx.Ctx.FlowSlotOccupied ? baseThreshold + 4 : baseThreshold;
        }
    }
}

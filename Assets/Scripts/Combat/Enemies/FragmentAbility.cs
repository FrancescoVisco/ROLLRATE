namespace Rollrate.Combat.Enemies
{
    /// <summary>
    /// Fragment (Grade I Base, D4) - Static: if the Inhibited value is 1-2,
    /// the Threshold increases by +3.
    /// </summary>
    public class FragmentAbility : EnemyAbilityBase
    {
        public override int ApplyThresholdModifier(int baseThreshold, EnemyAbilityContext ctx)
        {
            bool inhibitedIsLow = ctx.Ctx.InhibitedValue == 1 || ctx.Ctx.InhibitedValue == 2;
            return inhibitedIsLow ? baseThreshold + 3 : baseThreshold;
        }
    }
}

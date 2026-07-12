namespace Rollrate.Combat.Enemies
{
    /// <summary>
    /// Sentinel (Grade II Elite, D10) - Jammer: if your Core Die shows an
    /// Even value, the Sentinel also inhibits the value 1 this turn.
    /// </summary>
    public class SentinelAbility : EnemyAbilityBase
    {
        public override int ApplyThresholdModifier(int baseThreshold, EnemyAbilityContext ctx) => baseThreshold;

        public override int GetExtraInhibitedValue(EnemyAbilityContext ctx)
        {
            return ctx.Ctx.CoreIsEven ? 1 : -1;
        }
    }
}

namespace Rollrate.Combat.Enemies
{
    /// <summary>
    /// Placeholder ability that applies no Threshold modifier and no other
    /// special behavior. All other hooks use EnemyAbilityBase's defaults.
    /// </summary>
    public class NoOpAbility : EnemyAbilityBase
    {
        public override int ApplyThresholdModifier(int baseThreshold, EnemyAbilityContext ctx)
        {
            return baseThreshold;
        }
    }
}

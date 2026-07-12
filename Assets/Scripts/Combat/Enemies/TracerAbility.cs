namespace Rollrate.Combat.Enemies
{
    /// <summary>
    /// Tracer (Grade II Base, D8) - Pressure: if you end the turn without
    /// exceeding the Threshold, you take 1 extra direct HP damage (on top
    /// of the normal failure damage).
    /// </summary>
    public class TracerAbility : EnemyAbilityBase
    {
        public override int ApplyThresholdModifier(int baseThreshold, EnemyAbilityContext ctx) => baseThreshold;

        public override int GetBonusDamageOnFailure(EnemyAbilityContext ctx) => 1;
    }
}

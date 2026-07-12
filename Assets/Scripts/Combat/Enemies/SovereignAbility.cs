namespace Rollrate.Combat.Enemies
{
    /// <summary>
    /// Sovereign (Grade V Guardian) - Delete: at the end of every turn,
    /// looks at the Core Die's final value. From then on, any pool die that
    /// rolls that exact value is immediately destroyed (permanently removed
    /// from the pool) - see DiceRoller, which checks EnemyController's
    /// PersistentDestroyValue while rolling the pool each turn.
    /// </summary>
    public class SovereignAbility : EnemyAbilityBase
    {
        public override int ApplyThresholdModifier(int baseThreshold, EnemyAbilityContext ctx) => baseThreshold;

        public override void OnTurnEnd(EnemyController enemy, EnemyAbilityContext ctx)
        {
            enemy.SetPersistentDestroyValue(ctx.Ctx.CoreValue);
        }
    }
}

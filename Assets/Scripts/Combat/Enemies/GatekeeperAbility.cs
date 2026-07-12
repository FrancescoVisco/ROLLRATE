namespace Rollrate.Combat.Enemies
{
    /// <summary>
    /// Gatekeeper (Grade I Guardian) - Clockwork: at the start of every
    /// turn, the Threshold increases permanently by +2.
    /// </summary>
    public class GatekeeperAbility : EnemyAbilityBase
    {
        public override void OnTurnStart(EnemyController enemy)
        {
            enemy.AddPermanentThresholdBonus(2);
        }

        public override int ApplyThresholdModifier(int baseThreshold, EnemyAbilityContext ctx)
        {
            // The +2/turn growth is already folded into baseThreshold via
            // AddPermanentThresholdBonus in OnTurnStart - nothing more to add here.
            return baseThreshold;
        }
    }
}

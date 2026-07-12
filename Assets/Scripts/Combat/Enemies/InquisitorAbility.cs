namespace Rollrate.Combat.Enemies
{
    /// <summary>
    /// Inquisitor (Grade IV Base, D14) - Tax: the Threshold increases by
    /// +3 for every die in the player's pool with fewer than 12 faces.
    /// </summary>
    public class InquisitorAbility : EnemyAbilityBase
    {
        public override int ApplyThresholdModifier(int baseThreshold, EnemyAbilityContext ctx)
        {
            if (ctx.State == null || ctx.State.dicePool == null) return baseThreshold;

            int countBelow12 = 0;
            foreach (var die in ctx.State.dicePool)
            {
                if (die != null && die.faces < 12) countBelow12++;
            }

            return baseThreshold + (countBelow12 * 3);
        }
    }
}

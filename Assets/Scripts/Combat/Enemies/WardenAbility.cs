namespace Rollrate.Combat.Enemies
{
    /// <summary>
    /// Warden (Grade IV Elite, D20) - Stasis: every time you use the FLOW
    /// slot to reroll (Second Chance), you take 1 direct HP damage.
    /// Triggered once per turn a reroll actually happens, not once per die.
    /// </summary>
    public class WardenAbility : EnemyAbilityBase
    {
        public override int ApplyThresholdModifier(int baseThreshold, EnemyAbilityContext ctx) => baseThreshold;

        public override int OnFlowRerollUsed(EnemyAbilityContext ctx) => 1;
    }
}

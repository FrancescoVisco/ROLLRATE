namespace Rollrate.Combat.Enemies
{
    /// <summary>
    /// Warden (Grade IV Elite, D20) - Stasis: every time you use the FLOW
    /// slot to reroll, you take 1 direct HP damage.
    ///
    /// NOT YET FUNCTIONAL: Second Chance grants a reroll count
    /// (DiceToReroll) but the actual interactive reroll mechanic isn't
    /// wired into the UI/CombatController yet, so there's no real "reroll
    /// used" event to hook this to. Revisit once Second Chance's reroll
    /// is implemented end-to-end.
    /// </summary>
    public class WardenAbility : EnemyAbilityBase
    {
        public override int ApplyThresholdModifier(int baseThreshold, EnemyAbilityContext ctx) => baseThreshold;
    }
}

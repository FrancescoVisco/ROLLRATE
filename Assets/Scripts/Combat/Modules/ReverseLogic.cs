namespace Rollrate.Combat.Modules
{
    /// <summary>
    /// Reverse (Flow) - Static: choose a die - flip its value
    /// (e.g. on a D6: 1&lt;-&gt;6, 2&lt;-&gt;5).
    /// Frequency (Low DCD): Total Inversion - flip the dice of all occupied
    /// slots on the board.
    ///
    /// Note: "choose a die" / "all occupied slots" target dice beyond this
    /// module's own slot - the actual flip logic needs access to the full
    /// board state, which isn't wired up yet. Reported via DebugLog for now.
    /// </summary>
    public class ReverseLogic : ModuleLogicBase
    {
        public override ModuleResult ApplyStaticEffect(int dieValue, CombatContext ctx)
        {
            return new ModuleResult
            {
                DebugLog = "Reverse: TODO wire up target-die selection - flip a chosen die's value"
            };
        }

        public override bool IsFrequencyConditionMet(CombatContext ctx)
        {
            return ctx.CoreRange == Data.ValueRange.Low;
        }

        public override ModuleResult ApplyFrequencyEffect(int dieValue, CombatContext ctx)
        {
            return new ModuleResult
            {
                DebugLog = "Reverse Frequency: TODO wire up board-wide flip of all occupied slots"
            };
        }
    }
}

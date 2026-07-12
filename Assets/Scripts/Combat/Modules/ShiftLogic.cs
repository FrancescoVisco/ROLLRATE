namespace Rollrate.Combat.Modules
{
    /// <summary>
    /// Shift (Flow) - Static: choose a die - increase or decrease its value by 1.
    /// Frequency (Odd): you can modify a die's value up to a maximum equal
    /// to the Core Die.
    ///
    /// Note: "choose a die" targets another die on the board - the actual
    /// die-picking UI/logic doesn't exist yet, so this reports intent via
    /// DebugLog for now.
    /// </summary>
    public class ShiftLogic : ModuleLogicBase
    {
        public override ModuleResult ApplyStaticEffect(int dieValue, CombatContext ctx)
        {
            return new ModuleResult
            {
                DebugLog = "Shift: TODO wire up target-die selection - +/-1 to a chosen die's value"
            };
        }

        public override bool IsFrequencyConditionMet(CombatContext ctx)
        {
            return !ctx.CoreIsEven; // Odd
        }

        public override ModuleResult ApplyFrequencyEffect(int dieValue, CombatContext ctx)
        {
            return new ModuleResult
            {
                DebugLog = $"Shift Frequency: TODO wire up target-die selection - can modify up to Core value ({ctx.CoreValue})"
            };
        }
    }
}

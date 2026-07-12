namespace Rollrate.Combat.Modules
{
    /// <summary>
    /// Mirror (Flow) - Static: choose a die - its value becomes identical
    /// to the Core Die's value.
    /// Frequency (Even): the die placed in Mirror returns to the pool for
    /// the next turn instead of being discarded.
    ///
    /// Note: "choose a die" targets another die on the board - the actual
    /// die-picking UI/logic doesn't exist yet, so this reports intent via
    /// DebugLog for now. The die placed in the Flow slot itself is what
    /// ReturnDieToPoolNextTurn refers to.
    /// </summary>
    public class MirrorLogic : ModuleLogicBase
    {
        public override ModuleResult ApplyStaticEffect(int dieValue, CombatContext ctx)
        {
            return new ModuleResult
            {
                DebugLog = $"Mirror: TODO wire up target-die selection - chosen die's value becomes Core's value ({ctx.CoreValue})"
            };
        }

        public override bool IsFrequencyConditionMet(CombatContext ctx)
        {
            return ctx.CoreIsEven;
        }

        public override ModuleResult ApplyFrequencyEffect(int dieValue, CombatContext ctx)
        {
            return new ModuleResult
            {
                ReturnDieToPoolNextTurn = true,
                DebugLog = "Mirror Frequency: the die placed here returns to the pool next turn"
            };
        }
    }
}

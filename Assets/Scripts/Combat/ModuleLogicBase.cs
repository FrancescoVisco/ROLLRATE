namespace Rollrate.Combat
{
    /// <summary>
    /// Base class for a module's logic. One subclass per ModuleId.
    /// The Frequency Effect is applied ONLY if the required condition
    /// (Even/Odd/Low-DCD/High-DCD) is met AND the module isn't inhibited -
    /// that check is done by the ModuleResolver, not by the module itself.
    /// </summary>
    public abstract class ModuleLogicBase
    {
        /// <summary>Always applied, regardless of Frequency/Inhibition.</summary>
        public abstract ModuleResult ApplyStaticEffect(int placedDieValue, CombatContext ctx);

        /// <summary>Applied only if the module's Frequency condition is satisfied.</summary>
        public abstract ModuleResult ApplyFrequencyEffect(int placedDieValue, CombatContext ctx);

        /// <summary>
        /// Condition required to trigger the Frequency Effect (used by the
        /// Resolver to decide whether to call ApplyFrequencyEffect).
        /// </summary>
        public abstract bool IsFrequencyConditionMet(CombatContext ctx);
    }
}

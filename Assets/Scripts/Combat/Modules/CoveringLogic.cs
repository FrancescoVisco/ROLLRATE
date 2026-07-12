namespace Rollrate.Combat.Modules
{
    /// <summary>
    /// Covering (Stability) - Static: reduces incoming damage by 2 (minimum 1).
    /// Frequency (Even): recover 1 HP (up to max value).
    ///
    /// Note: the "minimum 1" clamp is applied where damage is actually
    /// subtracted (CombatController/damage resolution), not here - this
    /// module only reports how much reduction it grants.
    /// </summary>
    public class CoveringLogic : ModuleLogicBase
    {
        public override ModuleResult ApplyStaticEffect(int dieValue, CombatContext ctx)
        {
            return new ModuleResult
            {
                DamageReduction = 2,
                DebugLog = "Covering: -2 incoming damage (min 1, enforced at damage resolution)"
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
                HpRecovered = 1,
                DebugLog = "Covering Frequency: +1 HP recovered (Core Even)"
            };
        }
    }
}

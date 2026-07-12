namespace Rollrate.Combat.Modules
{
    /// <summary>
    /// Changeover (Stability) - Static: every point exceeding the Threshold
    /// generates 1 Charge (10 Charges = +1 Die).
    /// Frequency (Odd): the conversion is doubled (1 excess point = 2 Charges).
    ///
    /// Note: relies on CombatContext.PointsExceedingThreshold, which the
    /// CombatController must compute in a second pass after the Power slot's
    /// total is known (this module's own die placement doesn't determine
    /// that number).
    /// </summary>
    public class ChangeoverLogic : ModuleLogicBase
    {
        public override ModuleResult ApplyStaticEffect(int dieValue, CombatContext ctx)
        {
            int excess = UnityEngine.Mathf.Max(0, ctx.PointsExceedingThreshold);
            return new ModuleResult
            {
                ChargesGenerated = excess,
                DebugLog = $"Changeover: {excess} Charges from {excess} excess points"
            };
        }

        public override bool IsFrequencyConditionMet(CombatContext ctx)
        {
            return !ctx.CoreIsEven; // Odd
        }

        public override ModuleResult ApplyFrequencyEffect(int dieValue, CombatContext ctx)
        {
            int excess = UnityEngine.Mathf.Max(0, ctx.PointsExceedingThreshold);
            return new ModuleResult
            {
                ChargesGenerated = excess, // doubles the static result when combined
                DebugLog = $"Changeover Frequency: conversion doubled, +{excess} more Charges"
            };
        }
    }
}

using Rollrate.Combat;

namespace Rollrate.Combat.Enemies
{
    /// <summary>
    /// Eraser (Grade II Guardian) - Backlash: every module Frequency that
    /// triggers this turn raises the Threshold by +3.
    ///
    /// Simplification: since Threshold is needed before Power is resolved
    /// (and Frequency triggers are normally only known slot-by-slot during
    /// resolution), this approximates the count by checking, for every
    /// installed module with a die placed, whether its Frequency condition
    /// would be met given the Core roll - without factoring in Inhibition
    /// or Resonance overrides on that check. Close enough for a working
    /// version; revisit if exact per-trigger timing matters later.
    /// </summary>
    public class EraserAbility : EnemyAbilityBase
    {
        public override int ApplyThresholdModifier(int baseThreshold, EnemyAbilityContext ctx)
        {
            int triggeringModules = 0;

            if (ctx.InstalledModules != null && ctx.PlacedValues != null)
            {
                foreach (var kvp in ctx.InstalledModules)
                {
                    if (kvp.Value == null || !ctx.PlacedValues.ContainsKey(kvp.Key)) continue;

                    var logic = ModuleLogicRegistry.Get(kvp.Value.id);
                    if (logic.IsFrequencyConditionMet(ctx.Ctx))
                    {
                        triggeringModules++;
                    }
                }
            }

            return baseThreshold + (triggeringModules * 3);
        }
    }
}

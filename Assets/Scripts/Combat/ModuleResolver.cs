using UnityEngine;
using Rollrate.Data;

namespace Rollrate.Combat
{
    /// <summary>
    /// Resolves the result of a single slot during CHECK: applies the Static
    /// Effect always, then applies the Frequency Effect only if the module's
    /// condition (Even/Odd/Low-DCD/High-DCD) is met on the Core Die AND the
    /// placed die's value isn't the enemy's Inhibited value.
    ///
    /// Resonance (Legame/Totale) can override the Inhibition check - see the
    /// ignoreInhibition parameter, to be wired up once ResonanceDetector exists.
    /// </summary>
    public static class ModuleResolver
    {
        /// <summary>
        /// Resolves one slot. Returns an empty ModuleResult if there's no
        /// module installed or no die placed.
        /// </summary>
        /// <param name="ignoreInhibition">
        /// True if Resonance (Legame or Totale) makes this slot ignore
        /// enemy Inhibition regardless of the placed die's value.
        /// </param>
        /// <param name="forceFrequency">
        /// True during Full Resonance's "Sovraccarico Moduli": applies the
        /// Frequency Effect even if the Core condition isn't met.
        /// </param>
        public static ModuleResult ResolveSlot(
            ModuleData installedModule,
            int? placedDieValue,
            CombatContext ctx,
            bool ignoreInhibition = false,
            bool forceFrequency = false)
        {
            if (installedModule == null || placedDieValue == null)
            {
                return new ModuleResult { DebugLog = "No module or no die placed - slot contributes nothing." };
            }

            int dieValue = placedDieValue.Value;
            ModuleLogicBase logic = ModuleLogicRegistry.Get(installedModule.id);

            // Static Effect always applies.
            ModuleResult result = logic.ApplyStaticEffect(dieValue, ctx);

            // Frequency Effect applies if the Core condition is met AND the
            // die isn't inhibited (unless Resonance overrides either check).
            bool isInhibited = !ignoreInhibition && IsValueInhibited(dieValue, ctx);
            bool conditionMet = forceFrequency || logic.IsFrequencyConditionMet(ctx);

            if (conditionMet && !isInhibited)
            {
                ModuleResult freqResult = logic.ApplyFrequencyEffect(dieValue, ctx);
                result = Combine(result, freqResult);
                result.FrequencyTriggered = true;
            }

            return result;
        }

        /// <summary>
        /// True if the given value is inhibited this turn under any of the
        /// three inhibition sources: the base Inhibitor Die roll, an enemy
        /// ability's extra inhibited value (e.g. Sentinel), or a permanently
        /// inhibited value accumulated over the fight (e.g. Judge). Used
        /// both for gating Frequency Effects and for the die's on-screen
        /// "inhibited" visual indicator.
        /// </summary>
        public static bool IsValueInhibited(int value, CombatContext ctx)
        {
            bool matchesBaseInhibition = value == ctx.InhibitedValue;
            bool matchesExtraInhibition = ctx.ExtraInhibitedValue >= 0 && value == ctx.ExtraInhibitedValue;
            bool matchesPermanentInhibition = ctx.PermanentlyInhibitedValues != null && ctx.PermanentlyInhibitedValues.Contains(value);
            return matchesBaseInhibition || matchesExtraInhibition || matchesPermanentInhibition;
        }

        /// <summary>Merges a Static result with a Frequency result into one total.</summary>
        private static ModuleResult Combine(ModuleResult baseResult, ModuleResult bonusResult)
        {
            return new ModuleResult
            {
                ValueBonus = baseResult.ValueBonus + bonusResult.ValueBonus,
                HpRecovered = baseResult.HpRecovered + bonusResult.HpRecovered,
                ScrapGained = baseResult.ScrapGained + bonusResult.ScrapGained,
                IgnoresInhibition = baseResult.IgnoresInhibition || bonusResult.IgnoresInhibition,
                IgnoresInhibitionBoardWide = baseResult.IgnoresInhibitionBoardWide || bonusResult.IgnoresInhibitionBoardWide,
                DamageReduction = baseResult.DamageReduction + bonusResult.DamageReduction,
                NextThresholdReductionPercent = Mathf.Max(baseResult.NextThresholdReductionPercent, bonusResult.NextThresholdReductionPercent),
                ChargesGenerated = baseResult.ChargesGenerated + bonusResult.ChargesGenerated,
                ReturnDieToPoolNextTurn = baseResult.ReturnDieToPoolNextTurn || bonusResult.ReturnDieToPoolNextTurn,
                DiceToReroll = Mathf.Max(baseResult.DiceToReroll, bonusResult.DiceToReroll),
                RerollKeepsBetterResult = baseResult.RerollKeepsBetterResult || bonusResult.RerollKeepsBetterResult,
                TempDiceToAddNextTurn = Mathf.Max(baseResult.TempDiceToAddNextTurn, bonusResult.TempDiceToAddNextTurn),
                AddedDiceCopyCoreType = baseResult.AddedDiceCopyCoreType || bonusResult.AddedDiceCopyCoreType,
                NextShopDiscountPercent = Mathf.Max(baseResult.NextShopDiscountPercent, bonusResult.NextShopDiscountPercent),
                NextTurnPowerBonus = baseResult.NextTurnPowerBonus + bonusResult.NextTurnPowerBonus,
                // Frequency's target value (if it sets one) takes priority over Static's -
                // for Mirror/Shift/Reverse, Frequency represents an upgraded version of
                // the same single action, not a stacking bonus.
                NewTargetValue = bonusResult.NewTargetValue ?? baseResult.NewTargetValue,
                DebugLog = baseResult.DebugLog + " | " + bonusResult.DebugLog
            };
        }
    }
}

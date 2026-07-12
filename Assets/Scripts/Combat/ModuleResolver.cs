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
        public static ModuleResult ResolveSlot(
            ModuleData installedModule,
            int? placedDieValue,
            CombatContext ctx,
            bool ignoreInhibition = false)
        {
            if (installedModule == null || placedDieValue == null)
            {
                return new ModuleResult { DebugLog = "No module or no die placed - slot contributes nothing." };
            }

            int dieValue = placedDieValue.Value;
            ModuleLogicBase logic = ModuleLogicRegistry.Get(installedModule.id);

            // Static Effect always applies.
            ModuleResult result = logic.ApplyStaticEffect(dieValue, ctx);

            // Frequency Effect applies only if the Core condition is met AND
            // the die isn't inhibited this turn (unless Resonance overrides it).
            bool isInhibited = !ignoreInhibition && dieValue == ctx.InhibitedValue;
            bool conditionMet = logic.IsFrequencyConditionMet(ctx);

            if (conditionMet && !isInhibited)
            {
                ModuleResult freqResult = logic.ApplyFrequencyEffect(dieValue, ctx);
                result = Combine(result, freqResult);
            }

            return result;
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
                DebugLog = baseResult.DebugLog + " | " + bonusResult.DebugLog
            };
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using Rollrate.Data;

namespace Rollrate.Combat
{
    /// <summary>
    /// Vibrazione 3.0 (design doc Section 5): every placed die gets a NET
    /// MULTIPLIER, applied to its own value before any module sums it -
    /// there is no more board-wide "hand" (Coppia/Tris/Poker/Cinquina) and
    /// no more automatic-victory trigger. The net multiplier combines two
    /// independent things:
    ///
    /// 1. Vibrazione: how many of 3 axes (Parità, Grandezza, Valore) this
    ///    die shares with the Dado Core - 0 axes = x1, 1 = x1.5, 2 = x2,
    ///    3 = x3.
    /// 2. Tiro Massimo/Minimo: if the die shows its own highest or lowest
    ///    possible face, it gets an additional flat bonus/malus, scaled by
    ///    the die's Grade (I-V) - summed onto the Vibrazione multiplier,
    ///    not multiplied by it.
    ///
    /// Inhibited dice are locked to a flat x1 net multiplier by the CALLER
    /// (see CombatController.HighlightVibratingDice/ComputeEffectiveValues) -
    /// this class's own methods don't take inhibition into account
    /// directly except where noted, keeping "inhibited = neutral" simple.
    /// </summary>
    public static class ResonanceDetector
    {
        /// <summary>Max/Min bonus magnitude by die Grade (index 1-5 = Grade I-V; index 0 unused).</summary>
        private static readonly float[] MaxMinBonusByGrade = { 0f, 0.3f, 0.5f, 0.7f, 0.9f, 1.1f };

        /// <summary>
        /// How many of the 3 axes (Parità, Grandezza, Valore) this single
        /// die shares with the Core - 0 to 3. Exposed publicly for any UI
        /// that wants to explain why a die does or doesn't get a
        /// Vibrazione bonus.
        /// </summary>
        public static int GetAxesShared(int value, DieData die, int coreValue, bool coreIsEven, ValueRange coreRange)
        {
            if (die == null) return 0;

            int axesShared = 0;
            if (die.IsEven(value) == coreIsEven) axesShared++;

            ValueRange dieRange = die.GetRange(value);
            if (dieRange != ValueRange.DeadZone && dieRange == coreRange) axesShared++;

            if (value == coreValue) axesShared++;

            return axesShared;
        }

        /// <summary>Maps a shared-axes count (0-3) to its Vibrazione multiplier (x1/x1.5/x2/x3).</summary>
        public static float GetVibrationMultiplier(int axesShared)
        {
            switch (axesShared)
            {
                case 3: return 3f;
                case 2: return 2f;
                case 1: return 1.5f;
                default: return 1f;
            }
        }

        /// <summary>
        /// The flat Max/Min bonus/malus for this die's CURRENT value: +bonus
        /// if it shows its highest possible face, -bonus if its lowest
        /// (i.e. 1), 0 otherwise. Magnitude scales with the die's Grade.
        /// </summary>
        public static float GetMaxMinBonus(int value, DieData die)
        {
            if (die == null) return 0f;

            int grade = Mathf.Clamp(die.grade, 1, 5);
            float magnitude = MaxMinBonusByGrade[grade];

            if (value >= die.faces) return magnitude;   // max roll
            if (value <= 1) return -magnitude;           // min roll
            return 0f;
        }

        /// <summary>
        /// The net multiplier for a single placed die (assuming it's NOT
        /// inhibited - the caller locks inhibited dice to x1 directly) -
        /// Vibrazione (axes) bonus SUMMED with the Max/Min bonus/malus.
        /// This is the one number shown on the die and applied to its
        /// value wherever a module sums it.
        /// </summary>
        public static float GetNetMultiplier(int value, DieData die, int coreValue, bool coreIsEven, ValueRange coreRange)
        {
            float vibration = GetVibrationMultiplier(GetAxesShared(value, die, coreValue, coreIsEven, coreRange));
            float maxMinBonus = GetMaxMinBonus(value, die);
            return vibration + maxMinBonus;
        }

        /// <summary>Sums a list of already-computed effective (multiplier-scaled) die values. Null-safe.</summary>
        public static float EffectiveSum(List<float> effectiveValues)
        {
            if (effectiveValues == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < effectiveValues.Count; i++) sum += effectiveValues[i];
            return sum;
        }

        /// <summary>True if this die's effective value is above its raw value (i.e. its net multiplier is above x1 - a bonus, not a malus or neutral).</summary>
        public static bool IsBonusMultiplier(int rawValue, float effectiveValue)
        {
            if (rawValue <= 0) return false;
            return effectiveValue > rawValue;
        }
    }
}

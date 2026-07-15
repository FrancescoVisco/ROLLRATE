using UnityEngine;

namespace Rollrate.Data
{
    public enum ValueRange
    {
        Low,
        DeadZone,
        High
    }

    [CreateAssetMenu(fileName = "SO_Die_", menuName = "Rollrate/Die")]
    public class DieData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName; // e.g. "D4", "D6", "D20"
        public int faces;
        [Tooltip("Which Grade (Echelon, 1-5) this die belongs to - used for Dismantle refund value, independent of the player's current Grade. D4/D6=1, D8/D10=2, D12=3, D14/D16=4, D20=5.")]
        public int grade = 1;

        [Header("Ranges (from the design's Dead Zone table)")]
        [Tooltip("Maximum value included in the Low range")]
        public int lowMax;
        [Tooltip("Minimum value included in the High range")]
        public int highMin;

        [Header("Visual")]
        public Sprite dieSprite;
        public Color casteColor = Color.white;

        [Header("Shop - Evolution Chain")]
        [Tooltip("The next die this evolves into at the Shop (e.g. D4's Next Tier is D6). Leave empty for D20 - it's already the maximum.")]
        public DieData nextTier;

        /// <summary>
        /// Returns which range a rolled value falls into.
        /// </summary>
        public ValueRange GetRange(int rolledValue)
        {
            if (rolledValue <= lowMax) return ValueRange.Low;
            if (rolledValue >= highMin) return ValueRange.High;
            return ValueRange.DeadZone;
        }

        /// <summary>
        /// True if the rolled value is even.
        /// </summary>
        public bool IsEven(int rolledValue) => rolledValue % 2 == 0;

        /// <summary>
        /// Editor-only helper: rough auto-calculation of ranges using
        /// Dead Zone = ceil(faces / 4). Use only as a starting point -
        /// the final ranges were hand-tuned in the design doc and should
        /// be entered manually for correctness.
        /// </summary>
        [ContextMenu("Auto-calculate ranges (rough estimate)")]
        private void AutoCalculateRanges()
        {
            int deadZone = Mathf.CeilToInt(faces / 4f);
            int remaining = faces - deadZone;
            int lowCount = Mathf.CeilToInt(remaining / 2f);
            lowMax = lowCount;
            highMin = faces - (remaining - lowCount) + 1;
        }
    }
}

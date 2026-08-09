using UnityEngine;

namespace Rollrate.Data
{
    /// <summary>
    /// Canonical color per DieType, per the design doc (Section 5):
    /// Power = red, Stability = blue, Flow = purple, Echo = green.
    /// Centralized here so every UI element (die visuals, tooltips,
    /// icons) reads the same source instead of duplicating hex values.
    /// Colors are code-owned constants, not Inspector fields - nothing
    /// to misconfigure per prefab (same reasoning as the Vibrazione
    /// tint colors on DraggableDie).
    /// </summary>
    public static class DieTypeColors
    {
        public static readonly Color Power = new Color(0.85f, 0.2f, 0.2f);      // red
        public static readonly Color Stability = new Color(0.25f, 0.45f, 0.9f); // blue
        public static readonly Color Flow = new Color(0.45f, 0.2f, 0.85f);      // purple - shifted less red / more blue than before, so it doesn't read as reddish
        public static readonly Color Echo = new Color(0.25f, 0.75f, 0.35f);     // green

        public static Color For(DieType type)
        {
            switch (type)
            {
                case DieType.Power: return Power;
                case DieType.Stability: return Stability;
                case DieType.Flow: return Flow;
                case DieType.Echo: return Echo;
                default: return Color.white;
            }
        }
    }
}

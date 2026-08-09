using UnityEngine;

namespace Rollrate.Data
{
    /// <summary>
    /// One of the 16 Effects (design doc Section 5, "Effetti"). Replaces
    /// ModuleData entirely - an Effect is attached directly to a specific
    /// owned die (see DieInstance), not to a Slot/container. A die can
    /// carry several Effects at once, up to the Shop's per-Page cap (3)
    /// or higher via Fusion (up to 4, the absolute maximum).
    /// </summary>
    [CreateAssetMenu(fileName = "SO_Effect_", menuName = "Rollrate/Effect")]
    public class EffectData : ScriptableObject
    {
        [Header("Identity")]
        public EffectId id;
        [Tooltip("Which DieType this Effect can be applied to - a Power Effect can only ever go on a Power die.")]
        public DieType dieType;
        [Tooltip("Grade I-V. Grade I has no Effects (dice sold at Grade I are always base) - see the design doc's Grado degli Effetti table.")]
        public int grade = 2;
        public string displayName;

        [Header("Description (for UI/tooltips)")]
        [TextArea] public string effectDescription;

        [Header("Visual")]
        public Sprite icon;
    }
}

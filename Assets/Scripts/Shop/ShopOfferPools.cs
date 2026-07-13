using UnityEngine;
using Rollrate.Data;

namespace Rollrate.Shop
{
    /// <summary>
    /// The set of dice and modules offerable at a single Grade.
    /// </summary>
    [System.Serializable]
    public class GradeOfferPool
    {
        public DieData[] dice;
        public ModuleData[] modules;
    }

    /// <summary>
    /// The Shop's offer pools, one set per Grade (I-V). The Merchant Node
    /// only offers items from the pool matching the player's CURRENT Grade
    /// (state.currentEchelon) - not a cumulative pool of everything unlocked
    /// so far. This mirrors the design's caste system, where each Grade's
    /// environment (Lowborn D4-D6, Enforcers D8-D10, Nobles D12, Arbiters
    /// D14-D16, Sovereigns D20) reflects what's actually around you.
    /// </summary>
    [CreateAssetMenu(fileName = "ShopOfferPools", menuName = "Rollrate/Shop Offer Pools")]
    public class ShopOfferPools : ScriptableObject
    {
        [Header("Grade I - Lowborn (D4-D6)")]
        public GradeOfferPool gradeI;
        [Header("Grade II - Enforcers (D8-D10)")]
        public GradeOfferPool gradeII;
        [Header("Grade III - Nobles (D12)")]
        public GradeOfferPool gradeIII;
        [Header("Grade IV - Arbiters (D14-D16)")]
        public GradeOfferPool gradeIV;
        [Header("Grade V - Sovereigns (D20)")]
        public GradeOfferPool gradeV;

        /// <summary>Returns the offer pool matching the given Echelon (1-5), clamped.</summary>
        public GradeOfferPool GetForGrade(int currentEchelon)
        {
            switch (Mathf.Clamp(currentEchelon, 1, 5))
            {
                case 1: return gradeI;
                case 2: return gradeII;
                case 3: return gradeIII;
                case 4: return gradeIV;
                default: return gradeV;
            }
        }
    }
}

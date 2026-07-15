using UnityEngine;
using Rollrate.Archive;

namespace Rollrate.Data
{
    /// <summary>
    /// Per-Grade numbers for the 3 Archive Tests (index 0 = Grade I ... 4 = Grade V).
    /// Thresholds/rewards from the design doc; Ambition's Scrap reward and
    /// Tribute's die effects don't have a doc-specified number - flagged
    /// with [Tooltip] as adjustable defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "ArchiveTestTable", menuName = "Rollrate/Archive Test Table")]
    public class ArchiveTestTable : ScriptableObject
    {
        [Header("Test di Risonanza - roll Core Die")]
        public int[] resonanceThreshold = { 3, 5, 7, 9, 11 };
        public int[] resonanceRewardScrap = { 15, 25, 35, 45, 55 };
        public int[] resonancePenaltyScrap = { 10, 15, 20, 25, 30 };

        [Header("Test di Tributo - roll entire Pool, sum")]
        public int[] tributeThreshold = { 12, 18, 26, 36, 48 };
        // Reward/penalty are a random owned die evolved/destroyed - no number needed here.

        [Header("Test di Ambizione - roll Core + best Pool die")]
        public int[] ambitionThreshold = { 10, 15, 22, 30, 40 };
        [Tooltip("Not specified precisely in the design doc ('enorme quantità') - adjust freely.")]
        public int[] ambitionRewardScrap = { 50, 80, 110, 150, 200 };
        // Penalty is 20% of max HP (computed, not tabled) - see ArchiveController.

        public int GetResonanceThreshold(int grade) => resonanceThreshold[Clamp(grade)];
        public int GetResonanceReward(int grade) => resonanceRewardScrap[Clamp(grade)];
        public int GetResonancePenalty(int grade) => resonancePenaltyScrap[Clamp(grade)];
        public int GetTributeThreshold(int grade) => tributeThreshold[Clamp(grade)];
        public int GetAmbitionThreshold(int grade) => ambitionThreshold[Clamp(grade)];
        public int GetAmbitionRewardScrap(int grade) => ambitionRewardScrap[Clamp(grade)];

        private int Clamp(int grade) => Mathf.Clamp(grade - 1, 0, 4);
    }
}

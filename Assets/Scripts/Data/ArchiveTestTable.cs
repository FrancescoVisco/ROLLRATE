using UnityEngine;

namespace Rollrate.Data
{
    /// <summary>
    /// Per-Grade numbers for the 3 Archive Tests (index 0 = Grade I ... 4 = Grade V).
    /// Thresholds from the redesigned doc. The Scrap reward is now UNIFIED
    /// across all three Tests (design: "la ricompensa di ogni Test superato
    /// è identica per tutti e tre") - it's only ONE of three choices the
    /// player picks from on success (the other two are Potenziamento Dado
    /// / Potenziamento Modulo, which have no Scrap value at all - see
    /// ArchiveController).
    /// </summary>
    [CreateAssetMenu(fileName = "ArchiveTestTable", menuName = "Rollrate/Archive Test Table")]
    public class ArchiveTestTable : ScriptableObject
    {
        [Header("Test di Risonanza - roll Core Die")]
        public int[] resonanceThreshold = { 3, 5, 6, 7, 8 };
        [Tooltip("Risk on failure: Scrap lost. Not a specific number in the design doc ('Perdita di Scrap') - adjust freely.")]
        public int[] resonancePenaltyScrap = { 10, 15, 20, 25, 30 };

        [Header("Test di Tributo - roll entire Pool, sum")]
        public int[] tributeThreshold = { 10, 20, 30, 40, 50 };
        // Risk on failure: player CHOOSES a die or module to lose permanently - see ArchiveController's sacrifice-choice step. No Scrap number needed here.

        [Header("Test di Ambizione - roll Core + best Pool die")]
        public int[] ambitionThreshold = { 6, 12, 16, 20, 24 };
        // Risk on failure: 20% of Max HP (computed directly in ArchiveController, not tabled here).

        [Header("Reward (UNIFIED - identical for all 3 Tests, per Grade)")]
        [Tooltip("Scelta dell'Archivio, option 1 of 3: flat Scrap. The other two options (Potenziamento Dado / Potenziamento Modulo) have no Scrap value.")]
        public int[] rewardScrap = { 20, 30, 40, 50, 60 };

        public int GetResonanceThreshold(int grade) => resonanceThreshold[Clamp(grade)];
        public int GetResonancePenalty(int grade) => resonancePenaltyScrap[Clamp(grade)];
        public int GetTributeThreshold(int grade) => tributeThreshold[Clamp(grade)];
        public int GetAmbitionThreshold(int grade) => ambitionThreshold[Clamp(grade)];
        public int GetRewardScrap(int grade) => rewardScrap[Clamp(grade)];

        private int Clamp(int grade) => Mathf.Clamp(grade - 1, 0, 4);
    }
}

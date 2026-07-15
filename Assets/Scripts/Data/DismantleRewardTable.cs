using UnityEngine;

namespace Rollrate.Data
{
    /// <summary>
    /// Flat Scrap reward per Grade for dismantling a die or module at the
    /// Dismantle Node. Based on the ITEM's own Grade (DieData.grade /
    /// ModuleData.grade), not the player's current Echelon - unlike Shop
    /// costs, which scale with the player's progress.
    /// </summary>
    [CreateAssetMenu(fileName = "DismantleRewardTable", menuName = "Rollrate/Dismantle Reward Table")]
    public class DismantleRewardTable : ScriptableObject
    {
        [Header("Scrap per Grade (index 0 = Grade I ... index 4 = Grade V)")]
        public int[] moduleScrapByGrade = { 10, 15, 20, 25, 30 };
        public int[] dieScrapByGrade = { 8, 12, 16, 20, 24 };

        public int GetModuleScrap(int grade) => moduleScrapByGrade[Mathf.Clamp(grade - 1, 0, moduleScrapByGrade.Length - 1)];
        public int GetDieScrap(int grade) => dieScrapByGrade[Mathf.Clamp(grade - 1, 0, dieScrapByGrade.Length - 1)];
    }
}

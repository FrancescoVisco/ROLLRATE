using UnityEngine;

namespace Rollrate.Data
{
    /// <summary>
    /// The Shop's cost table, one column per Grade (index 0 = Grade I ...
    /// index 4 = Grade V), matching the design document's "Tabella Costi".
    /// </summary>
    [CreateAssetMenu(fileName = "ShopCostTable", menuName = "Rollrate/Shop Cost Table")]
    public class ShopCostTable : ScriptableObject
    {
        [Header("Costs per Grade (I, II, III, IV, V)")]
        public int[] newModuleCost = { 30, 45, 60, 80, 100 };
        public int[] newDieCost = { 25, 40, 55, 70, 85 };
        [Tooltip("Evolving a satellite die (e.g. D4->D6). Never applies to the Core Die - the Core only evolves by defeating a Guardian.")]
        public int[] evolveDieCost = { 35, 50, 65, 80, 95 };
        [Tooltip("Cost per single HP point repaired.")]
        public int[] repairHpCost = { 10, 20, 40, 60, 80 };
        [Tooltip("Cost to permanently raise Max HP.")]
        public int[] increaseMaxHpCost = { 10, 20, 40, 60, 80 };
        public int[] rerollShopCost = { 5, 10, 15, 20, 25 };

        private int GradeIndex(int currentEchelon) => Mathf.Clamp(currentEchelon - 1, 0, 4);

        public int GetNewModuleCost(int currentEchelon) => newModuleCost[GradeIndex(currentEchelon)];
        public int GetNewDieCost(int currentEchelon) => newDieCost[GradeIndex(currentEchelon)];
        public int GetEvolveDieCost(int currentEchelon) => evolveDieCost[GradeIndex(currentEchelon)];
        public int GetRepairHpCost(int currentEchelon) => repairHpCost[GradeIndex(currentEchelon)];
        public int GetIncreaseMaxHpCost(int currentEchelon) => increaseMaxHpCost[GradeIndex(currentEchelon)];
        public int GetRerollShopCost(int currentEchelon) => rerollShopCost[GradeIndex(currentEchelon)];
    }
}

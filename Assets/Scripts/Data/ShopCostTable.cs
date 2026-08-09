using UnityEngine;

namespace Rollrate.Data
{
    /// <summary>
    /// The Shop's cost table, one column per Grade (index 0 = Grade I ...
    /// index 4 = Grade V), matching the design document's "Tabella Costi"
    /// (Section 6) exactly - Nuovo Dado, Aumenta PV, Reroll Shop. No
    /// Modules (removed system), no die evolution (that only happens via
    /// Furnace fusion or a Guardian's Data Extraction, never a purchase),
    /// no HP repair (that's the Rest node's role, not the Shop's).
    /// </summary>
    [CreateAssetMenu(fileName = "ShopCostTable", menuName = "Rollrate/Shop Cost Table")]
    public class ShopCostTable : ScriptableObject
    {
        [Header("Costs per Grade (I, II, III, IV, V)")]
        public int[] newDieCost = { 15, 25, 40, 60, 70 };
        [Tooltip("Permanently raises Max HP by 1 (design doc Section 6, 'Aumento Coerenza').")]
        public int[] increaseMaxHpCost = { 10, 15, 20, 25, 30 };
        public int[] rerollShopCost = { 5, 10, 15, 20, 25 };

        private int GradeIndex(int currentEchelon) => Mathf.Clamp(currentEchelon - 1, 0, 4);

        public int GetNewDieCost(int currentEchelon) => newDieCost[GradeIndex(currentEchelon)];
        public int GetIncreaseMaxHpCost(int currentEchelon) => increaseMaxHpCost[GradeIndex(currentEchelon)];
        public int GetRerollShopCost(int currentEchelon) => rerollShopCost[GradeIndex(currentEchelon)];
    }
}

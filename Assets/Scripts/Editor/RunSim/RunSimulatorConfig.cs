using UnityEngine;
using Rollrate.Data;
using Rollrate.Shop;

namespace Rollrate.Simulation
{
    /// <summary>
    /// Assets the Run Simulator needs - same data the real game uses, so
    /// results reflect real balance rather than a re-authored copy.
    /// </summary>
    [CreateAssetMenu(fileName = "RunSimulatorConfig", menuName = "Rollrate/Run Simulator Config")]
    public class RunSimulatorConfig : ScriptableObject
    {
        [Header("Starting Values (mirrors RunManager)")]
        public DieData startingCoreDie; // assign the D4 asset
        public int startingHp = 10;
        public DieData[] startingPool; // mirrors RunManager's debugStartingPool
        [Tooltip("Mirrors RunManager.HandSize - how many dice are drawn from the Draw Pile each turn.")]
        public int handSize = 6;

        [Header("Data (same assets the real game uses)")]
        public EnemyRegistry enemyRegistry;
        public ShopOfferPools offerPools;
        public ShopCostTable costTable;
        public ArchiveTestTable archiveTestTable;
        public DismantleRewardTable dismantleRewardTable;

        [Header("Starting Loadout (debug modules, mirrors RunManager's debug fields)")]
        public ModuleData startingPowerModule;
        public ModuleData startingStabilityModule;
        public ModuleData startingFlowModule;
        public ModuleData startingEchoModule;

        [Header("Simulation Bounds")]
        [Tooltip("Safety cap - if a single campaign takes more runs than this without reaching victory, it's abandoned and logged as a failure (shouldn't normally trigger).")]
        public int maxRunsPerCampaign = 200;
        [Tooltip("Safety cap on turns per fight, same purpose as the combat Balance Simulator's.")]
        public int maxTurnsPerFight = 30;
    }
}

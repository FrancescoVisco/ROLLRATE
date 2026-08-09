using System;
using UnityEngine;
using Rollrate.Data;

namespace Rollrate.Simulation
{
    [Serializable]
    public class RunSimStartingDie
    {
        public DieData data;
        public DieType type;
    }

    [Serializable]
    public class RunSimDieSizeOptions
    {
        public DieData[] options;
    }

    /// <summary>
    /// Everything the simulator needs configured, mirroring the same data
    /// the real game reads from RunManager/ShopController/FurnaceController.
    /// A real asset (like ShopCostTable/EnemyRegistry) - set it up once,
    /// save it, and just assign the SAME asset in the Run Simulator
    /// window every time, instead of re-entering everything from scratch.
    /// </summary>
    [CreateAssetMenu(fileName = "RunSimConfig", menuName = "Rollrate/Run Simulator Config")]
    public class RunSimConfig : ScriptableObject
    {
        [Header("Starting Run")]
        public DieData startingCoreDie;
        public int startingHp = 10;
        public int handSize = 6;
        public RunSimStartingDie[] startingPool;

        [Header("Registries / Tables (same assets the real scenes use)")]
        public EnemyRegistry enemyRegistry;
        public EffectRegistry effectRegistry;
        public ShopCostTable shopCostTable;
        public ArchiveTestTable archiveTestTable;

        [Header("Shop - die sizes sold per Grade (mirrors ShopController)")]
        public RunSimDieSizeOptions[] dieSizeByGrade = new RunSimDieSizeOptions[5];
        public int shopOfferCount = 5;

        [Header("Furnace")]
        public int[] fusionCostByGrade = { 20, 35, 55, 80, 100 };

        [Header("Simulation")]
        [Tooltip("How many simulated runs (Fragmentation cycles) before giving up on a campaign that never wins.")]
        public int maxRunsPerCampaign = 30;
        [Tooltip("Safety cap on turns within a single fight, in case a balance issue creates a stalemate.")]
        public int maxTurnsPerFight = 60;
    }
}

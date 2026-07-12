using System;
using System.Collections.Generic;
using UnityEngine;
using Rollrate.Data;

namespace Rollrate.Core
{
    /// <summary>
    /// Holds the full state of the current run. This is NOT a ScriptableObject:
    /// it's a plain runtime object that lives for the duration of a play session
    /// and gets rebuilt/reset on Fragmentation (defeat).
    ///
    /// Persistent fields (survive defeat) are marked below.
    /// Non-persistent fields reset to their starting values on Fragmentation.
    /// </summary>
    [Serializable]
    public class GameState
    {
        [Header("Core Die (PERSISTENT across runs)")]
        public DieData coreDie;

        [Header("Scrap (PERSISTENT across runs)")]
        public int scrap;

        [Header("Dice Pool (reset on Fragmentation)")]
        public List<DieData> dicePool = new List<DieData>();

        [Header("Installed Modules per Slot (reset on Fragmentation)")]
        public Dictionary<SlotType, ModuleData> installedModules = new Dictionary<SlotType, ModuleData>();

        [Header("HP (reset on Fragmentation)")]
        public int currentHp;
        public int maxHp;

        [Header("Progress (reset on Fragmentation)")]
        public int currentEchelon = 1; // Grade I -> V
        public int currentPage = 1;    // Page 1-3 within the Echelon

        [Header("Deferred Turn Effects (reset on Fragmentation)")]
        [Tooltip("Changeover: 10 Charges = +1 Die added to the pool.")]
        public int changeoverCharges;
        [Tooltip("Overload: flat bonus applied to the first Power die placed next turn.")]
        public int pendingNextTurnPowerBonus;
        [Tooltip("Aiming: % reduction applied to the enemy Threshold next turn (0 = none).")]
        public float pendingThresholdReductionPercent;

        /// <summary>
        /// Sets up a brand new run: default HP, empty pool except the Core Die,
        /// no modules installed. Scrap and Core Die evolution level are NOT touched here -
        /// call this only after applying persistence rules on defeat.
        /// </summary>
        public void ResetForNewRun(DieData startingCoreDie, int startingHp)
        {
            coreDie = startingCoreDie;
            currentHp = startingHp;
            maxHp = startingHp;
            currentEchelon = 1;
            currentPage = 1;
            dicePool.Clear();
            installedModules.Clear();
            changeoverCharges = 0;
            pendingNextTurnPowerBonus = 0;
            pendingThresholdReductionPercent = 0f;
        }

        /// <summary>
        /// Applies the Fragmentation rule: Core Die level and Scrap persist,
        /// everything else (pool, modules, HP, progress) resets.
        /// </summary>
        public void ApplyFragmentation(int startingHp)
        {
            // coreDie and scrap are intentionally left untouched
            currentHp = startingHp;
            maxHp = startingHp;
            currentEchelon = 1;
            currentPage = 1;
            dicePool.Clear();
            installedModules.Clear();
            changeoverCharges = 0;
            pendingNextTurnPowerBonus = 0;
            pendingThresholdReductionPercent = 0f;
        }

        public bool IsDefeated => currentHp <= 0;
    }
}

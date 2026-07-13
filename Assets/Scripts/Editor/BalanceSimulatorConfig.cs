using UnityEngine;

namespace Rollrate.Simulation
{
    /// <summary>
    /// Configuration for a balance simulation run. Create one via
    /// Assets/Create/Rollrate/Balance Simulator Config.
    ///
    /// Everything is AUTO-DISCOVERED from a folder - no manual dragging of
    /// dice/modules/enemies required. The simulator:
    /// - Finds every DieData, ModuleData, and EnemyData asset inside
    ///   AssetsFolderPath (subfolders included).
    /// - Generates one "pure pool" preset per (die type x pool size in
    ///   PoolSizesToTest) - e.g. "4x D6", "6x D6", "4x D12", etc.
    /// - Generates the full cross-product of every discovered module option
    ///   per slot (Power x Stability x Flow x Echo) as Module Loadouts.
    ///
    /// This can produce a LOT of combinations very fast - the Balance
    /// Simulator window shows the total fight count before you run it, so
    /// you can dial FightsPerCombination down (or narrow the overrides
    /// below) if it's too large.
    /// </summary>
    [CreateAssetMenu(fileName = "BalanceSimulatorConfig", menuName = "Rollrate/Balance Simulator Config")]
    public class BalanceSimulatorConfig : ScriptableObject
    {
        [Header("Auto-Discovery")]
        [Tooltip("Folder (relative to the project, e.g. 'Assets/Data') scanned for DieData, ModuleData, and EnemyData assets. Subfolders are included. They can all be mixed together in one folder.")]
        public string assetsFolderPath = "Assets/Data";

        [Header("Auto-Generated Pool Presets")]
        [Tooltip("For each discovered die type, generates a 'pure pool' (N copies of that one type) at each size listed here. E.g. sizes {2,6} with 8 die types = 16 auto-generated pool presets.")]
        public int[] poolSizesToTest = { 4 };
        [Tooltip("If true, also generates 2 MIXED pool presets per size (heterogeneous dice, not all one type): 'Mixed-AllTypes' (cycles through every die type) and 'Mixed-Weakest+Strongest' (half weakest, half strongest die found). Pure pools over-represent duplicate values as size grows, inflating Full Resonance frequency in a way real mixed pools likely wouldn't - enable this to check that effect.")]
        public bool includeMixedPools = true;

        [Header("Auto-Generated Module Loadouts")]
        [Tooltip("If true, each slot's option list also includes 'no module installed', multiplying the loadout count further. Off by default to keep the combination count manageable.")]
        public bool includeEmptySlotOption = false;

        [Header("Simulation Settings")]
        [Tooltip("How many independent fights to simulate for each Core x Pool x Loadout x Enemy combination. Consider starting low (e.g. 20-50) for a first broad sweep, then raising it for a narrowed-down follow-up run.")]
        public int fightsPerCombination = 50;
        [Tooltip("Safety cap: a fight that goes past this many turns is recorded as a timeout instead of looping forever.")]
        public int maxTurnsPerFight = 30;
        [Tooltip("Starting/max HP for the player in every simulated fight.")]
        public int startingHp = 10;

        [Header("Optional Overrides (leave empty to auto-discover everything)")]
        [Tooltip("If set, ONLY these Core Dice are tested instead of every DieData found in the folder.")]
        public Rollrate.Data.DieData[] coreDiceOverride;
        [Tooltip("If set, ONLY these Enemies are tested instead of every EnemyData found in the folder.")]
        public Rollrate.Data.EnemyData[] enemiesOverride;
    }
}

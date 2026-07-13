using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Rollrate.Data;

namespace Rollrate.Simulation
{
    /// <summary>
    /// Editor-only helpers: finds all assets of a type inside a folder, and
    /// auto-generates Pool Presets / Module Loadouts from what's found, so
    /// nothing needs to be dragged into the config by hand.
    /// </summary>
    public static class SimulationAutoDiscovery
    {
        public static List<T> FindAllOfType<T>(string folder) where T : Object
        {
            var result = new List<T>();
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder)) return result;

            string typeName = typeof(T).Name;
            string[] guids = AssetDatabase.FindAssets($"t:{typeName}", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) result.Add(asset);
            }
            return result;
        }

        /// <summary>
        /// Generates one "pure pool" PoolPreset (N copies of one die type)
        /// per (die type x size) combination.
        /// </summary>
        public static List<PoolPreset> GeneratePoolPresets(List<DieData> allDice, int[] sizes)
        {
            var result = new List<PoolPreset>();
            foreach (DieData die in allDice)
            {
                foreach (int size in sizes)
                {
                    if (size <= 0) continue;
                    var dice = new DieData[size];
                    for (int i = 0; i < size; i++) dice[i] = die;
                    result.Add(new PoolPreset { presetName = $"{size}x {die.displayName}", dice = dice });
                }
            }
            return result;
        }

        /// <summary>
        /// Generates MIXED pool presets, as opposed to the "pure" (all one
        /// die type) presets from GeneratePoolPresets. Pure pools of the
        /// same die type over-represent duplicate values as pool size grows
        /// (a pigeonhole/collision effect independent of die complexity),
        /// inflating Full Resonance frequency in a way real, heterogeneous
        /// pools likely wouldn't show. Two mixed compositions per size:
        ///
        /// - "Mixed-AllTypes": cycles through every discovered die type in
        ///   order, for maximum heterogeneity (e.g. size 6 with 8 die types
        ///   found -> D4,D6,D8,D10,D12,D14).
        /// - "Mixed-{weakest}+{strongest}": half the pool is the weakest die
        ///   found, half is the strongest - an "upgraded some dice, not all"
        ///   scenario in progress.
        /// </summary>
        public static List<PoolPreset> GenerateMixedPoolPresets(List<DieData> allDice, int[] sizes)
        {
            var result = new List<PoolPreset>();
            if (allDice == null || allDice.Count < 2) return result;

            var sortedDice = allDice.OrderBy(d => d.faces).ToList();
            DieData weakest = sortedDice[0];
            DieData strongest = sortedDice[sortedDice.Count - 1];

            foreach (int size in sizes)
            {
                if (size <= 0) continue;

                // Mixed-AllTypes: cycle through every discovered die type.
                var cycled = new DieData[size];
                for (int i = 0; i < size; i++) cycled[i] = sortedDice[i % sortedDice.Count];
                result.Add(new PoolPreset { presetName = $"{size}x Mixed-AllTypes", dice = cycled });

                // Mixed-Weakest+Strongest: half weakest, half strongest (skip if they're the same die).
                if (weakest != strongest)
                {
                    var halfHalf = new DieData[size];
                    int half = size / 2;
                    for (int i = 0; i < size; i++) halfHalf[i] = i < half ? weakest : strongest;
                    result.Add(new PoolPreset { presetName = $"{size}x Mixed-{weakest.displayName}+{strongest.displayName}", dice = halfHalf });
                }
            }
            return result;
        }

        /// <summary>
        /// Generates the full cross-product of module options per slot as
        /// ModuleLoadout entries. If includeEmptyOption is true, each slot's
        /// option list also includes "null" (no module installed).
        /// </summary>
        public static List<ModuleLoadout> GenerateModuleLoadouts(List<ModuleData> allModules, bool includeEmptyOption)
        {
            List<ModuleData> PowerOptions() => OptionsFor(allModules, SlotType.Power, includeEmptyOption);
            List<ModuleData> StabilityOptions() => OptionsFor(allModules, SlotType.Stability, includeEmptyOption);
            List<ModuleData> FlowOptions() => OptionsFor(allModules, SlotType.Flow, includeEmptyOption);
            List<ModuleData> EchoOptions() => OptionsFor(allModules, SlotType.Echo, includeEmptyOption);

            var result = new List<ModuleLoadout>();
            foreach (var p in PowerOptions())
                foreach (var s in StabilityOptions())
                    foreach (var f in FlowOptions())
                        foreach (var e in EchoOptions())
                        {
                            result.Add(new ModuleLoadout
                            {
                                loadoutName = $"{NameOf(p)}/{NameOf(s)}/{NameOf(f)}/{NameOf(e)}",
                                power = p,
                                stability = s,
                                flow = f,
                                echo = e,
                            });
                        }
            return result;
        }

        private static List<ModuleData> OptionsFor(List<ModuleData> allModules, SlotType slot, bool includeEmpty)
        {
            var options = allModules.Where(m => m.slot == slot).ToList();
            if (includeEmpty) options.Add(null);
            return options;
        }

        private static string NameOf(ModuleData m) => m != null ? m.displayName : "-";
    }
}

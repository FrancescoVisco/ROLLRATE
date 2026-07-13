using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rollrate.Simulation
{
    /// <summary>
    /// Window/Rollrate/Balance Simulator. Assign a BalanceSimulatorConfig
    /// asset, review the auto-discovered counts, click Run, and a CSV with
    /// one row per Core x Pool x Loadout x Enemy combination gets written
    /// to the project root (BalanceSimulationResults.csv, next to Assets).
    /// </summary>
    public class BalanceSimulatorWindow : EditorWindow
    {
        private BalanceSimulatorConfig config;
        private Vector2 scroll;
        private string lastOutputPath;
        private List<string> progressLog = new List<string>();

        [MenuItem("Window/Rollrate/Balance Simulator")]
        public static void ShowWindow()
        {
            GetWindow<BalanceSimulatorWindow>("Balance Simulator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("ROLLRATE - Balance Simulator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            config = (BalanceSimulatorConfig)EditorGUILayout.ObjectField("Config", config, typeof(BalanceSimulatorConfig), false);

            if (config == null)
            {
                EditorGUILayout.HelpBox(
                    "Create a config via Assets/Create/Rollrate/Balance Simulator Config, " +
                    "point it at your assets folder, then assign it here. Everything else is auto-discovered.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            var so = new SerializedObject(config);
            so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("assetsFolderPath"));
            EditorGUILayout.PropertyField(so.FindProperty("poolSizesToTest"), true);
            EditorGUILayout.PropertyField(so.FindProperty("includeMixedPools"));
            EditorGUILayout.PropertyField(so.FindProperty("includeEmptySlotOption"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(so.FindProperty("fightsPerCombination"));
            EditorGUILayout.PropertyField(so.FindProperty("maxTurnsPerFight"));
            EditorGUILayout.PropertyField(so.FindProperty("startingHp"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Optional Overrides", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("coreDiceOverride"), true);
            EditorGUILayout.PropertyField(so.FindProperty("enemiesOverride"), true);
            so.ApplyModifiedProperties();

            EditorGUILayout.Space();

            var preview = BalanceSimulationRunner.PreviewCounts(config);

            EditorGUILayout.LabelField("Auto-Discovery Preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Dice found: {preview.diceFound}  |  Modules found: {preview.modulesFound}  |  Enemies found: {preview.enemiesFound}");
            EditorGUILayout.LabelField($"Auto-generated Pool Presets: {preview.poolPresetCount}  |  Module Loadouts: {preview.loadoutCount}");

            EditorGUILayout.Space();

            var boxStyle = preview.totalFights > 2_000_000 ? MessageType.Warning : MessageType.None;
            EditorGUILayout.HelpBox(
                $"Total: {preview.combinations:N0} combinations x {config.fightsPerCombination} fights = {preview.totalFights:N0} simulated fights.",
                boxStyle);

            if (preview.totalFights > 2_000_000)
            {
                EditorGUILayout.HelpBox(
                    "This is a very large run and may take a long time (and will freeze the Editor while running). " +
                    "Consider: fewer Pool Sizes To Test, a smaller Fights Per Combination for a first broad pass, " +
                    "or restricting Core Dice / Enemies via the Overrides above.",
                    MessageType.Warning);
            }

            if (GUILayout.Button("Run Simulation", GUILayout.Height(32)))
            {
                RunSimulation();
            }

            if (!string.IsNullOrEmpty(lastOutputPath))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox($"Last result written to:\n{lastOutputPath}", MessageType.None);
                if (GUILayout.Button("Show in Explorer/Finder"))
                {
                    EditorUtility.RevealInFinder(lastOutputPath);
                }
            }

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(200));
            foreach (var line in progressLog)
            {
                EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        private void RunSimulation()
        {
            progressLog.Clear();

            var preview = BalanceSimulationRunner.PreviewCounts(config);
            if (preview.diceFound == 0 || preview.modulesFound == 0 || preview.enemiesFound == 0)
            {
                EditorUtility.DisplayDialog("Balance Simulator",
                    "No Dice/Modules/Enemies found in the configured Assets Folder Path. Double-check the path and that your assets are inside it.",
                    "OK");
                return;
            }

            var results = BalanceSimulationRunner.RunAll(config, line =>
            {
                progressLog.Add(line);
                Repaint();
            });

            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "BalanceSimulationResults.csv");
            BalanceSimulationRunner.WriteCsv(results, path);
            lastOutputPath = path;

            Debug.Log($"[BalanceSimulator] Done. {results.Count} combinations written to {path}");
            EditorUtility.DisplayDialog("Balance Simulator", $"Done!\n{results.Count} combinations simulated.\nResults written to:\n{path}", "OK");
        }
    }
}

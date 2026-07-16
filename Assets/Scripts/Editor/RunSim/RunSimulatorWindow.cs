using UnityEditor;
using UnityEngine;
using Rollrate.Simulation;

namespace Rollrate.Editor
{
    /// <summary>
    /// Window/Rollrate/Run Simulator. Simulates N full Campaigns end-to-end
    /// (Map -> every node type -> Terminal/Recalibration -> victory), then
    /// prints the aggregate statistics requested: runs-to-victory, Shop
    /// purchases, module usage, Meta unlocks, Archive Test win rates,
    /// dismantle count, Collection swap count.
    /// </summary>
    public class RunSimulatorWindow : EditorWindow
    {
        private RunSimulatorConfig config;
        private int campaignCount = 20;
        private string resultText = "";
        private Vector2 scroll;
        private bool isRunning;

        [MenuItem("Window/Rollrate/Run Simulator")]
        public static void ShowWindow()
        {
            GetWindow<RunSimulatorWindow>("Run Simulator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("ROLLRATE - Full Run Simulator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Simula l'intero ciclo di gioco (Mappa, Shop, Archivio, Smantellamento, Falò, Combattimento, Terminale/Ricalibrazione) per N Campagne complete (dalla prima Run fino alla Vittoria di Grado V). Non tocca i PlayerPrefs reali del gioco.", MessageType.Info);

            config = (RunSimulatorConfig)EditorGUILayout.ObjectField("Config", config, typeof(RunSimulatorConfig), false);
            campaignCount = EditorGUILayout.IntField("Numero di Campagne da simulare", campaignCount);

            EditorGUI.BeginDisabledGroup(config == null || isRunning);
            if (GUILayout.Button("Run Simulation", GUILayout.Height(30)))
            {
                RunSimulation();
            }
            EditorGUI.EndDisabledGroup();

            if (config == null)
            {
                EditorGUILayout.HelpBox("Assegna un RunSimulatorConfig per procedere.", MessageType.Warning);
            }

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(resultText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void RunSimulation()
        {
            isRunning = true;
            resultText = "Simulazione in corso...\n";

            try
            {
                RunSimStats stats = RunSimulator.SimulateCampaigns(config, campaignCount, msg => Debug.Log($"[RunSimulator] {msg}"));
                resultText = stats.FormatSummary();
                Debug.Log("[RunSimulator] Simulazione completata.\n" + resultText);
            }
            catch (System.Exception e)
            {
                resultText = $"ERRORE durante la simulazione:\n{e}";
                Debug.LogError(resultText);
            }
            finally
            {
                isRunning = false;
            }
        }
    }
}

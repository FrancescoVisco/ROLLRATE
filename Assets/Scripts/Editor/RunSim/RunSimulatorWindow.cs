using UnityEditor;
using UnityEngine;

namespace Rollrate.Simulation
{
    /// <summary>
    /// Editor window to configure and run the full-game simulator (Map,
    /// Combat, Shop, Furnace, Archive, Meta) any number of times, printing
    /// the aggregated RunSimStats summary - see RunSimulator's class
    /// summary for exactly which simplifications it uses.
    /// </summary>
    public class RunSimulatorWindow : EditorWindow
    {
        private RunSimConfig _config = new RunSimConfig();
        private int _campaignCount = 100;
        private string _resultText = "";
        private Vector2 _scroll;
        private SerializedObject _serializedConfig;

        [MenuItem("Rollrate/Run Simulator")]
        public static void ShowWindow()
        {
            GetWindow<RunSimulatorWindow>("Run Simulator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Configurazione", EditorStyles.boldLabel);

            // A plain C# class (not a MonoBehaviour/ScriptableObject) still draws fine
            // through a throwaway SerializedObject wrapper - simplest way to get normal
            // Inspector-style fields (with array resize handles etc.) in an EditorWindow.
            if (_serializedConfig == null || _serializedConfig.targetObject == null)
            {
                var holder = ScriptableObject.CreateInstance<RunSimConfigHolder>();
                holder.config = _config;
                _serializedConfig = new SerializedObject(holder);
            }

            _serializedConfig.Update();
            var configProp = _serializedConfig.FindProperty("config");
            EditorGUILayout.PropertyField(configProp, true);
            _serializedConfig.ApplyModifiedProperties();

            EditorGUILayout.Space();
            _campaignCount = EditorGUILayout.IntField("Numero di Campagne", _campaignCount);

            if (GUILayout.Button("Esegui Simulazione"))
            {
                var stats = RunSimulator.RunCampaigns(_config, _campaignCount);
                _resultText = stats.FormatSummary();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Risultati", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_resultText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        /// <summary>Throwaway wrapper purely so RunSimConfig (a plain class) can be drawn via SerializedObject/PropertyField, with proper array resize handles etc.</summary>
        private class RunSimConfigHolder : ScriptableObject
        {
            public RunSimConfig config;
        }
    }
}

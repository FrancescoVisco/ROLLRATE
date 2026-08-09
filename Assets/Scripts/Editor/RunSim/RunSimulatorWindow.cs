using UnityEditor;
using UnityEngine;

namespace Rollrate.Simulation
{
    /// <summary>
    /// Editor window to run the full-game simulator (Map, Combat, Shop,
    /// Furnace, Archive, Meta) any number of times, printing the
    /// aggregated RunSimStats summary - see RunSimulator's class summary
    /// for exactly which simplifications it uses.
    ///
    /// Configuration lives in a RunSimConfig ASSET (Create > Rollrate >
    /// Run Simulator Config) - set it up once, save it, and just assign
    /// the same asset here every time, instead of re-entering everything
    /// from scratch each session.
    /// </summary>
    public class RunSimulatorWindow : EditorWindow
    {
        private RunSimConfig _config;
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

            var newConfig = (RunSimConfig)EditorGUILayout.ObjectField("Config Asset", _config, typeof(RunSimConfig), false);
            if (newConfig != _config)
            {
                _config = newConfig;
                _serializedConfig = _config != null ? new SerializedObject(_config) : null;
            }

            if (_config == null)
            {
                EditorGUILayout.HelpBox("Assegna un asset RunSimConfig (Project window -> click destro -> Create -> Rollrate -> Run Simulator Config), oppure creane uno nuovo qui sotto.", MessageType.Info);
                if (GUILayout.Button("Crea nuovo asset RunSimConfig..."))
                {
                    string path = EditorUtility.SaveFilePanelInProject("Nuovo Run Simulator Config", "RunSimConfig", "asset", "Dove salvare il nuovo asset di configurazione?");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var asset = ScriptableObject.CreateInstance<RunSimConfig>();
                        AssetDatabase.CreateAsset(asset, path);
                        AssetDatabase.SaveAssets();
                        _config = asset;
                        _serializedConfig = new SerializedObject(_config);
                    }
                }
                return;
            }

            _serializedConfig.Update();
            SerializedProperty prop = _serializedConfig.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                if (prop.name == "m_Script") { enterChildren = false; continue; } // skip the built-in script reference field
                EditorGUILayout.PropertyField(prop, true);
                enterChildren = false;
            }
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
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rollrate.Data;

namespace Rollrate.Meta
{
    /// <summary>
    /// Orchestrates the Meta screen. Unlike Shop/Collection/etc., this
    /// does NOT require RunManager - meta-progression lives entirely in
    /// MetaProgressionManager's PlayerPrefs storage, so this scene can be
    /// tested standalone (Play directly on it) or entered from a future
    /// main menu / pause menu, not just additively from the Map.
    /// </summary>
    public class MetaUI : MonoBehaviour
    {
        [SerializeField] private MetaController controller;
        [SerializeField] private TextMeshProUGUI fragmentsText;
        [SerializeField] private GameObject rowPrefab;
        [SerializeField] private Transform diceContainer;
        [SerializeField] private Transform modulesContainer;
        [SerializeField] private Button leaveButton;
        [SerializeField] private string mainMenuSceneName = "MainMenuScene";

        private readonly List<GameObject> _spawnedRows = new List<GameObject>();

        private void Start()
        {
            if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);
            RefreshAll();
        }

        private void OnLeaveClicked()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
        }

        public void RefreshAll()
        {
            if (fragmentsText != null) fragmentsText.text = $"Frammenti Residui: {controller.ResidualFragments}";

            foreach (GameObject go in _spawnedRows)
            {
                if (go != null) Destroy(go);
            }
            _spawnedRows.Clear();

            // Grade I items have nothing earlier to unlock to - skip them.
            foreach (DieData die in controller.GetAllDice())
            {
                if (die == null || die.grade <= 1) continue;
                SpawnRow(die.name, die.displayName, die.grade, diceContainer);
            }

            foreach (ModuleData module in controller.GetAllModules())
            {
                if (module == null || module.grade <= 1) continue;
                SpawnRow(module.name, $"{module.displayName} ({module.slot})", module.grade, modulesContainer);
            }
        }

        private void SpawnRow(string keyName, string displayName, int grade, Transform container)
        {
            if (container == null) return;
            GameObject rowGO = Instantiate(rowPrefab, container);
            var row = rowGO.GetComponent<MetaRowUI>();
            row.Setup(keyName, displayName, grade, this);
            _spawnedRows.Add(rowGO);
        }
    }
}

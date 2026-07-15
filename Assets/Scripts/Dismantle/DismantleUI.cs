using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rollrate.Core;
using Rollrate.Data;

namespace Rollrate.Dismantle
{
    /// <summary>
    /// Orchestrates the Dismantle Node scene: lists every distinct owned
    /// die and module (grouped with counts), lets the player destroy one
    /// copy at a time for Scrap. Loaded additively like Shop/Collection -
    /// a Map node will call NodeSceneLoader.EnterNode("DismantleScene")
    /// once the Map exists; use the debug button until then.
    /// </summary>
    public class DismantleUI : MonoBehaviour
    {
        [SerializeField] private DismantleController controller;
        [SerializeField] private TextMeshProUGUI scrapText;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private GameObject rowPrefab;
        [SerializeField] private Transform diceContainer;
        [SerializeField] private Transform modulesContainer;
        [SerializeField] private Button leaveButton;
        [SerializeField] private string dismantleSceneName = "DismantleScene";

        private readonly List<GameObject> _spawnedRows = new List<GameObject>();

        private void Start()
        {
            if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);

            if (RunManager.Instance == null)
            {
                Debug.LogError("[DismantleUI] RunManager.Instance is null - load this scene additively via the debug button (DebugMapNodeButtons -> EnterDismantle), not by pressing Play directly on it.");
                return;
            }

            controller.OnInventoryChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (controller != null) controller.OnInventoryChanged -= Refresh;
        }

        private void OnLeaveClicked()
        {
            NodeSceneLoader.ExitNode(dismantleSceneName);
        }

        public void Refresh()
        {
            var state = RunManager.Instance.State;
            if (scrapText != null) scrapText.text = $"Scrap: {state.scrap}";
            if (hpText != null) hpText.text = $"HP: {state.currentHp} / {state.maxHp}";

            foreach (GameObject go in _spawnedRows)
            {
                if (go != null) Destroy(go);
            }
            _spawnedRows.Clear();

            foreach (DieData die in controller.GetDistinctOwnedDice())
            {
                SpawnRow(new DismantleItem { die = die }, diceContainer);
            }

            foreach (ModuleData module in controller.GetDistinctOwnedModules())
            {
                SpawnRow(new DismantleItem { module = module }, modulesContainer);
            }
        }

        private void SpawnRow(DismantleItem item, Transform container)
        {
            if (container == null) return;
            GameObject rowGO = Instantiate(rowPrefab, container);
            var row = rowGO.GetComponent<DismantleRowUI>();
            row.Setup(item, controller, this);
            _spawnedRows.Add(rowGO);
        }
    }
}

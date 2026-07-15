using System.Collections.Generic;
using UnityEngine;
using Rollrate.Core;
using Rollrate.Data;

namespace Rollrate.Shop
{
    /// <summary>
    /// Shows every OWNED module for each of the 4 slots, letting the player
    /// pick which one is actually equipped (installed) for combat. Lives in
    /// the Rest scene (e.g. "RestScene"), alongside RestUI, loaded/unloaded additively
    /// the same way as the Shop - a Map node will call
    /// NodeSceneLoader.EnterNode("RestScene") once the Map exists;
    /// for now, use a debug button (see DebugMapNodeButtons).
    /// </summary>
    public class EquipmentPanelUI : MonoBehaviour
    {
        [SerializeField] private ShopController shopController;

        [Header("One container per slot - add a Vertical/Horizontal Layout Group to each")]
        [SerializeField] private Transform powerContainer;
        [SerializeField] private Transform stabilityContainer;
        [SerializeField] private Transform flowContainer;
        [SerializeField] private Transform echoContainer;

        [Tooltip("Prefab with an EquippedModuleButtonUI component - instantiated once per owned module, per slot.")]
        [SerializeField] private GameObject moduleButtonPrefab;

        [Header("Leave")]
        [SerializeField] private UnityEngine.UI.Button leaveButton;
        [SerializeField] private string collectionSceneName = "CollectionScene";

        private readonly List<GameObject> _spawnedButtons = new List<GameObject>();

        private void Start()
        {
            // Leave must always work, even if something else below fails.
            if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);

            if (RunManager.Instance == null)
            {
                Debug.LogError("[EquipmentPanelUI] RunManager.Instance is null - this scene must be loaded additively from the Combat scene (or wherever RunManager lives), not played standalone. Use the debug button (DebugMapNodeButtons -> EnterCollection) instead of pressing Play directly on this scene.");
                return;
            }

            shopController.OnOffersChanged += Refresh; // owned modules can change after a purchase
            Refresh();
        }

        private void OnDestroy()
        {
            if (shopController != null) shopController.OnOffersChanged -= Refresh;
        }

        private void OnLeaveClicked()
        {
            NodeSceneLoader.ExitNode(collectionSceneName);
        }

        public void Refresh()
        {
            foreach (GameObject go in _spawnedButtons)
            {
                if (go != null) Destroy(go);
            }
            _spawnedButtons.Clear();

            RefreshSlot(SlotType.Power, powerContainer);
            RefreshSlot(SlotType.Stability, stabilityContainer);
            RefreshSlot(SlotType.Flow, flowContainer);
            RefreshSlot(SlotType.Echo, echoContainer);
        }

        private void RefreshSlot(SlotType slot, Transform container)
        {
            if (container == null) return;

            var state = RunManager.Instance.State;
            state.installedModules.TryGetValue(slot, out ModuleData equipped);

            List<ModuleData> owned = shopController.GetOwnedModulesForSlot(slot);

            // Group by distinct module, counting how many copies are owned -
            // ownedModules can contain the same module more than once if
            // bought multiple times, and we want one row showing "xN", not
            // N duplicate rows for the same module.
            var grouped = new Dictionary<ModuleData, int>();
            foreach (ModuleData m in owned)
            {
                if (m == null) continue;
                grouped[m] = grouped.TryGetValue(m, out int c) ? c + 1 : 1;
            }

            foreach (var kvp in grouped)
            {
                GameObject rowGO = Instantiate(moduleButtonPrefab, container);
                var row = rowGO.GetComponent<EquippedModuleButtonUI>();
                row.Setup(kvp.Key, kvp.Key == equipped, kvp.Value, this);
                _spawnedButtons.Add(rowGO);
            }
        }

        /// <summary>Called by an EquippedModuleButtonUI row when clicked.</summary>
        public void EquipModule(ModuleData module)
        {
            shopController.EquipOwnedModule(module);
            Refresh();
        }
    }
}

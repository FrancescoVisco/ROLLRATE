using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rollrate.Core;

namespace Rollrate.Shop
{
    /// <summary>
    /// Shop UI: dynamically instantiates one ShopOfferRowUI per current
    /// offer (up to Max Offers, a random Module/Die mix decided by
    /// ShopController), plus Repair HP / Increase Max HP / Reroll / Leave.
    /// Lives in the Shop scene, loaded additively on top of the Map.
    ///
    /// CHECKPOINT: buying a Module installs it directly (replacing whatever
    /// was in that slot) - no owned/equipped split for now.
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ShopController shopController;
        [SerializeField] private TextMeshProUGUI scrapText;
        [SerializeField] private TextMeshProUGUI hpText;

        [Header("Dynamic Offer Board")]
        [Tooltip("Prefab with a ShopOfferRowUI component - instantiated once per current offer.")]
        [SerializeField] private GameObject offerRowPrefab;
        [Tooltip("Parent transform the offer rows are instantiated into. Add a Layout Group here for automatic arrangement.")]
        [SerializeField] private Transform offersContainer;

        [Header("Other Actions")]
        [SerializeField] private Button repairButton;
        [SerializeField] private TextMeshProUGUI repairCostText;
        [SerializeField] private Button increaseMaxHpButton;
        [SerializeField] private TextMeshProUGUI increaseMaxHpCostText;
        [SerializeField] private Button rerollButton;
        [SerializeField] private TextMeshProUGUI rerollCostText;
        [SerializeField] private Button leaveButton;

        [Header("Scene To Return To")]
        [SerializeField] private string shopSceneName = "ShopScene";

        private readonly List<GameObject> _spawnedRows = new List<GameObject>();

        private void Start()
        {
            if (RunManager.Instance == null)
            {
                Debug.LogError("[ShopUI] RunManager.Instance is null - this scene needs RunManager already loaded " +
                                "(e.g. Play from the Combat/test scene first, then open the Shop additively).");
                return;
            }

            if (repairButton != null) repairButton.onClick.AddListener(OnRepairClicked);
            if (increaseMaxHpButton != null) increaseMaxHpButton.onClick.AddListener(OnIncreaseMaxHpClicked);
            if (rerollButton != null) rerollButton.onClick.AddListener(OnRerollClicked);
            if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);

            shopController.OnOffersChanged += RefreshOfferBoard;

            Refresh();
        }

        private void OnDestroy()
        {
            if (shopController != null)
            {
                shopController.OnOffersChanged -= RefreshOfferBoard;
            }
        }

        /// <summary>Refreshes everything: Scrap/HP labels, the offer board, and action costs.</summary>
        public void Refresh()
        {
            RefreshTopLabels();
            RefreshOfferBoard();
            RefreshActionCosts();
        }

        private void RefreshTopLabels()
        {
            var state = RunManager.Instance.State;
            if (scrapText != null) scrapText.text = $"Scrap: {state.scrap}";
            if (hpText != null) hpText.text = $"HP: {state.currentHp} / {state.maxHp}";
        }

        /// <summary>Clears and re-instantiates one row per current offer.</summary>
        private void RefreshOfferBoard()
        {
            foreach (GameObject row in _spawnedRows)
            {
                if (row != null) Destroy(row);
            }
            _spawnedRows.Clear();

            foreach (ShopOffer offer in shopController.CurrentOffers)
            {
                GameObject rowGO = Instantiate(offerRowPrefab, offersContainer);
                ShopOfferRowUI row = rowGO.GetComponent<ShopOfferRowUI>();
                row.Setup(offer, shopController);
                _spawnedRows.Add(rowGO);
            }

            RefreshTopLabels(); // Scrap may have changed after a purchase
        }

        private void RefreshActionCosts()
        {
            if (repairCostText != null) repairCostText.text = $"Repair 1 HP - {shopController.GetRepairHpCost(1)} Scrap";
            if (increaseMaxHpCostText != null) increaseMaxHpCostText.text = $"+Max HP - {shopController.GetIncreaseMaxHpCost()} Scrap";
            if (rerollCostText != null) rerollCostText.text = $"Reroll - {shopController.GetRerollCost()} Scrap";
        }

        private void OnRepairClicked()
        {
            shopController.TryRepairHp(1);
            Refresh();
        }

        private void OnIncreaseMaxHpClicked()
        {
            shopController.TryIncreaseMaxHp();
            Refresh();
        }

        private void OnRerollClicked()
        {
            shopController.TryRerollOffers();
            Refresh();
        }

        private void OnLeaveClicked()
        {
            NodeSceneLoader.ExitNode(shopSceneName);
        }
    }
}

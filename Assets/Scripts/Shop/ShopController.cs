using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Rollrate.Core;
using Rollrate.Data;

namespace Rollrate.Shop
{
    /// <summary>
    /// Nodo Dice Dealer (design doc Section 6): shows ONE rolled die offer
    /// at a time (random Type, die size for the current Grade, random
    /// unlocked Effects up to the Page/Grade cap) - click directly on the
    /// die's own icon (see ShopOfferUI) to buy it, no separate "Buy"
    /// button. Reroll pays to replace the current offer with a new one.
    /// Also offers Aumento Coerenza (+1 Max HP). No Modules, no die
    /// evolution, no HP repair - see design doc Section 6 exactly.
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        [System.Serializable]
        public class DieSizeOptions
        {
            [Tooltip("Every die size that can be sold at this Grade (design doc Section 3, Dice Hierarchy - e.g. Grade I 'Lowborn' is D4-D6, both options; Grade III 'Aristocrats' is D12 only, one option). One is picked at random each time a new offer is rolled.")]
            public DieData[] options;
        }

        [Header("Data")]
        [SerializeField] private ShopCostTable costTable;
        [SerializeField] private EffectRegistry effectRegistry;
        [Tooltip("Every possible die size sold at each Grade - index 0 = Grade I ... 4 = Grade V. Most Grades have 2 options (design doc Section 3); Grade III and V have only 1.")]
        [SerializeField] private DieSizeOptions[] dieSizeByGrade = new DieSizeOptions[5];

        [Header("UI - Shared")]
        [SerializeField] private TextMeshProUGUI scrapText;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private Button leaveButton;
        [SerializeField] private string mapSceneName = "MapScene";

        [Header("UI - Buy Die")]
        [Tooltip("Prefab with a ShopOfferUI component - instantiated fresh into Offer Container each time a new offer is rolled. Click DIRECTLY on it to buy: not a Button, a Button's own hover/press tint would fight with the Type-color tint on the same icon.")]
        [SerializeField] private ShopOfferUI offerPrefab;
        [SerializeField] private Transform offerContainer;

        [Header("UI - Increase Max HP")]
        [SerializeField] private Button increaseMaxHpButton;
        [SerializeField] private TextMeshProUGUI increaseMaxHpCostText;

        [Header("UI - Reroll")]
        [SerializeField] private Button rerollButton;
        [SerializeField] private TextMeshProUGUI rerollCostText;

        /// <summary>Design doc Section 6: max Effects on a purchasable die, by [grade-1][page-1].</summary>
        private static readonly int[,] MaxEffectsByGradeAndPage =
        {
            { 0, 0, 1 }, // Grade I
            { 1, 1, 2 }, // Grade II
            { 2, 2, 2 }, // Grade III
            { 2, 2, 3 }, // Grade IV
            { 3, 4, 4 }, // Grade V
        };

        private static readonly DieType[] AllTypes = { DieType.Power, DieType.Stability, DieType.Flow, DieType.Echo };

        private DieInstance _currentOffer;
        private ShopOfferUI _currentOfferView;
        private float _hpRatioAtEntry; // cached once at Start - avoids rounding drift if the player buys +1 Max HP many times in a row

        private void Start()
        {
            if (increaseMaxHpButton != null) increaseMaxHpButton.onClick.AddListener(OnIncreaseMaxHpClicked);
            if (rerollButton != null) rerollButton.onClick.AddListener(OnRerollClicked);
            if (leaveButton != null) leaveButton.onClick.AddListener(() => SceneManager.LoadScene(mapSceneName));

            var state = RunManager.Instance?.State;
            _hpRatioAtEntry = (state != null && state.maxHp > 0) ? (float)state.currentHp / state.maxHp : 1f;

            RollNewOffer();
            RefreshUI();
        }

        /// <summary>Rolls a brand new die offer: random Type, a random size among this Grade's options, random unlocked Effects up to the Page/Grade cap.</summary>
        private void RollNewOffer()
        {
            var state = RunManager.Instance?.State;
            if (state == null) return;

            int gradeIndex = Mathf.Clamp(state.currentEchelon - 1, 0, 4);
            int pageIndex = Mathf.Clamp(state.currentPage - 1, 0, 2);

            var options = dieSizeByGrade[gradeIndex]?.options;
            if (options == null || options.Length == 0)
            {
                Debug.LogError($"[ShopController] No die sizes configured for Grade {state.currentEchelon} - check Die Size By Grade in the Inspector.");
                return;
            }
            DieData size = options[Random.Range(0, options.Length)];
            DieType type = AllTypes[Random.Range(0, AllTypes.Length)];

            _currentOffer = new DieInstance(size, type);

            int maxEffects = MaxEffectsByGradeAndPage[gradeIndex, pageIndex];
            if (maxEffects > 0 && effectRegistry != null)
            {
                var picked = effectRegistry.GetRandomUnlocked(type, state.currentEchelon, maxEffects);
                foreach (var e in picked) _currentOffer.AddEffect(e);
            }

            if (_currentOfferView != null) Destroy(_currentOfferView.gameObject);
            if (offerPrefab != null && offerContainer != null)
            {
                int dieCost = costTable != null ? costTable.GetNewDieCost(state.currentEchelon) : 0;
                _currentOfferView = Instantiate(offerPrefab, offerContainer);
                _currentOfferView.Setup(_currentOffer, dieCost, OnBuyDieClicked);
            }
        }

        private void OnBuyDieClicked()
        {
            var state = RunManager.Instance?.State;
            if (state == null || costTable == null || _currentOffer == null) return;

            int cost = costTable.GetNewDieCost(state.currentEchelon);
            if (state.scrap < cost) return; // can't afford it - click silently does nothing

            state.scrap -= cost;
            state.AddDieToPool(_currentOffer, fromRunUnlock: true); // acquired DURING this run - eligible for the Meta screen on defeat
            RollNewOffer();
            RefreshUI();
        }

        private void OnIncreaseMaxHpClicked()
        {
            var state = RunManager.Instance?.State;
            if (state == null || costTable == null) return;

            int cost = costTable.GetIncreaseMaxHpCost(state.currentEchelon);
            if (state.scrap < cost) return;

            state.scrap -= cost;
            state.maxHp += 1; // design doc Section 6, "Aumento Coerenza"

            // Current HP scales proportionally with Max HP (floored), using the ratio from
            // when the player ENTERED the shop - not recomputed after every single +1
            // purchase, which would drift downward from repeated rounding (e.g. 10 separate
            // +1 clicks should land on the same result as one +10 jump: 6/10 -> 12/20, not
            // something lower from flooring ten times in a row).
            state.currentHp = Mathf.FloorToInt(_hpRatioAtEntry * state.maxHp);

            RefreshUI();
        }

        private void OnRerollClicked()
        {
            var state = RunManager.Instance?.State;
            if (state == null || costTable == null) return;

            int cost = costTable.GetRerollShopCost(state.currentEchelon);
            if (state.scrap < cost) return;

            state.scrap -= cost;
            RollNewOffer();
            RefreshUI();
        }

        private void RefreshUI()
        {
            var state = RunManager.Instance?.State;
            if (state == null) return;

            if (scrapText != null) scrapText.text = $"Scrap: {state.scrap}";
            if (hpText != null) hpText.text = $"HP: {state.currentHp}/{state.maxHp}";

            int dieCost = costTable.GetNewDieCost(state.currentEchelon);
            int hpCost = costTable.GetIncreaseMaxHpCost(state.currentEchelon);
            int rerollCost = costTable.GetRerollShopCost(state.currentEchelon);

            // dieCost is now shown directly on the offer itself (ShopOfferUI.Setup), not a separate text
            if (increaseMaxHpCostText != null) increaseMaxHpCostText.text = $"{hpCost} Scrap";
            if (rerollCostText != null) rerollCostText.text = $"{rerollCost} Scrap";

            if (increaseMaxHpButton != null) increaseMaxHpButton.interactable = state.scrap >= hpCost;
            if (rerollButton != null) rerollButton.interactable = state.scrap >= rerollCost;
        }
    }
}

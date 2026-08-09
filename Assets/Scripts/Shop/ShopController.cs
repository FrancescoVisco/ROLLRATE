using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rollrate.Core;
using Rollrate.Data;

namespace Rollrate.Shop
{
    /// <summary>
    /// Nodo Dice Dealer (design doc Section 6): shows SEVERAL rolled die
    /// offers at once (a real shelf, not one rotating item) - random Type,
    /// die size for the current Grade, random unlocked Effects up to the
    /// Page/Grade cap. Click directly on a die's own icon (see
    /// ShopOfferUI) to buy it, no separate "Buy" button - buying one
    /// immediately re-rolls JUST that slot, keeping the shelf full.
    /// Reroll pays to refresh the WHOLE shelf at once. Also offers
    /// Aumento Coerenza (+1 Max HP, current HP scales proportionally).
    /// No Modules, no die evolution, no HP repair - see design doc
    /// Section 6 exactly.
    ///
    /// Reached additively from the Map (NodeSceneLoader.EnterNode) - the
    /// Leave button must use NodeSceneLoader.ExitNode to return to the
    /// SAME Map instance at the SAME position, not a plain scene load
    /// (which would reload the Map from scratch, losing the current
    /// page/position and effectively restarting the run's map progress).
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        [System.Serializable]
        public class DieSizeOptions
        {
            [Tooltip("Every die size that can be sold at this Grade (design doc Section 3, Dice Hierarchy - e.g. Grade I 'Lowborn' is D4-D6, both options; Grade III 'Aristocrats' is D12 only, one option). One is picked at random each time a slot is (re)rolled.")]
            public DieData[] options;
        }

        [Header("Data")]
        [SerializeField] private ShopCostTable costTable;
        [SerializeField] private EffectRegistry effectRegistry;
        [Tooltip("Every possible die size sold at each Grade - index 0 = Grade I ... 4 = Grade V. Most Grades have 2 options (design doc Section 3); Grade III and V have only 1.")]
        [SerializeField] private DieSizeOptions[] dieSizeByGrade = new DieSizeOptions[5];
        [Tooltip("How many die offers are shown on the shelf at once.")]
        [SerializeField] private int offerCount = 5;

        [Header("UI - Shared")]
        [SerializeField] private TextMeshProUGUI scrapText;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private Button leaveButton;
        [Tooltip("Must match the actual name of this Shop scene (same convention as Combat's Combat Scene Name) - used to return to the Map via NodeSceneLoader.ExitNode.")]
        [SerializeField] private string shopSceneName = "ShopScene";

        [Header("UI - Buy Die")]
        [Tooltip("Prefab with a ShopOfferUI component - one instantiated per shelf slot (Offer Count of them) into Offer Container. Click DIRECTLY on one to buy it: not a Button, a Button's own hover/press tint would fight with the Type-color tint on the same icon.")]
        [SerializeField] private ShopOfferUI offerPrefab;
        [SerializeField] private Transform offerContainer;

        [Header("UI - Increase Max HP")]
        [SerializeField] private Button increaseMaxHpButton;
        [SerializeField] private TextMeshProUGUI increaseMaxHpCostText;

        [Header("UI - Reroll (refreshes the WHOLE shelf)")]
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

        private readonly List<DieInstance> _offers = new List<DieInstance>();
        private readonly List<ShopOfferUI> _offerViews = new List<ShopOfferUI>();
        private float _hpRatioAtEntry; // cached once at Start - avoids rounding drift if the player buys +1 Max HP many times in a row

        private void Start()
        {
            if (increaseMaxHpButton != null) increaseMaxHpButton.onClick.AddListener(OnIncreaseMaxHpClicked);
            if (rerollButton != null) rerollButton.onClick.AddListener(OnRerollShelfClicked);
            if (leaveButton != null) leaveButton.onClick.AddListener(() => NodeSceneLoader.ExitNode(shopSceneName));

            var state = RunManager.Instance?.State;
            _hpRatioAtEntry = (state != null && state.maxHp > 0) ? (float)state.currentHp / state.maxHp : 1f;

            RollAllOffers();
            RefreshCosts();
        }

        /// <summary>Fills the whole shelf with fresh offers (Start, and paid Reroll).</summary>
        private void RollAllOffers()
        {
            foreach (var view in _offerViews)
            {
                if (view != null) Destroy(view.gameObject);
            }
            _offers.Clear();
            _offerViews.Clear();

            for (int i = 0; i < offerCount; i++)
            {
                SpawnOfferSlot();
            }
        }

        /// <summary>Rolls one new die (random Type, this Grade's size, unlocked Effects up to the Page/Grade cap) and instantiates its view into a new shelf slot.</summary>
        private void SpawnOfferSlot()
        {
            var state = RunManager.Instance?.State;
            if (state == null || offerPrefab == null || offerContainer == null) return;

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

            var offer = new DieInstance(size, type);

            int maxEffects = MaxEffectsByGradeAndPage[gradeIndex, pageIndex];
            if (maxEffects > 0 && effectRegistry != null)
            {
                var picked = effectRegistry.GetRandomUnlocked(type, state.currentEchelon, maxEffects);
                foreach (var e in picked) offer.AddEffect(e);
            }

            int dieCost = costTable != null ? costTable.GetNewDieCost(state.currentEchelon) : 0;
            string costLabel = ArchiveRewardContext.FreeDiePurchasePending ? "Gratis" : $"{dieCost} Scrap";
            var view = Instantiate(offerPrefab, offerContainer);
            view.Setup(offer, costLabel, () => OnBuyDieClicked(offer, view));

            _offers.Add(offer);
            _offerViews.Add(view);
        }

        /// <summary>Buying one slot removes just that die and immediately re-rolls a fresh one in its place - the shelf always stays full.</summary>
        private void OnBuyDieClicked(DieInstance offer, ShopOfferUI view)
        {
            var state = RunManager.Instance?.State;
            if (state == null || costTable == null) return;

            bool isFree = ArchiveRewardContext.FreeDiePurchasePending; // design doc Section 7, Archive's "Acquisto Gratis"
            int cost = isFree ? 0 : costTable.GetNewDieCost(state.currentEchelon);
            if (!isFree && state.scrap < cost) return; // can't afford it - click silently does nothing

            state.scrap -= cost;
            if (isFree) ArchiveRewardContext.FreeDiePurchasePending = false; // spent - only the NEXT purchase after the reward is free, not every purchase this visit

            state.AddDieToPool(offer, fromRunUnlock: true); // acquired DURING this run - eligible for the Meta screen on defeat

            _offers.Remove(offer);
            _offerViews.Remove(view);
            Destroy(view.gameObject);
            // No auto-refill here on purpose - a bought (or freely obtained) slot stays
            // empty until the player pays for Reroll, which refreshes the whole shelf at once.

            RefreshCosts();
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

            RefreshCosts();
        }

        private void OnRerollShelfClicked()
        {
            var state = RunManager.Instance?.State;
            if (state == null || costTable == null) return;

            int cost = costTable.GetRerollShopCost(state.currentEchelon);
            if (state.scrap < cost) return;

            state.scrap -= cost;
            RollAllOffers();
            RefreshCosts();
        }

        private void RefreshCosts()
        {
            var state = RunManager.Instance?.State;
            if (state == null) return;

            if (scrapText != null) scrapText.text = $"Scrap: {state.scrap}";
            if (hpText != null) hpText.text = $"HP: {state.currentHp}/{state.maxHp}";

            int hpCost = costTable.GetIncreaseMaxHpCost(state.currentEchelon);
            int rerollCost = costTable.GetRerollShopCost(state.currentEchelon);

            if (increaseMaxHpCostText != null) increaseMaxHpCostText.text = $"{hpCost} Scrap";
            if (rerollCostText != null) rerollCostText.text = $"{rerollCost} Scrap";

            if (increaseMaxHpButton != null) increaseMaxHpButton.interactable = state.scrap >= hpCost;
            if (rerollButton != null) rerollButton.interactable = state.scrap >= rerollCost;
        }
    }
}

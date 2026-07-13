using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rollrate.Core;
using Rollrate.Data;

namespace Rollrate.Shop
{
    /// <summary>
    /// A single purchasable Shop offer: either a Module or a Die, never both.
    /// </summary>
    public class ShopOffer
    {
        public ModuleData module;
        public DieData die;

        public bool IsModule => module != null;
        public string DisplayName => IsModule ? module.displayName : die.displayName;
    }

    /// <summary>
    /// Handles the Merchant Node's purchasing services: a single unified
    /// offer board (up to Max Offers items, a random mix of Modules and Dice
    /// drawn from the current Grade's pool), plus HP repair and permanently
    /// raising Max HP. Costs come from a ShopCostTable, scaled by the
    /// current Grade.
    ///
    /// Clicking any offer buys it directly if there's enough Scrap - no
    /// separate "Buy" button needed, the offer itself is the button.
    /// Buying a Module installs it directly into its own fixed slot,
    /// replacing whatever was equipped there before (CHECKPOINT: simple
    /// direct-install behavior, no separate owned/equipped split for now).
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        [Header("Cost Table")]
        [SerializeField] private ShopCostTable costTable;

        [Header("Offer Pools (per-Grade - only the player's current Grade is offered)")]
        [SerializeField] private ShopOfferPools offerPools;

        [Header("Offer Settings")]
        [Tooltip("Up to this many offers appear at once, in a random Module/Die mix each time offers are (re)generated.")]
        [SerializeField] private int maxOffers = 6;
        [SerializeField] private int maxHpIncreasePerPurchase = 1;

        /// <summary>The current offer board. Fires whenever it changes (purchase, reroll, initial generation).</summary>
        public List<ShopOffer> CurrentOffers { get; private set; } = new List<ShopOffer>();

        /// <summary>Raised any time CurrentOffers changes, so the UI knows to redraw.</summary>
        public System.Action OnOffersChanged;

        private void Start()
        {
            if (RunManager.Instance == null)
            {
                Debug.LogError("[ShopController] RunManager.Instance is null - this scene needs RunManager already loaded " +
                                "(e.g. Play from the Combat/test scene first, then open the Shop additively). " +
                                "Playing this scene in isolation won't work.");
                return;
            }
            GenerateOffers();
        }

        public int GetModuleCost()
        {
            var state = RunManager.Instance.State;
            return ApplyDiscountIfPending(costTable.GetNewModuleCost(state.currentEchelon), state);
        }

        public int GetNewDieCost()
        {
            var state = RunManager.Instance.State;
            return ApplyDiscountIfPending(costTable.GetNewDieCost(state.currentEchelon), state);
        }

        public int GetOfferCost(ShopOffer offer) => offer.IsModule ? GetModuleCost() : GetNewDieCost();

        public int GetRepairHpCost(int amount)
        {
            var state = RunManager.Instance.State;
            return costTable.GetRepairHpCost(state.currentEchelon) * Mathf.Max(1, amount);
        }

        public int GetIncreaseMaxHpCost()
        {
            var state = RunManager.Instance.State;
            return costTable.GetIncreaseMaxHpCost(state.currentEchelon);
        }

        public int GetRerollCost()
        {
            var state = RunManager.Instance.State;
            return costTable.GetRerollShopCost(state.currentEchelon);
        }

        private int ApplyDiscountIfPending(int baseCost, GameState state)
        {
            if (state.pendingShopDiscountPercent > 0f)
            {
                return Mathf.RoundToInt(baseCost * (1f - state.pendingShopDiscountPercent));
            }
            return baseCost;
        }

        private void ConsumeDiscountIfPending(GameState state)
        {
            if (state.pendingShopDiscountPercent > 0f) state.pendingShopDiscountPercent = 0f;
        }

        /// <summary>True if this offer is a Module the player already owns (shown/blocked in the UI).</summary>
        public bool IsOfferOwned(ShopOffer offer)
        {
            return offer.IsModule && RunManager.Instance.State.OwnsModule(offer.module);
        }

        /// <summary>
        /// Buys the given offer. Dice are added straight to the pool/draw
        /// pile. Modules are added to the OWNED collection for their slot -
        /// NOT auto-equipped, unless that slot currently has nothing
        /// equipped yet (first module for an empty slot is a convenience
        /// auto-equip). Refuses to sell a Module already owned.
        /// </summary>
        public bool TryBuyOffer(ShopOffer offer)
        {
            var state = RunManager.Instance.State;

            if (IsOfferOwned(offer))
            {
                Debug.LogWarning($"[ShopController] {offer.DisplayName} is already owned - purchase blocked.");
                return false;
            }

            int cost = GetOfferCost(offer);
            if (state.scrap < cost) return false;

            state.scrap -= cost;
            ConsumeDiscountIfPending(state);

            if (offer.IsModule)
            {
                state.AddOwnedModule(offer.module);
                if (!state.installedModules.ContainsKey(offer.module.slot))
                {
                    state.EquipModule(offer.module); // convenience: auto-equip only if that slot was empty
                }
                Debug.Log($"[ShopController] Bought {offer.module.displayName} for {offer.module.slot} ({cost} Scrap) - added to owned modules.");
            }
            else
            {
                state.AddDieToPool(offer.die);
                Debug.Log($"[ShopController] Bought a new {offer.die.displayName} ({cost} Scrap). Pool size now {state.dicePool.Count}.");
            }

            CurrentOffers.Remove(offer);
            OnOffersChanged?.Invoke();
            return true;
        }

        /// <summary>Equips an already-owned module into its own slot, replacing whatever was equipped there.</summary>
        public void EquipOwnedModule(ModuleData module)
        {
            RunManager.Instance.State.EquipModule(module);
        }

        /// <summary>Returns every owned module for the given slot (for an equip/swap UI).</summary>
        public List<ModuleData> GetOwnedModulesForSlot(SlotType slot)
        {
            var state = RunManager.Instance.State;
            return state.ownedModules.TryGetValue(slot, out var owned) ? owned : new List<ModuleData>();
        }

        public bool TryRepairHp(int amount)
        {
            var state = RunManager.Instance.State;
            amount = Mathf.Min(amount, state.maxHp - state.currentHp);
            if (amount <= 0) return false;

            int cost = GetRepairHpCost(amount);
            if (state.scrap < cost) return false;

            state.scrap -= cost;
            state.currentHp += amount;
            Debug.Log($"[ShopController] Repaired {amount} HP ({cost} Scrap). HP now {state.currentHp}/{state.maxHp}.");
            return true;
        }

        public bool TryIncreaseMaxHp()
        {
            var state = RunManager.Instance.State;
            int cost = GetIncreaseMaxHpCost();
            if (state.scrap < cost) return false;

            state.scrap -= cost;
            state.maxHp += maxHpIncreasePerPurchase;
            state.currentHp += maxHpIncreasePerPurchase;
            Debug.Log($"[ShopController] Max HP increased by {maxHpIncreasePerPurchase} ({cost} Scrap). Max HP now {state.maxHp}.");
            return true;
        }

        public bool TryRerollOffers()
        {
            var state = RunManager.Instance.State;
            int cost = GetRerollCost();
            if (state.scrap < cost) return false;

            state.scrap -= cost;
            GenerateOffers();
            Debug.Log($"[ShopController] Rerolled offers ({cost} Scrap).");
            return true;
        }

        private void GenerateOffers()
        {
            var state = RunManager.Instance.State;
            GradeOfferPool gradePool = offerPools.GetForGrade(state.currentEchelon);

            ModuleData[] allGradeModules = gradePool.modules ?? new ModuleData[0];
            DieData[] availableDice = gradePool.dice ?? new DieData[0];

            // Prefer modules not yet owned, so the board doesn't repeat what
            // you already have. If every module in this Grade is already
            // owned, fall back to the full list (ShopOfferRowUI still shows
            // "Already Owned" and blocks the click) rather than showing fewer
            // than the requested offer count.
            ModuleData[] unownedModules = allGradeModules.Where(m => !state.OwnsModule(m)).ToArray();
            ModuleData[] availableModules = unownedModules.Length > 0 ? unownedModules : allGradeModules;

            int moduleCount;
            int dieCount;

            if (availableModules.Length == 0 && availableDice.Length == 0)
            {
                moduleCount = 0;
                dieCount = 0;
            }
            else if (availableModules.Length == 0)
            {
                moduleCount = 0;
                dieCount = maxOffers;
            }
            else if (availableDice.Length == 0)
            {
                moduleCount = maxOffers;
                dieCount = 0;
            }
            else
            {
                moduleCount = Random.Range(0, maxOffers + 1);
                dieCount = maxOffers - moduleCount;
            }

            var modules = PickRandom(availableModules, moduleCount);
            var dice = PickRandom(availableDice, dieCount);

            var offers = new List<ShopOffer>();
            foreach (ModuleData m in modules) offers.Add(new ShopOffer { module = m });
            foreach (DieData d in dice) offers.Add(new ShopOffer { die = d });

            CurrentOffers = offers.OrderBy(_ => Random.value).ToList();
            OnOffersChanged?.Invoke();
        }

        private List<T> PickRandom<T>(T[] pool, int count) where T : Object
        {
            var result = new List<T>();
            if (pool == null || pool.Length == 0 || count <= 0) return result;

            var poolCopy = new List<T>(pool);
            for (int i = 0; i < count; i++)
            {
                if (poolCopy.Count == 0)
                {
                    poolCopy = new List<T>(pool);
                }
                int index = Random.Range(0, poolCopy.Count);
                result.Add(poolCopy[index]);
                poolCopy.RemoveAt(index);
            }
            return result;
        }
    }
}

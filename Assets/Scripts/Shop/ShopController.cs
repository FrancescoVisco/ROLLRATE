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
        [Tooltip("When both dice and modules are available, each type gets at least this many of the offer slots (e.g. 2 with Max Offers 6 = a 2-4 or 3-3 or 4-2 split, never all-one-type).")]
        [SerializeField] private int minOffersPerType = 2;
        [Tooltip("Relative weight for modules NOT yet owned - higher means they show up far more often than owned ones.")]
        [SerializeField] private int unownedModuleWeight = 5;
        [Tooltip("Relative weight for modules already owned - low but non-zero, so they can still occasionally appear instead of being fully excluded.")]
        [SerializeField] private int ownedModuleWeight = 1;
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

        /// <summary>
        /// True if this offer is a Module the player already owns at least
        /// one copy of - informational only (shown as "(Owned xN)" in the
        /// UI), does NOT block purchase. Owning duplicates is intentional:
        /// spares can be Dismantled for Scrap without losing your equipped copy.
        /// </summary>
        public bool IsOfferOwned(ShopOffer offer)
        {
            return offer.IsModule && RunManager.Instance.State.OwnsModule(offer.module);
        }

        /// <summary>How many copies of this offer's module the player already owns (0 for dice offers).</summary>
        public int GetOfferOwnedCount(ShopOffer offer)
        {
            return offer.IsModule ? RunManager.Instance.State.GetOwnedModuleCount(offer.module) : 0;
        }

        /// <summary>
        /// Buys the given offer. Dice are added straight to the pool/draw
        /// pile. Modules are added to the OWNED collection for their slot -
        /// NOT auto-equipped, unless that slot currently has nothing
        /// equipped yet (first module for an empty slot is a convenience
        /// auto-equip). Buying a module you already own is allowed on
        /// purpose - see GetOfferOwnedCount.
        /// </summary>
        public bool TryBuyOffer(ShopOffer offer)
        {
            var state = RunManager.Instance.State;

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
                Debug.Log($"[ShopController] Bought {offer.module.displayName} for {offer.module.slot} ({cost} Scrap) - now own {state.GetOwnedModuleCount(offer.module)}.");
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

        // NOTE: Dismantling now lives entirely in the dedicated Dismantle
        // Node (Rollrate.Dismantle.DismantleController), using a flat
        // Scrap-per-Grade table instead of a percentage of purchase cost.
        // It's no longer part of the Shop at all - see DismantleController.

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

        /// <summary>
        /// Rest Node's free heal: recovers half of the missing HP (rounded
        /// up), no Scrap cost. Intended as a single action per node visit -
        /// the calling UI (RestUI) is responsible for disabling its button
        /// after one use, this method itself doesn't track "already rested".
        /// </summary>
        public bool TryFreeRest()
        {
            var state = RunManager.Instance.State;
            int missing = state.maxHp - state.currentHp;
            if (missing <= 0) return false;

            int healAmount = Mathf.CeilToInt(missing / 2f);
            state.currentHp = Mathf.Min(state.maxHp, state.currentHp + healAmount);
            Debug.Log($"[ShopController] Free Rest: +{healAmount} HP. HP now {state.currentHp}/{state.maxHp}.");
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
            GradeOfferPool gradePool = offerPools.GetForGradeWithUnlocks(state.currentEchelon);

            ModuleData[] allGradeModules = gradePool.modules ?? new ModuleData[0];
            DieData[] availableDice = gradePool.dice ?? new DieData[0];

            // Weighted pool instead of a hard exclusion: modules you don't
            // own yet are far more likely to appear, but an already-owned
            // one can still show up occasionally (small chance) instead of
            // being completely filtered out.
            var weightedModules = new List<ModuleData>();
            foreach (ModuleData m in allGradeModules)
            {
                int weight = state.OwnsModule(m) ? ownedModuleWeight : unownedModuleWeight;
                for (int i = 0; i < weight; i++) weightedModules.Add(m);
            }
            ModuleData[] availableModules = weightedModules.ToArray();

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
                // Force a real mix: never all-dice or all-modules when both
                // types are actually available - each type gets between
                // minOffersPerType and (maxOffers - minOffersPerType) slots.
                int minCount = Mathf.Clamp(minOffersPerType, 0, maxOffers / 2);
                int maxCount = maxOffers - minCount;
                moduleCount = Random.Range(minCount, maxCount + 1);
                dieCount = maxOffers - moduleCount;
            }

            var modules = PickRandomWeightedModules(weightedModules, moduleCount);
            var dice = PickRandom(availableDice, dieCount);

            var offers = new List<ShopOffer>();
            foreach (ModuleData m in modules) offers.Add(new ShopOffer { module = m });
            foreach (DieData d in dice) offers.Add(new ShopOffer { die = d });

            CurrentOffers = offers.OrderBy(_ => Random.value).ToList();
            OnOffersChanged?.Invoke();
        }

        /// <summary>
        /// Picks up to `count` DISTINCT modules from a weighted candidate
        /// list (which may contain the same module multiple times, to bias
        /// probability toward unowned ones - see GenerateOffers). Every copy
        /// of a picked module is removed after selection, so the same
        /// module can't appear twice on the same offer board.
        /// </summary>
        private List<ModuleData> PickRandomWeightedModules(List<ModuleData> weightedPool, int count)
        {
            var result = new List<ModuleData>();
            if (weightedPool == null || weightedPool.Count == 0 || count <= 0) return result;

            var poolCopy = new List<ModuleData>(weightedPool);
            for (int i = 0; i < count && poolCopy.Count > 0; i++)
            {
                int index = Random.Range(0, poolCopy.Count);
                ModuleData picked = poolCopy[index];
                result.Add(picked);
                poolCopy.RemoveAll(m => m == picked); // remove every weighted copy - no repeats on this board
            }
            return result;
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

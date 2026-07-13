using System;
using System.Collections.Generic;
using UnityEngine;
using Rollrate.Data;

namespace Rollrate.Core
{
    /// <summary>
    /// Holds the full state of the current run. This is NOT a ScriptableObject:
    /// it's a plain runtime object that lives for the duration of a play session
    /// and gets rebuilt/reset on Fragmentation (defeat).
    ///
    /// Persistent fields (survive defeat) are marked below.
    /// Non-persistent fields reset to their starting values on Fragmentation.
    /// </summary>
    [Serializable]
    public class GameState
    {
        [Header("Core Die (PERSISTENT across runs)")]
        public DieData coreDie;

        [Header("Scrap (PERSISTENT across runs)")]
        public int scrap;

        [Header("Dice Pool - total owned dice (reset on Fragmentation)")]
        [Tooltip("The master list of every die owned this run. DrawPile + DiscardPile together always contain exactly these same dice - dicePool itself isn't drawn from directly during combat.")]
        public List<DieData> dicePool = new List<DieData>();

        [Header("Draw/Discard Piles (reset on Fragmentation)")]
        [Tooltip("Dice not yet drawn this cycle. Each Roll draws up to Hand Size from here.")]
        public List<DieData> drawPile = new List<DieData>();
        [Tooltip("Dice already drawn and resolved this cycle (used or not). Reshuffled into DrawPile once DrawPile runs out.")]
        public List<DieData> discardPile = new List<DieData>();

        [Header("Installed Modules per Slot - currently EQUIPPED (reset on Fragmentation)")]
        public Dictionary<SlotType, ModuleData> installedModules = new Dictionary<SlotType, ModuleData>();

        [Header("Owned Modules per Slot - everything bought, not necessarily equipped (reset on Fragmentation)")]
        public Dictionary<SlotType, List<ModuleData>> ownedModules = new Dictionary<SlotType, List<ModuleData>>();

        [Header("HP (reset on Fragmentation)")]
        public int currentHp;
        public int maxHp;

        [Header("Progress (reset on Fragmentation)")]
        public int currentEchelon = 1; // Grade I -> V
        public int currentPage = 1;    // Page 1-3 within the Echelon

        [Header("Deferred Turn Effects (reset on Fragmentation)")]
        [Tooltip("Changeover: 10 Charges = +1 Die added to the pool.")]
        public int changeoverCharges;
        [Tooltip("Changeover: dice queued here are rolled for exactly ONE turn (the next Roll), then removed from the game entirely - never added to the pool/deck, never discarded.")]
        public List<DieData> pendingChangeoverBonusDice = new List<DieData>();
        [Tooltip("Overload: flat bonus applied to the first Power die placed next turn.")]
        public int pendingNextTurnPowerBonus;
        [Tooltip("Aiming: % reduction applied to the enemy Threshold next turn (0 = none).")]
        public float pendingThresholdReductionPercent;
        [Tooltip("Scrap (Odd): % discount applied to the next Module or Die purchased at the Shop (0 = none).")]
        public float pendingShopDiscountPercent;

        /// <summary>
        /// Sets up a brand new run: default HP, empty pool except the Core Die,
        /// no modules installed/owned. Scrap and Core Die evolution level are
        /// NOT touched here - call this only after applying persistence rules on defeat.
        /// </summary>
        public void ResetForNewRun(DieData startingCoreDie, int startingHp)
        {
            coreDie = startingCoreDie;
            currentHp = startingHp;
            maxHp = startingHp;
            currentEchelon = 1;
            currentPage = 1;
            dicePool.Clear();
            drawPile.Clear();
            discardPile.Clear();
            installedModules.Clear();
            ownedModules.Clear();
            changeoverCharges = 0;
            pendingChangeoverBonusDice.Clear();
            pendingNextTurnPowerBonus = 0;
            pendingThresholdReductionPercent = 0f;
            pendingShopDiscountPercent = 0f;
        }

        /// <summary>
        /// Applies the Fragmentation rule: Core Die level and Scrap persist,
        /// everything else (pool, modules, HP, progress) resets.
        /// </summary>
        public void ApplyFragmentation(int startingHp)
        {
            // coreDie and scrap are intentionally left untouched
            currentHp = startingHp;
            maxHp = startingHp;
            currentEchelon = 1;
            currentPage = 1;
            dicePool.Clear();
            drawPile.Clear();
            discardPile.Clear();
            installedModules.Clear();
            ownedModules.Clear();
            changeoverCharges = 0;
            pendingChangeoverBonusDice.Clear();
            pendingNextTurnPowerBonus = 0;
            pendingThresholdReductionPercent = 0f;
            pendingShopDiscountPercent = 0f;
        }

        /// <summary>
        /// Adds a newly acquired die to both the master ownership list and
        /// the draw pile (so it's available to be drawn soon).
        /// </summary>
        public void AddDieToPool(DieData die)
        {
            dicePool.Add(die);
            drawPile.Add(die);
        }

        /// <summary>
        /// Replaces every occurrence of oldDie with newDie across dicePool
        /// and whichever pile currently holds it (Evolve Die at the Shop).
        /// </summary>
        public void ReplaceDieEverywhere(DieData oldDie, DieData newDie)
        {
            ReplaceFirst(dicePool, oldDie, newDie);
            ReplaceFirst(drawPile, oldDie, newDie);
            ReplaceFirst(discardPile, oldDie, newDie);
        }

        private void ReplaceFirst(List<DieData> list, DieData oldDie, DieData newDie)
        {
            int index = list.IndexOf(oldDie);
            if (index >= 0) list[index] = newDie;
        }

        /// <summary>True if the given module is already owned for its own slot.</summary>
        public bool OwnsModule(ModuleData module)
        {
            return ownedModules.TryGetValue(module.slot, out var owned) && owned.Contains(module);
        }

        /// <summary>Adds a module to the owned collection for its slot (does not equip it).</summary>
        public void AddOwnedModule(ModuleData module)
        {
            if (!ownedModules.TryGetValue(module.slot, out var owned))
            {
                owned = new List<ModuleData>();
                ownedModules[module.slot] = owned;
            }
            if (!owned.Contains(module)) owned.Add(module);
        }

        /// <summary>
        /// Equips an already-owned module into its own fixed slot,
        /// replacing whatever was equipped there before. Free - no cost
        /// here; whichever UI calls this (e.g. the Equipment node) decides
        /// whether a cost applies. Does nothing if the module isn't owned.
        /// </summary>
        public void EquipModule(ModuleData module)
        {
            if (module == null || !OwnsModule(module)) return;
            installedModules[module.slot] = module;
        }

        // --- Deck (Draw Pile / Discard Pile) ---

        /// <summary>
        /// Shuffles every owned die into a fresh Draw Pile and empties the
        /// Discard Pile. Call this once at the start of each fight.
        /// </summary>
        public void InitializeDeckForFight()
        {
            drawPile = new List<DieData>(dicePool);
            ShuffleList(drawPile);
            discardPile.Clear();
        }

        /// <summary>
        /// Draws up to `count` dice from the Draw Pile. If it runs out
        /// mid-draw, the Discard Pile is reshuffled into a new Draw Pile
        /// automatically and drawing continues. Returns fewer than `count`
        /// only if the player owns fewer dice in total than requested.
        /// </summary>
        public List<DieData> DrawHand(int count)
        {
            var hand = new List<DieData>();
            for (int i = 0; i < count; i++)
            {
                if (drawPile.Count == 0)
                {
                    if (discardPile.Count == 0) break; // no more dice owned at all
                    drawPile = new List<DieData>(discardPile);
                    ShuffleList(drawPile);
                    discardPile.Clear();
                }
                hand.Add(drawPile[0]);
                drawPile.RemoveAt(0);
            }
            return hand;
        }

        /// <summary>Moves a set of dice (an entire turn's drawn hand) into the Discard Pile.</summary>
        public void DiscardHand(List<DieData> hand)
        {
            if (hand != null) discardPile.AddRange(hand);
        }

        /// <summary>
        /// Permanently removes one die from the game entirely (Sovereign's
        /// Delete ability) - not discarded, just gone. Removes it from
        /// whichever pile currently holds it, and from the master ownership list.
        /// </summary>
        public void RemoveDiePermanently(DieData die)
        {
            dicePool.Remove(die);
            drawPile.Remove(die);
            discardPile.Remove(die);
        }

        private static void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public bool IsDefeated => currentHp <= 0;
    }
}

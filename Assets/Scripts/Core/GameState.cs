using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rollrate.Data;

namespace Rollrate.Core
{
    /// <summary>
    /// Holds the full state of the current run. Not a ScriptableObject:
    /// a plain runtime object that lives for the play session and gets
    /// rebuilt/reset on Fragmentation (defeat).
    ///
    /// DICE-TYPE REDESIGN: the pool is now a list of DieInstance (each
    /// owned die has its own permanent DieType and its own Effects), not
    /// a flat list of DieData "kinds". Modules/Slots/Levels are gone
    /// entirely, replaced by Effects attached directly to dice.
    ///
    /// HAND SIZE REINTRODUCED (design doc Section 4): each turn's Roll
    /// draws a fixed-size Hand from a Draw Pile built from ALL owned
    /// dice, shuffled; resolved dice go to a Discard Pile; once the Draw
    /// Pile runs out mid-draw, the Discard Pile is reshuffled into a
    /// fresh Draw Pile and drawing continues. The deck is rebuilt fresh
    /// at the start of every FIGHT (not every turn, not every run) - see
    /// InitializeDeckForFight.
    /// </summary>
    [Serializable]
    public class GameState
    {
        [Header("Core Die (PERSISTENT across runs)")]
        public DieData coreDie;

        [Header("Scrap (PERSISTENT across runs)")]
        public int scrap;

        [Header("Dice Pool - every owned die, each its own instance (reset on Fragmentation)")]
        public List<DieInstance> dicePool = new List<DieInstance>();

        [Header("Draw/Discard Pile (rebuilt at the start of every fight)")]
        [Tooltip("Dice not yet drawn this fight's deck cycle. Each turn's Roll draws Hand Size from here.")]
        public List<DieInstance> drawPile = new List<DieInstance>();
        [Tooltip("Dice already drawn and resolved this fight. Reshuffled into drawPile once drawPile runs out.")]
        public List<DieInstance> discardPile = new List<DieInstance>();

        [Header("HP (reset on Fragmentation)")]
        public int currentHp;
        public int maxHp;

        [Header("Progress (reset on Fragmentation)")]
        public int currentEchelon = 1; // Grade I -> V
        public int currentPage = 1;    // Page 1-3 within the Echelon

        [Header("Meta tracking (reset on Fragmentation, consumed on defeat)")]
        [Tooltip("Every DieInstance added to the pool THIS run via Dice Dealer, Archive, or Furnace - the pool the Meta end-of-run screen picks its 3 candidates from. Dice present at the start of the run (including any inherited from a previous Meta pick) are NOT added here.")]
        public List<DieInstance> unlockedThisRun = new List<DieInstance>();

        [Header("Per-fight transient state (reset at the start of every fight)")]
        [Tooltip("Dice temporarily disabled for the CURRENT fight only (e.g. Sovereign's [Delete]) - excluded from the deck, automatically available again next fight.")]
        public HashSet<DieInstance> disabledThisFight = new HashSet<DieInstance>();

        [Tooltip("Reverb Effect (Section 5): a value pending for a SPECIFIC die instance's next turn, applied automatically (added to its rolledValue) the next time that instance is drawn into a Hand - see TurnController.RollAll.")]
        public Dictionary<DieInstance, float> pendingNextTurnBonus = new Dictionary<DieInstance, float>();

        [Tooltip("Reverb (Echo Effect): a bonus queued for a SPECIFIC owned die, to apply automatically the next time that exact die is drawn into a Hand - design doc Section 5, Reverb: 'il valore si applica sia questo turno che il prossimo'.")]
        public Dictionary<DieInstance, int> pendingReverbBonus = new Dictionary<DieInstance, int>();

        /// <summary>
        /// Sets up a brand new run: default HP, empty pool except the Core Die.
        /// Scrap and Core Die evolution are NOT touched here - call this only
        /// after applying persistence rules on defeat.
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
            unlockedThisRun.Clear();
            disabledThisFight.Clear();
            pendingNextTurnBonus.Clear();
            pendingReverbBonus.Clear();
        }

        /// <summary>
        /// Applies the Fragmentation rule: Core Die and Scrap persist,
        /// everything else resets - EXCEPT the single die chosen at the
        /// Meta end-of-run screen (see MetaController), which the caller
        /// adds back to the fresh pool AFTER calling this.
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
            unlockedThisRun.Clear();
            disabledThisFight.Clear();
            pendingNextTurnBonus.Clear();
            pendingReverbBonus.Clear();
        }

        /// <summary>
        /// Call once at the start of every fight: clears the per-fight
        /// disable list AND rebuilds the deck (shuffles every owned,
        /// non-disabled die into a fresh Draw Pile, empties the Discard Pile).
        /// </summary>
        public void ResetForNewFight()
        {
            disabledThisFight.Clear();
            pendingNextTurnBonus.Clear();
            pendingReverbBonus.Clear();
            InitializeDeckForFight();
        }

        /// <summary>Shuffles every owned, non-disabled die into a fresh Draw Pile and empties the Discard Pile.</summary>
        public void InitializeDeckForFight()
        {
            drawPile = new List<DieInstance>(dicePool.Where(d => !disabledThisFight.Contains(d)));
            ShuffleList(drawPile);
            discardPile.Clear();
        }

        /// <summary>
        /// Draws up to `count` dice from the Draw Pile for this turn's
        /// Hand. If it runs out mid-draw, the Discard Pile is reshuffled
        /// into a new Draw Pile automatically and drawing continues.
        /// Returns fewer than `count` only if the player owns fewer
        /// (non-disabled) dice in total than requested.
        /// </summary>
        public List<DieInstance> DrawHand(int count)
        {
            var hand = new List<DieInstance>();
            for (int i = 0; i < count; i++)
            {
                // Skip disabled dice (Sovereign's [Delete]) if they somehow ended up in
                // the piles - reshuffle around them rather than drawing them.
                while (drawPile.Count > 0 && disabledThisFight.Contains(drawPile[0]))
                {
                    discardPile.Add(drawPile[0]);
                    drawPile.RemoveAt(0);
                }

                if (drawPile.Count == 0)
                {
                    var redrawable = discardPile.Where(d => !disabledThisFight.Contains(d)).ToList();
                    if (redrawable.Count == 0) break; // nothing left to draw at all
                    drawPile = redrawable;
                    ShuffleList(drawPile);
                    discardPile = discardPile.Where(d => disabledThisFight.Contains(d)).ToList(); // keep disabled ones parked here, out of rotation
                    continue;
                }

                hand.Add(drawPile[0]);
                drawPile.RemoveAt(0);
            }
            return hand;
        }

        /// <summary>Moves a set of dice (a turn's drawn Hand, once resolved) into the Discard Pile.</summary>
        public void DiscardHand(List<DieInstance> hand)
        {
            if (hand != null) discardPile.AddRange(hand);
        }

        /// <summary>
        /// Adds a newly acquired die to the pool. Pass fromRunUnlock=true
        /// for anything gained DURING a run (Dice Dealer, Archive, Furnace
        /// output) so it's eligible for the Meta end-of-run pick; pass
        /// false for the starting pool itself (run setup, or the single
        /// die inherited from a previous Meta pick).
        /// </summary>
        public void AddDieToPool(DieInstance die, bool fromRunUnlock)
        {
            dicePool.Add(die);
            if (fromRunUnlock) unlockedThisRun.Add(die);
        }

        /// <summary>Evolves a die instance to its next tier DieData (Shop's Evoluzione Dado, or Furnace's same-Grade Fusion result), keeping its Type and Effects untouched. No-op if already at max tier.</summary>
        public void EvolveDieInstance(DieInstance instance)
        {
            if (instance == null || instance.data == null || instance.data.nextTier == null) return;
            instance.data = instance.data.nextTier;
        }

        /// <summary>
        /// Removes one die from the game entirely and permanently (Test
        /// di Tributo's forced loss, or Furnace consuming its two source
        /// dice after producing the fused result).
        /// </summary>
        public void RemoveDiePermanently(DieInstance die)
        {
            dicePool.Remove(die);
            drawPile.Remove(die);
            discardPile.Remove(die);
            unlockedThisRun.Remove(die);
            disabledThisFight.Remove(die);
        }

        /// <summary>True if dismantling/removing a die is allowed: keep at least 4 dice in the pool after removal (Test di Tributo, etc.).</summary>
        public bool CanRemoveDie()
        {
            return dicePool.Count > 4;
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

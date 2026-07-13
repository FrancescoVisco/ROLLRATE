using System.Collections.Generic;
using UnityEngine;
using Rollrate.Core;
using Rollrate.Data;
using Rollrate.Combat;

namespace Rollrate.UI
{
    /// <summary>
    /// Rolls the player's Core Die, the dice pool, and the enemy's Inhibitor
    /// Die all at once (SET-UP + ROLL happen together, per the design's turn
    /// loop). The Core Die and the enemy's Inhibitor Die are both shown as
    /// locked (non-draggable) dice in their own fixed display slots. Pool
    /// dice are spawned into the hand container as normal draggable dice.
    /// </summary>
    public class DiceRoller : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject diePrefab;      // prefab with Image + CanvasGroup + DraggableDie
        [SerializeField] private Transform handContainer;   // UI panel where pool dice appear
        [SerializeField] private Transform coreDisplaySlot; // fixed UI spot where the Core Die is shown
        [SerializeField] private Transform enemyDieDisplaySlot; // fixed UI spot where the enemy's Inhibitor Die is shown
        [SerializeField] private EnemyController enemyController; // rolls its Inhibitor Die alongside the player
        [SerializeField] private CombatController combatController; // notified after rolling, to reset turn gating and refresh slot labels
        [SerializeField] private GameHUD gameHUD; // refreshed after rolling, to show updated HP
        [SerializeField] private UnityEngine.UI.Button rollButton; // disabled after rolling, re-enabled by CombatController once the turn resolves

        [Header("Core Die Visual")]
        [SerializeField] private Color coreDieColor = new Color(1f, 0.85f, 0.2f); // gold-ish, matches the "sfarzo" theme

        [Header("Enemy Inhibitor Die Visual")]
        [SerializeField] private Color enemyDieColor = new Color(0.75f, 0.2f, 0.3f); // reddish, reads as "hostile/enemy"

        [Header("Pool Layout (free positioning, relative to HandContainer's own center)")]
        [SerializeField] private float spacingX = 100f;
        [SerializeField] private float startX = -200f;
        [SerializeField] private float fixedY = 0f;

        [Header("Hand Size")]
        [Tooltip("Read from RunManager.HandSize at runtime - kept here only as a fallback if RunManager isn't found.")]
        [SerializeField] private int fallbackHandSize = 6;

        private readonly List<DraggableDie> _currentHand = new List<DraggableDie>();
        private List<DieData> _currentHandDice = new List<DieData>(); // the actual DieData drawn this turn, for discarding at turn end

        /// <summary>
        /// Returns the pool dice currently sitting unplaced in the hand
        /// (not dragged into any slot). Used by Second Chance's reroll
        /// heuristic, which needs a die to reroll automatically.
        /// </summary>
        public List<DraggableDie> GetUnplacedHandDice()
        {
            var result = new List<DraggableDie>();
            foreach (var die in _currentHand)
            {
                if (die != null && die.transform.parent == handContainer)
                {
                    result.Add(die);
                }
            }
            return result;
        }
        private DraggableDie _currentCoreInstance;
        private DraggableDie _currentEnemyDieInstance;

        /// <summary>The Core Die's rolled value from the last RollAllDice() call.</summary>
        public int LastCoreRolledValue { get; private set; }

        /// <summary>
        /// Rolls the Core Die, the dice pool, and the enemy's Inhibitor Die.
        /// The Core Die and the Inhibitor Die each go into their own fixed
        /// display slot as locked (non-draggable) pieces; pool dice go into
        /// the hand container as draggable pieces.
        /// </summary>
        /// <summary>Re-enables the Roll button - called by CombatController once the turn resolves.</summary>
        public void SetRollButtonInteractable(bool interactable)
        {
            if (rollButton != null) rollButton.interactable = interactable;
        }

        public void RollAllDice()
        {
            // Safety net: if a hand from a previous, unresolved turn is
            // still tracked (shouldn't happen once the Roll button is
            // properly disabled mid-turn, but this protects deck/discard
            // integrity even if RollAllDice() is ever called again early),
            // discard it first instead of silently losing those dice from
            // both piles.
            if (_currentHandDice.Count > 0)
            {
                Debug.LogWarning("[DiceRoller] RollAllDice() called again before the previous hand was discarded - discarding it now to avoid losing dice from the deck.");
                RunManager.Instance.State.DiscardHand(_currentHandDice);
                _currentHandDice = new List<DieData>();
            }

            ClearHand();
            ClearCore();
            ClearEnemyDie();

            var state = RunManager.Instance.State;

            // Core Die - fixed display, locked (never draggable)
            if (state.coreDie != null)
            {
                int coreRolled = Random.Range(1, state.coreDie.faces + 1);
                LastCoreRolledValue = coreRolled;
                SpawnCore(state.coreDie, coreRolled);
            }

            // SET-UP: the enemy rolls its Inhibitor Die at the same time -
            // shown as a locked die too, right next to the Core, so the
            // player can see it before placing anything.
            if (enemyController != null)
            {
                enemyController.RollInhibitor();
                if (enemyController.InhibitorDieType != null)
                {
                    SpawnEnemyDie(enemyController.InhibitorDieType, enemyController.LastInhibitedValue);
                }
            }

            // Pool dice - draw a fresh hand from the Draw Pile (reshuffling
            // the Discard Pile in automatically if needed). If the enemy
            // (Sovereign) has a locked "destroy value", any drawn die
            // rolling that exact value is permanently removed from the
            // game instead of being spawned - and isn't discarded either,
            // since it no longer exists.
            _currentHandDice = state.DrawHand(RunManager.Instance != null ? RunManager.Instance.HandSize : fallbackHandSize);
            var toDiscard = new List<DieData>();
            int spawnIndex = 0;

            foreach (DieData die in _currentHandDice)
            {
                if (die == null) continue;

                int rolled = Random.Range(1, die.faces + 1);

                bool destroyedBySovereign = enemyController != null
                    && enemyController.PersistentDestroyValue >= 0
                    && rolled == enemyController.PersistentDestroyValue;

                if (destroyedBySovereign)
                {
                    Debug.Log($"[DiceRoller] Sovereign destroyed a {die.displayName} die (rolled {rolled}, matches the locked value). Permanently removed from the game.");
                    state.RemoveDiePermanently(die);
                    continue; // not discarded (it's gone), not spawned
                }

                toDiscard.Add(die);

                bool inhibited = enemyController != null && enemyController.LastInhibitedValue == rolled;
                SpawnPoolDie(die, rolled, spawnIndex, inhibited);
                spawnIndex++;
            }

            _currentHandDice = toDiscard; // only the survivors get discarded at turn end

            // Notify dependent systems: turn gating resets, slot labels and
            // HUD (HP) refresh to reflect this new turn.
            combatController?.NotifyDiceRolled();
            gameHUD?.RefreshStats();

            // Block re-rolling mid-turn: CombatController re-enables this
            // once the turn is actually resolved.
            if (rollButton != null) rollButton.interactable = false;
        }

        /// <summary>
        /// Moves this turn's drawn hand into the Discard Pile. Call this
        /// once the turn has been resolved (CombatController does this at
        /// the end of ResolveTurn, alongside setting the "needs reroll" gate).
        /// </summary>
        public void DiscardCurrentHand()
        {
            var state = RunManager.Instance.State;
            state.DiscardHand(_currentHandDice);
            _currentHandDice = new List<DieData>();
        }

        private void SpawnCore(DieData die, int rolledValue)
        {
            GameObject instance = Instantiate(diePrefab, coreDisplaySlot);
            var draggable = instance.GetComponent<DraggableDie>();
            draggable.Setup(die, rolledValue, locked: true);

            var rect = instance.GetComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;

            var image = instance.GetComponent<UnityEngine.UI.Image>();
            if (image != null) image.color = coreDieColor;

            _currentCoreInstance = draggable;
        }

        private void SpawnEnemyDie(DieData die, int rolledValue)
        {
            if (enemyDieDisplaySlot == null) return;

            GameObject instance = Instantiate(diePrefab, enemyDieDisplaySlot);
            var draggable = instance.GetComponent<DraggableDie>();
            draggable.Setup(die, rolledValue, locked: true);

            var rect = instance.GetComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;

            var image = instance.GetComponent<UnityEngine.UI.Image>();
            if (image != null) image.color = enemyDieColor;

            _currentEnemyDieInstance = draggable;
        }

        private void SpawnPoolDie(DieData die, int rolledValue, int index, bool inhibited = false)
        {
            GameObject instance = Instantiate(diePrefab, handContainer);
            var draggable = instance.GetComponent<DraggableDie>();
            draggable.Setup(die, rolledValue, locked: false, inhibited: inhibited);

            var rect = instance.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(startX + index * spacingX, fixedY);

            _currentHand.Add(draggable);
        }

        private void ClearHand()
        {
            foreach (var die in _currentHand)
            {
                if (die != null) Destroy(die.gameObject);
            }
            _currentHand.Clear();
        }

        private void ClearCore()
        {
            if (_currentCoreInstance != null) Destroy(_currentCoreInstance.gameObject);
            _currentCoreInstance = null;
        }

        private void ClearEnemyDie()
        {
            if (_currentEnemyDieInstance != null) Destroy(_currentEnemyDieInstance.gameObject);
            _currentEnemyDieInstance = null;
        }
    }
}

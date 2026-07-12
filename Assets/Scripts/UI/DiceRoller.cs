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

        [Header("Core Die Visual")]
        [SerializeField] private Color coreDieColor = new Color(1f, 0.85f, 0.2f); // gold-ish, matches the "sfarzo" theme

        [Header("Enemy Inhibitor Die Visual")]
        [SerializeField] private Color enemyDieColor = new Color(0.75f, 0.2f, 0.3f); // reddish, reads as "hostile/enemy"

        [Header("Pool Layout (free positioning, relative to HandContainer's own center)")]
        [SerializeField] private float spacingX = 100f;
        [SerializeField] private float startX = -200f;
        [SerializeField] private float fixedY = 0f;

        private readonly List<DraggableDie> _currentHand = new List<DraggableDie>();
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
        public void RollAllDice()
        {
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

            // Pool dice - normal draggable dice, freely positioned in a row.
            // If the enemy (Sovereign) has a locked "destroy value", any
            // pool die rolling that exact value is permanently removed from
            // the pool instead of being spawned.
            var poolSnapshot = new List<DieData>(state.dicePool);
            state.dicePool.Clear();
            int spawnIndex = 0;

            foreach (DieData die in poolSnapshot)
            {
                if (die == null) continue;

                int rolled = Random.Range(1, die.faces + 1);

                bool destroyedBySovereign = enemyController != null
                    && enemyController.PersistentDestroyValue >= 0
                    && rolled == enemyController.PersistentDestroyValue;

                if (destroyedBySovereign)
                {
                    Debug.Log($"[DiceRoller] Sovereign destroyed a {die.displayName} die (rolled {rolled}, matches the locked value). Permanently removed from the pool.");
                    continue; // not re-added to state.dicePool, not spawned
                }

                state.dicePool.Add(die); // keep it for future turns

                bool inhibited = enemyController != null && enemyController.LastInhibitedValue == rolled;
                SpawnPoolDie(die, rolled, spawnIndex, inhibited);
                spawnIndex++;
            }

            // Notify dependent systems: turn gating resets, slot labels and
            // HUD (HP) refresh to reflect this new turn.
            combatController?.NotifyDiceRolled();
            gameHUD?.RefreshStats();
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

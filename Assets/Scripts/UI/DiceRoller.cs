using System.Collections.Generic;
using UnityEngine;
using Rollrate.Core;
using Rollrate.Data;

namespace Rollrate.UI
{
    /// <summary>
    /// Rolls the player's Core Die and dice pool at the start of a turn.
    /// The Core Die is spawned in a fixed display slot (never draggable into
    /// a board slot - see DraggableDie.isCoreDie). Pool dice are spawned into
    /// the hand container as normal draggable dice.
    /// </summary>
    public class DiceRoller : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject diePrefab;      // prefab with Image + CanvasGroup + DraggableDie
        [SerializeField] private Transform handContainer;   // UI panel where pool dice appear
        [SerializeField] private Transform coreDisplaySlot; // fixed UI spot where the Core Die is shown

        [Header("Core Die Visual")]
        [SerializeField] private Color coreDieColor = new Color(1f, 0.85f, 0.2f); // gold-ish, matches the "sfarzo" theme

        [Header("Pool Layout (free positioning, relative to HandContainer's own center)")]
        [SerializeField] private float spacingX = 100f;
        [SerializeField] private float startX = -200f;
        [SerializeField] private float fixedY = 0f;

        private readonly List<DraggableDie> _currentHand = new List<DraggableDie>();
        private DraggableDie _currentCoreInstance;

        /// <summary>The Core Die's rolled value from the last RollAllDice() call.</summary>
        public int LastCoreRolledValue { get; private set; }

        /// <summary>
        /// Rolls the Core Die plus every die in the pool. The Core Die goes
        /// into its own fixed display slot; pool dice go into the hand
        /// container as draggable pieces.
        /// </summary>
        public void RollAllDice()
        {
            ClearHand();
            ClearCore();

            var state = RunManager.Instance.State;

            // Core Die - fixed display, not draggable into slots
            if (state.coreDie != null)
            {
                int coreRolled = Random.Range(1, state.coreDie.faces + 1);
                LastCoreRolledValue = coreRolled;
                SpawnCore(state.coreDie, coreRolled);
            }

            // Pool dice - normal draggable dice, freely positioned in a row
            for (int i = 0; i < state.dicePool.Count; i++)
            {
                DieData die = state.dicePool[i];
                if (die == null) continue;

                int rolled = Random.Range(1, die.faces + 1);
                SpawnPoolDie(die, rolled, i);
            }
        }

        private void SpawnCore(DieData die, int rolledValue)
        {
            GameObject instance = Instantiate(diePrefab, coreDisplaySlot);
            var draggable = instance.GetComponent<DraggableDie>();
            draggable.Setup(die, rolledValue, isCore: true);

            var rect = instance.GetComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;

            var image = instance.GetComponent<UnityEngine.UI.Image>();
            if (image != null) image.color = coreDieColor;

            _currentCoreInstance = draggable;
        }

        private void SpawnPoolDie(DieData die, int rolledValue, int index)
        {
            GameObject instance = Instantiate(diePrefab, handContainer);
            var draggable = instance.GetComponent<DraggableDie>();
            draggable.Setup(die, rolledValue, isCore: false);

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
    }
}


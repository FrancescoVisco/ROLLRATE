using System.Collections;
using UnityEngine;
using TMPro;
using Rollrate.Core;
using Rollrate.Combat;

namespace Rollrate.UI
{
    /// <summary>
    /// Displays the player's HP, the enemy's HP, and short status messages
    /// (e.g. "Roll dice to continue"). The enemy's Inhibitor Die itself is
    /// shown as a visible die by DiceRoller, not here - this only reflects
    /// numeric HP values and text status.
    /// Call RefreshStats() any time HP might have changed.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EnemyController enemyController;
        [SerializeField] private CombatController combatController;

        [Header("UI Labels")]
        [SerializeField] private TextMeshProUGUI playerHpText;
        [SerializeField] private TextMeshProUGUI enemyNameText;
        [SerializeField] private TextMeshProUGUI enemyHpText;
        [SerializeField] private TextMeshProUGUI thresholdText;
        [SerializeField] private TextMeshProUGUI dicePoolCountText;
        [SerializeField] private TextMeshProUGUI bonusDiceText; // Changeover: shows "+N Bonus" while a bonus die is queued for the next Roll
        [SerializeField] private TextMeshProUGUI statusMessageText;

        private void Start()
        {
            // Wait one frame: Awake runs for every object before any Start,
            // but RunManager/EnemyController populate their actual values
            // inside their own Start() - reading immediately here could
            // race against them and show stale/zeroed numbers for a frame.
            StartCoroutine(RefreshNextFrame());
        }

        private IEnumerator RefreshNextFrame()
        {
            yield return null;
            RefreshStats();
        }

        /// <summary>Refreshes HP, Threshold, and other labels from current game state.</summary>
        public void RefreshStats()
        {
            var state = RunManager.Instance.State;

            if (playerHpText != null)
            {
                playerHpText.text = $"HP: {state.currentHp} / {state.maxHp}";
            }

            if (enemyNameText != null && enemyController != null && enemyController.Data != null)
            {
                enemyNameText.text = enemyController.Data.displayName;
            }

            if (enemyHpText != null && enemyController != null)
            {
                enemyHpText.text = $"Enemy HP: {enemyController.CurrentHp} / {enemyController.MaxHp}";
            }

            if (thresholdText != null && combatController != null)
            {
                thresholdText.text = $"Threshold: {combatController.PreviewEffectiveThreshold()}";
            }

            if (dicePoolCountText != null)
            {
                dicePoolCountText.text = $"Dice Pool: {state.dicePool.Count} (Draw: {state.drawPile.Count} / Discard: {state.discardPile.Count})";
            }

            if (bonusDiceText != null)
            {
                int pendingCount = state.pendingChangeoverBonusDice.Count;
                bonusDiceText.text = pendingCount > 0 ? $"+{pendingCount} Bonus" : string.Empty;
            }
        }

        /// <summary>Shows a short status message (e.g. "Roll dice to continue").</summary>
        public void ShowMessage(string message)
        {
            if (statusMessageText != null)
            {
                statusMessageText.text = message;
            }
        }

        /// <summary>Clears the status message.</summary>
        public void ClearMessage()
        {
            if (statusMessageText != null)
            {
                statusMessageText.text = string.Empty;
            }
        }
    }
}

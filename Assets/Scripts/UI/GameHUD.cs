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

        [Header("UI Labels")]
        [SerializeField] private TextMeshProUGUI playerHpText;
        [SerializeField] private TextMeshProUGUI enemyNameText;
        [SerializeField] private TextMeshProUGUI enemyHpText;
        [SerializeField] private TextMeshProUGUI thresholdText;
        [SerializeField] private TextMeshProUGUI dicePoolCountText;
        [SerializeField] private TextMeshProUGUI bonusDiceText; // shows a count of dice currently in Vibrazione BONUS (net multiplier > x1), hidden when zero - see ShowBonusDiceSummary
        [SerializeField] private TextMeshProUGUI statusMessageText;
        [SerializeField] private TextMeshProUGUI vibrationHandText; // shows a live summary of bonus/malus dice on the board (Vibrazione 3.0), recalculated as dice are placed - separate from statusMessageText so it never gets overwritten by targeting hints

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

            if (thresholdText != null && enemyController != null)
            {
                // PUNTO APERTO (Nemici): mostra solo la Soglia base per ora - i modificatori
                // delle abilita' nemiche torneranno quando ricostruiamo quel sistema.
                thresholdText.text = $"Threshold: {enemyController.GetThresholdForThisTurn()}";
            }

            if (dicePoolCountText != null)
            {
                dicePoolCountText.text = $"Dice Pool: {state.dicePool.Count}";
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

        /// <summary>
        /// Shows the current Vibrazione hand live (recalculated by
        /// RollKeepUIController any time dice are rolled/rerolled). Pass
        /// an empty string to hide it (e.g. no bonus/malus dice at all).
        /// </summary>
        public void ShowVibrationSummary(string summaryText)
        {
            if (vibrationHandText != null)
            {
                vibrationHandText.text = summaryText;
            }
        }

        /// <summary>
        /// Highlights specifically how many dice currently have a
        /// Vibrazione BONUS (net multiplier above x1) - a positive-only
        /// subset of ShowVibrationSummary's fuller bonus/malus count.
        /// Pass an empty string to hide it entirely (e.g. zero bonus dice).
        /// </summary>
        public void ShowBonusDiceSummary(string summaryText)
        {
            if (bonusDiceText != null)
            {
                bonusDiceText.text = summaryText;
            }
        }
    }
}

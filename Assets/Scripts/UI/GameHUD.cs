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
        [SerializeField] private TextMeshProUGUI enemyHpText;
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

        /// <summary>Refreshes HP labels from current game state.</summary>
        public void RefreshStats()
        {
            var state = RunManager.Instance.State;

            if (playerHpText != null)
            {
                playerHpText.text = $"HP: {state.currentHp} / {state.maxHp}";
            }

            if (enemyHpText != null && enemyController != null)
            {
                enemyHpText.text = $"Enemy HP: {enemyController.CurrentHp} / {enemyController.MaxHp}";
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

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rollrate.Core;

namespace Rollrate.Archive
{
    /// <summary>
    /// Archive Node UI: shows which of the 3 Tests was drawn (random, not
    /// chosen), then two explicit steps - Roll (shows the raw dice values)
    /// and Resolve (applies the win/lose effect based on that roll) -
    /// mirroring Combat's Roll/Resolve rhythm instead of doing everything
    /// automatically or in one click. Then Leave.
    /// </summary>
    public class ArchiveUI : MonoBehaviour
    {
        [SerializeField] private ArchiveController controller;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI rollText;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private TextMeshProUGUI scrapText;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private Button rollButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private string archiveSceneName = "ArchiveScene";

        private void Start()
        {
            // Leave is deliberately locked until the Test resolves (you
            // can't flee an Archive Test) - the listener is still wired
            // now so it's ready the moment it gets re-enabled.
            if (leaveButton != null)
            {
                leaveButton.onClick.AddListener(OnLeaveClicked);
                leaveButton.interactable = false;
            }

            if (RunManager.Instance == null)
            {
                Debug.LogError("[ArchiveUI] RunManager.Instance is null - load this scene additively via the Map, not by pressing Play directly on it.");
                // Fallback: don't leave the scene in a dead-end state if setup itself failed.
                if (leaveButton != null) leaveButton.interactable = true;
                return;
            }

            if (rollButton != null) rollButton.onClick.AddListener(OnRollClicked);

            RefreshTopLabels();
            if (descriptionText != null) descriptionText.text = controller.GetTestDescription();
            if (rollText != null) rollText.text = "";
            if (resultText != null) resultText.text = "";
        }

        private void RefreshTopLabels()
        {
            var state = RunManager.Instance.State;
            if (scrapText != null) scrapText.text = $"Scrap: {state.scrap}";
            if (hpText != null) hpText.text = $"HP: {state.currentHp} / {state.maxHp}";
        }

        private void OnRollClicked()
        {
            if (controller.HasResolved) return;

            controller.RollDice();
            controller.ResolveTest();
            RefreshTopLabels();

            if (rollText != null) rollText.text = controller.LastRollSummary;
            if (resultText != null) resultText.text = controller.LastResultSummary;
            if (rollButton != null) rollButton.interactable = false;
            if (leaveButton != null) leaveButton.interactable = true;
        }

        private void OnLeaveClicked()
        {
            NodeSceneLoader.ExitNode(archiveSceneName);
        }
    }
}

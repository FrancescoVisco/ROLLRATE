using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rollrate.Core;

namespace Rollrate.Rest
{
    /// <summary>
    /// The Rest Node (Nodo Falò): on entering, immediately recovers half of
    /// the player's missing HP (rounded up), then lets them open the
    /// Collection screen (to re-equip owned modules, no cost) before
    /// leaving. Loaded additively like Shop/Dismantle - a Map node will
    /// call NodeSceneLoader.EnterNode("RestNodeScene") once the Map exists;
    /// use the debug button until then.
    /// </summary>
    public class RestNodeUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button openCollectionButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private string restSceneName = "RestNodeScene";
        [SerializeField] private string collectionSceneName = "CollectionScene";

        private bool _hasRested;

        private void Start()
        {
            // Leave must always work, even if RunManager isn't ready.
            if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);
            if (openCollectionButton != null) openCollectionButton.onClick.AddListener(OnOpenCollectionClicked);

            if (RunManager.Instance == null)
            {
                Debug.LogError("[RestNodeUI] RunManager.Instance is null - load this scene additively via the debug button (DebugMapNodeButtons -> EnterRest), not by pressing Play directly on it.");
                return;
            }

            RestOnce();
        }

        /// <summary>
        /// Heals half the missing HP, rounded up. Only happens once per
        /// visit (re-entering Collection and coming back shouldn't heal again).
        /// </summary>
        private void RestOnce()
        {
            if (_hasRested) return;
            _hasRested = true;

            var state = RunManager.Instance.State;
            int missing = state.maxHp - state.currentHp;
            int healed = Mathf.CeilToInt(missing / 2f);
            state.currentHp = Mathf.Min(state.maxHp, state.currentHp + healed);

            RefreshHp();
            if (messageText != null)
            {
                messageText.text = healed > 0
                    ? $"You rest by the fire. Recovered {healed} HP."
                    : "You rest by the fire, though you weren't wounded.";
            }
        }

        private void RefreshHp()
        {
            var state = RunManager.Instance.State;
            if (hpText != null) hpText.text = $"HP: {state.currentHp} / {state.maxHp}";
        }

        private void OnOpenCollectionClicked()
        {
            NodeSceneLoader.EnterNode(collectionSceneName);
        }

        private void OnLeaveClicked()
        {
            NodeSceneLoader.ExitNode(restSceneName);
        }
    }
}

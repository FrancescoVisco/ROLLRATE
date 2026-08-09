using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Rollrate.Core;

namespace Rollrate.Meta
{
    /// <summary>
    /// The game's entry point scene - now also the landing point after
    /// Fragmentation (Meta screen's "Continue") and after Final Victory
    /// (MapController's full reset), not just on first launch.
    ///
    /// Two DISTINCT actions on purpose:
    /// - "Continua"/New Run: proceeds to the Map with WHATEVER state
    ///   currently exists - preserves Core Die/Scrap if the player just
    ///   came from a Fragmentation, or the freshly-zeroed defaults if this
    ///   is the very first launch (RunManager's own Awake already handles
    ///   that). Does NOT reset anything itself.
    /// - "Resetta Progressi": explicit, separate action - fully wipes
    ///   Core Die/Scrap/everything (RunManager.StartNewRun) before
    ///   proceeding, for a player who wants to abandon their current
    ///   progress and start completely from zero.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [FormerlySerializedAs("newRunButton")]
        [SerializeField] private Button continueButton;
        [SerializeField] private Button resetProgressButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private string mapSceneName = "MapScene";

        private void Start()
        {
            if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
            if (resetProgressButton != null) resetProgressButton.onClick.AddListener(OnResetProgressClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
        }

        /// <summary>Proceeds with whatever state currently exists - does NOT reset Core Die/Scrap.</summary>
        private void OnContinueClicked()
        {
            SceneManager.LoadScene(mapSceneName);
        }

        /// <summary>Explicit full wipe (Core Die, Scrap, everything) before proceeding - for a player who wants to abandon their current progress.</summary>
        private void OnResetProgressClicked()
        {
            RunManager.Instance?.StartNewRun();
            SceneManager.LoadScene(mapSceneName);
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

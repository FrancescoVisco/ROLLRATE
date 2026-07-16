using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Rollrate.Core;

namespace Rollrate.Meta
{
    /// <summary>
    /// The game's entry point scene. "Nuova Run" starts a fresh run and
    /// loads the Map (replacing this scene, not additive - the Map becomes
    /// the new persistent base). "Esci" quits the application. There's no
    /// direct access to Meta from here on purpose - it's only reached
    /// automatically after a Defeat or a full run completion.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button newRunButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button debugResetProgressButton; // DEBUG ONLY - wipes Frammenti Residui/unlocks, remove before shipping
        [SerializeField] private string mapSceneName = "MapScene";

        private void Start()
        {
            if (newRunButton != null) newRunButton.onClick.AddListener(OnNewRunClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
            if (debugResetProgressButton != null) debugResetProgressButton.onClick.AddListener(OnDebugResetProgressClicked);
        }

        private void OnNewRunClicked()
        {
            // If a RunManager already exists in memory (e.g. returning here
            // mid-session in the Editor), force a fresh run - its own
            // Awake() only calls StartNewRun() once, on first creation.
            if (RunManager.Instance != null)
            {
                RunManager.Instance.StartNewRun();
            }
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

        private void OnDebugResetProgressClicked()
        {
            MetaProgressionManager.ResetAll();
        }
    }
}

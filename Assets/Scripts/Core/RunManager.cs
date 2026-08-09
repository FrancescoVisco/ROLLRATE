using System;
using UnityEngine;
using Rollrate.Data;

namespace Rollrate.Core
{
    /// <summary>
    /// Single access point to the current GameState. Attach this to one
    /// GameObject that persists across scenes (DontDestroyOnLoad).
    ///
    /// DICE-TYPE REDESIGN: Modules are gone entirely; the debug starting
    /// pool specifies each die's DieType directly. Hand Size / Draw Pile
    /// ARE back (design doc Section 4) - each turn draws a fixed-size
    /// Hand from GameState's Draw Pile, not the whole owned pool.
    /// </summary>
    public class RunManager : MonoBehaviour
    {
        public static RunManager Instance { get; private set; }

        public GameState State { get; private set; } = new GameState();

        [Header("Starting Values")]
        [SerializeField] private DieData startingCoreDie; // assign the D4 asset here
        [SerializeField] private int startingHp = 10;
        [Tooltip("How many dice are drawn from the Draw Pile each Roll (design doc Section 4). If fewer dice are owned in total, draws as many as available.")]
        [SerializeField] private int handSize = 6;

        public int HandSize => handSize;

        [Serializable]
        public struct DebugDieEntry
        {
            public DieData data;
            public DieType type;
        }

        [Header("Debug Only - Test Dice Pool")]
        [Tooltip("Dice assigned here (with their Type) are added to the pool at the start of a new run, purely for testing. Remove/empty this once the Shop/Furnace can add dice for real.")]
        [SerializeField] private DebugDieEntry[] debugStartingPool;

        [Header("Debug Only - Starting Grade")]
        [Tooltip("Overrides the starting Echelon (Grade 1-5), purely for testing Grade-gated features without having to play up to them. Leave at 1 for a normal run.")]
        [SerializeField] private int debugStartingEchelon = 1;

        [Header("Meta Transition")]
        [Tooltip("Scene loaded after Defeat, where the player picks 1 of up to 3 dice unlocked this run to keep for future runs (design doc Section 7, Meta-progressione).")]
        [SerializeField] private string metaSceneName = "MetaScene";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Cleans up Unity's "There are 2 AudioListeners" console spam: every
            // scene brings its own Camera+AudioListener, but this object (and its
            // own listener, if any) persists across scene loads via
            // DontDestroyOnLoad above - so after the FIRST scene, there's always
            // at least one extra. Keeps exactly one enabled, disables the rest.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => CleanupExtraAudioListeners();

            // Populated here (not in Start()) so that GameState is guaranteed
            // ready before ANY other script's Start() runs.
            StartNewRun();
        }

        private static void CleanupExtraAudioListeners()
        {
            var listeners = FindObjectsOfType<AudioListener>();
            for (int i = 1; i < listeners.Length; i++)
            {
                listeners[i].enabled = false;
            }
        }

        public void StartNewRun()
        {
            State.ResetForNewRun(startingCoreDie, startingHp);

            // Debug only: jump straight to a chosen Grade, skipping the
            // normal Grade I start.
            if (debugStartingEchelon > 1)
            {
                State.currentEchelon = Mathf.Clamp(debugStartingEchelon, 1, 5);
            }

            // Debug only: seed the pool with test dice so there's more
            // than just the Core to roll, before the Shop/Furnace can add
            // dice for real. fromRunUnlock=false: this is the starting
            // pool, not something "unlocked during the run" (see
            // GameState.unlockedThisRun / the Meta end-of-run screen).
            if (debugStartingPool != null)
            {
                foreach (var entry in debugStartingPool)
                {
                    if (entry.data != null)
                    {
                        State.AddDieToPool(new DieInstance(entry.data, entry.type), fromRunUnlock: false);
                    }
                }
            }

            Debug.Log($"[RunManager] New run started. Core: {State.coreDie?.displayName}, HP: {State.currentHp}, Pool size: {State.dicePool.Count}");
        }

        /// <summary>Design doc Section 6: Scrap tax paid to ascend to the NEXT Grade, indexed by the CURRENT Grade (1-4; Grade V has no further ascent).</summary>
        private static readonly float[] AscensionTaxByGrade = { 0f, 0.10f, 0.15f, 0.20f, 0.25f };

        /// <summary>
        /// Call after defeating a Guardian (design doc Section 7-8,
        /// Nodo Terminale + Data Extraction): evolves the Core Die,
        /// charges the Tassa di Sfarzo (a % of current Scrap, based on
        /// the Grade being LEFT), and advances to the next Grade/Page 1.
        /// No-op past Grade V (Sovereign has no further ascent).
        /// </summary>
        /// <summary>
        /// SOLE AUTHORITY for Guardian-victory progression (Core evolution,
        /// Tassa di Sfarzo, Grade advance) - MapController.ApplyRecalibration
        /// used to ALSO do all three independently, causing every Guardian
        /// victory to double-evolve the Core, double-charge the tax, and
        /// skip a whole Grade. MapController now only reacts to the state
        /// this method already set (generate the new Page, or load Meta on
        /// final victory) - see MapController.ApplyRecalibration.
        /// </summary>
        public void ApplyGuardianVictory(EnemyData guardian)
        {
            if (guardian != null && guardian.coreEvolutionOnDefeat != null)
            {
                State.coreDie = guardian.coreEvolutionOnDefeat;
            }

            int currentGrade = Mathf.Clamp(State.currentEchelon, 1, 5);
            if (currentGrade < 5)
            {
                float taxRate = AscensionTaxByGrade[currentGrade];
                int tax = Mathf.RoundToInt(State.scrap * taxRate);
                State.scrap = Mathf.Max(0, State.scrap - tax);
                State.currentEchelon = currentGrade + 1;
                // currentPage is deliberately NOT touched here - MapController.OnAnySceneUnloaded
                // reads it right after this call to decide "next Page of same Grade" vs
                // "Recalibrate" (Page 3 check); GenerateAndRenderPage(1) resets it correctly
                // once Recalibration actually runs. Setting it here would make that check see
                // Page 1 instead of 3, generating the WRONG next page (skipping the new Grade's
                // Page 1 entirely).
                Debug.Log($"[RunManager] Guardian defeated - Core evolved to {State.coreDie?.displayName}, Tassa di Sfarzo -{tax} Scrap ({taxRate:P0} of Grade {currentGrade}), advancing to Grade {State.currentEchelon}.");
            }
            else
            {
                // Sentinel value (6 = "past Grade V") so MapController knows the run is
                // complete without needing to re-derive that from anything else.
                State.currentEchelon = 6;
                Debug.Log($"[RunManager] Sovereign (Grade V Guardian) defeated - Core evolved to {State.coreDie?.displayName}. Run complete.");
                // PUNTO APERTO: l'esito di vittoria completa (design doc Sezione 7,
                // "Vittoria completa") resta da progettare per intero.
            }
        }

        /// <summary>
        /// Call this when the player's HP reaches 0. Loads the Meta screen
        /// WITHOUT fragmenting yet - MetaController needs to read
        /// State.unlockedThisRun (and currentEchelon/enemiesDefeatedThisRun
        /// for the run summary) while they're still intact. Call
        /// FinalizeFragmentationAndContinue once the player has made their
        /// choice there.
        /// </summary>
        public void HandleDefeat()
        {
            Debug.Log($"[RunManager] Defeat - going to Meta screen. Core: {State.coreDie?.displayName}, Scrap: {State.scrap}, Grade reached: {State.currentEchelon}, enemies defeated: {State.enemiesDefeatedThisRun}, dice unlocked this run: {State.unlockedThisRun.Count}");
            UnityEngine.SceneManagement.SceneManager.LoadScene(metaSceneName);
        }

        /// <summary>
        /// Called by MetaController once the player has picked their die
        /// (or there was nothing to pick from). Applies Fragmentation
        /// (Core Die + Scrap persist, everything else resets) THEN adds
        /// the chosen die (if any) to the now-empty pool as the sole
        /// survivor (design doc Section 7, Meta-progressione).
        /// </summary>
        public void FinalizeFragmentationAndContinue(DieInstance chosenDie)
        {
            State.ApplyFragmentation(startingHp);
            if (chosenDie != null)
            {
                State.AddDieToPool(chosenDie, fromRunUnlock: false);
                Debug.Log($"[RunManager] Fragmentation applied - kept {chosenDie.data?.displayName} ({chosenDie.type}) from this run.");
            }
            else
            {
                Debug.Log("[RunManager] Fragmentation applied - no die was kept this time.");
            }
        }
    }
}

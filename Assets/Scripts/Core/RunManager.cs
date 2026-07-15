using UnityEngine;
using Rollrate.Data;

namespace Rollrate.Core
{
    /// <summary>
    /// Single access point to the current GameState. Attach this to one
    /// GameObject that persists across scenes (DontDestroyOnLoad).
    ///
    /// For now this only holds state and exposes basic run-lifecycle calls.
    /// Map transitions, combat triggers, and shop calls will hook into this
    /// later as those systems get built.
    /// </summary>
    public class RunManager : MonoBehaviour
    {
        public static RunManager Instance { get; private set; }

        public GameState State { get; private set; } = new GameState();

        [Header("Starting Values")]
        [SerializeField] private DieData startingCoreDie; // assign the D4 asset here
        [SerializeField] private int startingHp = 10;
        [Tooltip("How many dice are drawn from the Draw Pile each Roll. If fewer dice are owned in total, draws as many as available.")]
        [SerializeField] private int handSize = 6;

        public int HandSize => handSize;

        [Header("Debug Only - Test Dice Pool")]
        [Tooltip("Dice assigned here are added to the pool at the start of a new run, purely for testing. Remove/empty this once the Shop can add dice for real.")]
        [SerializeField] private DieData[] debugStartingPool;

        [Header("Debug Only - Test Modules")]
        [Tooltip("One module per slot, assigned at run start purely for testing. Remove/empty this once the Shop can install modules for real.")]
        [SerializeField] private ModuleData debugPowerModule;
        [SerializeField] private ModuleData debugStabilityModule;
        [SerializeField] private ModuleData debugFlowModule;
        [SerializeField] private ModuleData debugEchoModule;

        [Header("Debug Only - Starting Grade")]
        [Tooltip("Overrides the starting Echelon (Grade 1-5), purely for testing Grade-gated features (e.g. Oscuramento fog from Grade IV, Singolarità at Grade V) without having to play up to them. Leave at 1 for a normal run.")]
        [SerializeField] private int debugStartingEchelon = 1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Populated here (not in Start()) so that GameState is guaranteed
            // ready before ANY other script's Start() runs - Unity completes
            // all Awake() calls before calling any Start(), regardless of
            // execution order between different scripts.
            StartNewRun();
        }

        public void StartNewRun()
        {
            State.ResetForNewRun(startingCoreDie, startingHp);

            // Debug only: jump straight to a chosen Grade, skipping the
            // normal Grade I start - lets Grade-gated features (fog,
            // forced fights) be tested without a full playthrough.
            if (debugStartingEchelon > 1)
            {
                State.currentEchelon = Mathf.Clamp(debugStartingEchelon, 1, 5);
            }

            // Debug only: seed the pool with test dice so DiceRoller has more
            // than just the Core to roll, before the Shop can add dice for real.
            if (debugStartingPool != null)
            {
                foreach (var die in debugStartingPool)
                {
                    if (die != null) State.AddDieToPool(die);
                }
            }
            ShuffleDrawPile();

            // Debug only: install test modules directly, before the Shop can do it for real.
            InstallAndOwnDebugModule(SlotType.Power, debugPowerModule);
            InstallAndOwnDebugModule(SlotType.Stability, debugStabilityModule);
            InstallAndOwnDebugModule(SlotType.Flow, debugFlowModule);
            InstallAndOwnDebugModule(SlotType.Echo, debugEchoModule);

            Debug.Log($"[RunManager] New run started. Core: {State.coreDie?.displayName}, HP: {State.currentHp}, Pool size: {State.dicePool.Count}");
        }

        private void InstallAndOwnDebugModule(SlotType slot, ModuleData module)
        {
            if (module == null) return;
            State.installedModules[slot] = module;
            State.AddOwnedModule(module);
        }

        /// <summary>Shuffles the current Draw Pile in place (Fisher-Yates).</summary>
        private void ShuffleDrawPile()
        {
            var pile = State.drawPile;
            for (int i = pile.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pile[i], pile[j]) = (pile[j], pile[i]);
            }
        }

        /// <summary>
        /// Call this when the player's HP reaches 0. Applies the Fragmentation
        /// rule (Core Die level + Scrap persist, everything else resets) and
        /// sends the player back to Echelon I.
        /// </summary>
        public void HandleDefeat()
        {
            Debug.Log($"[RunManager] Defeat. Fragmenting. Core stays at {State.coreDie?.displayName}, Scrap kept: {State.scrap}");
            State.ApplyFragmentation(startingHp);
        }
    }
}

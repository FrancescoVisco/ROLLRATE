using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rollrate.Data;
using Rollrate.Core;
using Rollrate.Combat;

namespace Rollrate.UI
{
    /// <summary>
    /// The scene-level bridge between the pure-logic TurnController and
    /// the actual Roll & Keep UI (design doc Section 4). Spawns a
    /// RollableDie per Hand die (plus the Core Die and the enemy's
    /// Inhibitor Die as locked display-only dice), wires clicks to
    /// mark/unmark dice for reroll or assign Echo transfers, wires
    /// Reroll/Resolve, detects Defeat, and keeps GameHUD in sync.
    /// All 16 Effects are now connected (see TurnContext/TurnController).
    /// </summary>
    public class RollKeepUIController : MonoBehaviour
    {
        public static RollKeepUIController Instance { get; private set; }

        /// <summary>Exposed read-only so GameHUD can compute the true (ability-adjusted) Threshold for display - see GameHUD.RefreshStats.</summary>
        public TurnContext Context => _turn.Context;

        [Header("Scene References")]
        [SerializeField] private RollableDie dieViewPrefab;
        [SerializeField] private Transform handContainer;
        [SerializeField] private Transform coreDieContainer;
        [SerializeField] private Transform inhibitorDieContainer;
        [SerializeField] private Button rollButton;
        [SerializeField] private Button rerollButton;
        [Tooltip("Advances from the Reroll phase to the Echo phase (design doc Section 4) - only needed if you have Echo dice to assign; you can also skip straight to Resolve.")]
        [SerializeField] private Button doneRerollingButton;
        [SerializeField] private Button resolveButton;
        [SerializeField] private TextMeshProUGUI rerollsRemainingText;
        [SerializeField] private TextMeshProUGUI resultText;
        [Tooltip("Live preview of your current Power sum (before Resolve). Updates after Roll, Reroll, and Echo assignment.")]
        [SerializeField] private TextMeshProUGUI liveAttackText;
        [Tooltip("Live preview of your current Stability sum (before Resolve). Updates after Roll, Reroll, and Echo assignment.")]
        [SerializeField] private TextMeshProUGUI liveDefenseText;
        [Tooltip("Live preview of the enemy's Attack this turn (base + Inhibitor Parity + pending bonuses). Updates after Roll.")]
        [SerializeField] private TextMeshProUGUI liveEnemyAttackText;
        [Tooltip("Shows what the player can currently do - refreshed every time state changes.")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private EnemyController enemyController;
        [SerializeField] private GameHUD gameHUD;
        [Tooltip("Must match the actual name of this Combat scene (see MapController's own 'Combat Scene Name', same default) - used to return to the Map on victory via NodeSceneLoader.ExitNode.")]
        [SerializeField] private string combatSceneName = "CombatScene";

        /// <summary>
        /// Design doc Section 4 - the turn always proceeds Reroll first,
        /// THEN Echo: while in Reroll, any die EXCEPT Flow can be marked
        /// for reroll (Flow can never modify itself - it's what GRANTS
        /// the rerolls) - Echo dice can be rerolled here too, so their
        /// value can be improved BEFORE assigning a target. Once
        /// advanced to Echo (via doneRerollingButton), only Echo transfer
        /// assignment is available (reroll-marking is locked). Resolve
        /// is available in either phase, for players with no Echo dice
        /// to assign.
        /// </summary>
        private enum TurnPhase { Reroll, Echo }
        private TurnPhase _phase;

        private readonly TurnController _turn = new TurnController();
        private readonly List<RollableDie> _dieViews = new List<RollableDie>();
        private RollableDie _coreDieView;
        private RollableDie _inhibitorDieView;
        private bool _turnInProgress; // true from Roll until Resolve - blocks re-Rolling mid-turn

        private static readonly Color CoreDieTint = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color InhibitorDieTint = new Color(0.35f, 0.1f, 0.1f);

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (rollButton != null) rollButton.onClick.AddListener(RollAll);
            if (rerollButton != null) rerollButton.onClick.AddListener(OnRerollClicked);
            if (doneRerollingButton != null) doneRerollingButton.onClick.AddListener(OnDoneRerollingClicked);
            if (resolveButton != null) resolveButton.onClick.AddListener(OnResolveClicked);

            SetTurnButtonsState(canRoll: true, canResolve: false);
            if (doneRerollingButton != null) doneRerollingButton.interactable = false;
            RefreshStatusText();
            gameHUD?.ShowVibrationSummary(string.Empty);
        }

        /// <summary>SET-UP + ROLL (design doc Section 4). Call when the player presses Roll.</summary>
        public void RollAll()
        {
            if (_turnInProgress) return; // can't re-Roll mid-turn - must Resolve first

            var state = RunManager.Instance?.State;
            if (state == null) return;

            if (enemyController != null)
            {
                enemyController.RollInhibitor();
                EnemyAbilityResolver.OnTurnStart(enemyController);
            }
            int? inhibited = enemyController != null ? enemyController.LastInhibitedValue : (int?)null;

            int handSize = RunManager.Instance != null ? RunManager.Instance.HandSize : 6;
            var ctx = _turn.RollAll(state, handSize, inhibited);
            _pendingEchoSource = null;
            _phase = TurnPhase.Reroll;

            foreach (var view in _dieViews)
            {
                if (view != null) Destroy(view.gameObject);
            }
            _dieViews.Clear();

            foreach (var dieState in ctx.dice)
            {
                dieState.isInhibited = ctx.IsInhibited(dieState);
                var view = Instantiate(dieViewPrefab, handContainer);
                view.Setup(dieState, isLocked: false);
                view.SetVibrationMultiplier(ctx.GetNetMultiplier(dieState));
                view.RefreshInhibited(dieState.isInhibited);
                _dieViews.Add(view);
            }

            if (_coreDieView != null) Destroy(_coreDieView.gameObject);
            if (coreDieContainer != null)
            {
                _coreDieView = Instantiate(dieViewPrefab, coreDieContainer);
                _coreDieView.SetupLockedDisplay(ctx.coreValue, CoreDieTint);
            }

            if (_inhibitorDieView != null) Destroy(_inhibitorDieView.gameObject);
            if (inhibitorDieContainer != null && inhibited.HasValue)
            {
                _inhibitorDieView = Instantiate(dieViewPrefab, inhibitorDieContainer);
                _inhibitorDieView.SetupLockedDisplay(inhibited.Value, InhibitorDieTint);
            }

            _turnInProgress = true;
            SetTurnButtonsState(canRoll: false, canResolve: true);
            RefreshRerollsUI();
            RefreshVibrationSummary();
            RefreshLiveCombatStats();
            gameHUD?.RefreshStats(); // Draw/Discard Pile counters just changed (DrawHand consumed from the Draw Pile)
            LogHandDiagnostics(ctx);
        }

        /// <summary>Diagnostic only (Debug.Log, developer-facing) - logs every die in the Hand, its Type/value/multiplier, Echo assignment, and the resulting Attack/Defense.</summary>
        private void LogHandDiagnostics(TurnContext ctx)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[RollKeepUIController] Hand rolled - Core: {ctx.coreValue} ({(ctx.coreIsEven ? "Even" : "Odd")}, {ctx.coreRange})");
            foreach (var d in ctx.dice)
            {
                string typeName = d.instance?.type.ToString() ?? "?";
                float mult = ctx.GetNetMultiplier(d);
                string echoNote = "";
                if (d.instance?.type == DieType.Echo)
                {
                    if (d.echoTargetType.HasValue) echoNote = $" -> Chain to ALL {d.echoTargetType.Value}";
                    else if (d.echoTarget != null) echoNote = $" -> transfers to {d.echoTarget.instance?.type} d{d.echoTarget.instance?.data?.faces}={d.echoTarget.rolledValue}";
                }
                sb.AppendLine($"  {typeName} d{d.instance?.data?.faces} = {d.rolledValue} (x{mult:0.##} -> {ctx.GetEffectiveValue(d):0.#}){(d.isInhibited ? " [INHIBITED]" : "")}{echoNote}");
            }
            sb.AppendLine($"  Attack: {ctx.ComputeAttack():0.#} | Defense: {ctx.ComputeDefense():0.#}");
            Debug.Log(sb.ToString());
        }

        /// <summary>The Echo die currently awaiting a target/type (click it again to cancel). Null when no Echo assignment is in progress.</summary>
        private HeldDieState _pendingEchoSource;

        /// <summary>
        /// Single click on a die routes between three things (design doc
        /// Section 4):
        /// 1. If an Echo die is awaiting a target, this click completes
        ///    the transfer - to a single die normally, or (if the Echo
        ///    die carries Chain) to ALL dice of the clicked die's Type -
        ///    or cancels it (clicking the same Echo die again).
        /// 2. Clicking an Echo die (nothing pending) starts target-selection.
        /// 3. Otherwise, toggles the die's reroll mark - only while
        ///    rerolls are actually available.
        /// PHASE ORDER (design doc Section 4): while in the Reroll phase,
        /// ONLY reroll-marking works - Echo dice are not clickable at
        /// all. Once advanced to the Echo phase (OnDoneRerollingClicked),
        /// ONLY Echo transfer assignment works - reroll-marking is locked.
        /// </summary>
        public void OnDieClicked(RollableDie view)
        {
            if (view?.state == null) return;
            var clicked = view.state;

            if (_phase == TurnPhase.Echo)
            {
                if (_pendingEchoSource != null)
                {
                    if (clicked == _pendingEchoSource)
                    {
                        _pendingEchoSource = null; // clicking the source again cancels
                        view.SetPendingEchoSource(false);
                        RefreshStatusText();
                        return;
                    }

                    bool sourceHasChain = _turn.Context.HasEffect(_pendingEchoSource, EffectId.Chain);
                    bool assigned = sourceHasChain && clicked.instance != null
                        ? _turn.AssignEchoTargetType(_pendingEchoSource, clicked.instance.type)
                        : _turn.AssignEchoTarget(_pendingEchoSource, clicked);

                    if (assigned)
                    {
                        FindViewFor(_pendingEchoSource)?.SetPendingEchoSource(false);
                        _pendingEchoSource = null;
                        RefreshEchoLinkedVisuals(); // the target(s) now show the same yellow highlight, addressing "non si capisce che lo stai selezionando"
                        RefreshLiveCombatStats();
                        LogHandDiagnostics(_turn.Context); // effective values changed - show the new totals
                    }
                    RefreshStatusText();
                    return;
                }

                if (clicked.instance?.type == DieType.Echo)
                {
                    // "Diventa un numero e basta" (design doc Section 5, Inibizione) applies to
                    // Echo's BASE role too, not just its attached Effects - an inhibited Echo
                    // die can't transfer its value at all, same principle already applied to
                    // Flow's reroll-granting.
                    if (_turn.Context.IsInhibited(clicked)) return;

                    _pendingEchoSource = clicked;
                    view.SetPendingEchoSource(true);
                    RefreshStatusText();
                }
                return; // Echo phase: non-Echo dice do nothing when clicked
            }

            // Reroll phase: Flow dice can never be marked for reroll (a Flow die is
            // what GRANTS the rerolls, it never modifies itself) - Echo dice CAN be
            // rerolled here too, same as Power/Stability, so their value can be
            // improved BEFORE assigning a target in the Echo phase that follows.
            if (clicked.instance?.type == DieType.Flow) return;
            if (_turn.RerollsRemaining <= 0) return; // nothing to do with 0 rerolls left

            _turn.ToggleRerollMark(clicked);
            view.RefreshHeld();
            RefreshRerollsUI();
        }

        /// <summary>
        /// Live preview of Attack/Defense/enemy Attack (design doc Section
        /// 4, "valori aggiornati in tempo reale") - shows what Resolve
        /// WOULD do right now, without triggering any Effect/Ability side
        /// effects (Void's Echo redirect, Suppress's reduction, etc. only
        /// actually apply once, at the real Resolve). Call after Roll,
        /// Reroll, and any successful Echo assignment.
        /// </summary>
        private void RefreshLiveCombatStats()
        {
            if (_turn.Context != null)
            {
                if (liveAttackText != null) liveAttackText.text = $"Attack: {_turn.Context.ComputeAttack():0.#}";
                if (liveDefenseText != null) liveDefenseText.text = $"Defense: {_turn.Context.ComputeDefense():0.#}";
            }

            if (enemyController != null && liveEnemyAttackText != null)
            {
                liveEnemyAttackText.text = $"Enemy Attack: {enemyController.PreviewAttackForThisTurn()}";
            }
        }

        private RollableDie FindViewFor(HeldDieState dieState)
        {
            return _dieViews.Find(v => v.state == dieState);
        }

        /// <summary>
        /// Recomputes, for every die currently in the Hand, whether it's
        /// the target of an assigned Echo transfer (single-die or Chain
        /// type-wide) and updates its yellow "linked" highlight - the
        /// target previously showed nothing at all once chosen.
        /// </summary>
        private void RefreshEchoLinkedVisuals()
        {
            if (_turn.Context == null) return;

            foreach (var view in _dieViews)
            {
                if (view?.state == null) continue;

                bool isLinked = _turn.Context.dice.Exists(other =>
                    other.instance?.type == DieType.Echo &&
                    other != view.state &&
                    (other.echoTarget == view.state ||
                     (other.echoTargetType.HasValue && view.state.instance?.type == other.echoTargetType.Value)));

                view.SetEchoLinked(isLinked);
            }
        }

        private void OnDoneRerollingClicked()
        {
            if (_phase != TurnPhase.Reroll) return;
            _phase = TurnPhase.Echo;
            RefreshRerollsUI();
        }

        private void OnRerollClicked()
        {
            var result = _turn.Reroll();
            if (result.rerolledDice.Count == 0) return;

            var state = RunManager.Instance?.State;

            if (enemyController != null) EnemyAbilityResolver.OnPlayerReroll(enemyController, state);
            gameHUD?.RefreshStats(); // Warden's Stasis may have just changed player HP

            foreach (var view in _dieViews)
            {
                if (view.state == null || !result.rerolledDice.Contains(view.state)) continue;
                view.state.isInhibited = _turn.Context.IsInhibited(view.state);
                view.RefreshValue();
                view.SetVibrationMultiplier(_turn.Context.GetNetMultiplier(view.state));
                view.RefreshInhibited(view.state.isInhibited);
                view.RefreshHeld(); // mark was cleared by Reroll() - visual catches up
            }

            if (result.scrapGained > 0)
            {
                if (state != null) state.scrap += result.scrapGained; // SafetyNet
            }

            RefreshRerollsUI();
            RefreshVibrationSummary();
            RefreshLiveCombatStats();
        }

        private void RefreshRerollsUI()
        {
            if (rerollsRemainingText != null)
            {
                rerollsRemainingText.text = $"Rerolls: {_turn.RerollsRemaining}";
            }
            if (rerollButton != null)
            {
                rerollButton.interactable = _turnInProgress && _phase == TurnPhase.Reroll && _turn.RerollsRemaining > 0;
            }
            if (doneRerollingButton != null)
            {
                doneRerollingButton.interactable = _turnInProgress && _phase == TurnPhase.Reroll;
            }
            RefreshStatusText();
        }

        /// <summary>Counts dice with a Vibration bonus vs malus and pushes the summary to GameHUD - hidden (empty) when there's nothing to report.</summary>
        private void RefreshVibrationSummary()
        {
            if (gameHUD == null || _turn.Context == null) return;

            int bonusCount = 0;
            int malusCount = 0;
            foreach (var d in _turn.Context.dice)
            {
                float mult = _turn.Context.GetNetMultiplier(d);
                if (mult > 1f) bonusCount++;
                else if (mult < 1f) malusCount++;
            }

            gameHUD.ShowVibrationSummary(bonusCount > 0 || malusCount > 0
                ? $"Vibration: {bonusCount} bonus, {malusCount} malus"
                : string.Empty);
        }

        /// <summary>Roll is only available before a turn starts (or after Resolve); Reroll/Resolve only while a turn is in progress.</summary>
        private void SetTurnButtonsState(bool canRoll, bool canResolve)
        {
            if (rollButton != null) rollButton.interactable = canRoll;
            if (resolveButton != null) resolveButton.interactable = canResolve;
        }

        /// <summary>Tells the player what they can currently do, and which phase they're in.</summary>
        private void RefreshStatusText()
        {
            if (statusText == null) return;

            if (!_turnInProgress)
            {
                statusText.text = "Press Roll to start the turn.";
                return;
            }

            if (_phase == TurnPhase.Reroll)
            {
                int rerolls = _turn.RerollsRemaining;
                statusText.text = rerolls > 0
                    ? $"REROLL PHASE: click the dice you want to reroll (highlighted yellow), then press Reroll ({rerolls} available). Press \"Done Rerolling\" when finished, or Resolve to skip Echo entirely."
                    : "REROLL PHASE: no rerolls available. Press \"Done Rerolling\" to move on, or Resolve to skip Echo entirely.";
                return;
            }

            // TurnPhase.Echo
            if (_pendingEchoSource != null)
            {
                statusText.text = "ECHO PHASE: click a target die for the transfer (the Echo die is highlighted yellow - click it again to cancel).";
                return;
            }

            statusText.text = "ECHO PHASE: click an Echo die to assign it a target, or press Resolve when ready.";
        }

        /// <summary>
        /// CHECK: uses TurnController.ResolveCheck (all 16 Effects
        /// connected - see TurnContext/TurnController for exactly where
        /// each one is evaluated), applies its result to GameState/
        /// EnemyController, and stores Reverb's pending value for next turn.
        /// </summary>
        private void OnResolveClicked()
        {
            if (enemyController == null) return;

            var state = RunManager.Instance?.State;

            int enemyAttackThisTurn = enemyController.GetAttackForThisTurnAndClearPending(); // Suppress reduction, if any
            int thresholdThisTurn = enemyController.GetThresholdForThisTurn();

            var (attackAdj, defenseAdj, extraThreshold) = EnemyAbilityResolver.ApplyPreCheckModifiers(enemyController, _turn.Context, state);
            thresholdThisTurn += extraThreshold;

            var result = _turn.ResolveCheck(thresholdThisTurn, enemyAttackThisTurn, attackAdj, defenseAdj);

            if (result.attackSucceeded)
            {
                enemyController.ApplyDamage(result.excess);
            }
            if (result.bonusDamageToEnemy > 0)
            {
                enemyController.ApplyDamage(result.bonusDamageToEnemy); // Backlash
            }
            if (result.enemyAttackReductionNextTurn > 0)
            {
                enemyController.QueuePendingAttackReduction(result.enemyAttackReductionNextTurn); // Suppress
            }

            if (state != null)
            {
                if (result.attackSucceeded)
                {
                    state.scrap += result.excess; // design doc Section 6, "Combattimento": Scrap = Eccesso, base rule (was completely missing before - only Cushion/SafetyNet ever granted Scrap)
                }
                if (!result.defenseHeld)
                {
                    state.currentHp = Mathf.Max(0, state.currentHp - result.damageTaken);
                }
                if (result.scrapGained > 0)
                {
                    state.scrap += result.scrapGained; // Cushion
                }
                if (result.hpHealed > 0)
                {
                    state.currentHp = Mathf.Min(state.maxHp, state.currentHp + result.hpHealed); // Drain
                }

                _turn.StoreReverbPending(state); // Reverb - lands next time its target die is drawn
            }

            EnemyAbilityResolver.OnTurnEnd(enemyController, _turn.Context, state, result.attackSucceeded); // Pressure/Discord queue next-turn bonuses, Delete disables dice
            enemyController.ClearPerTurnAbilityState(); // Threshold/Attack bonuses just read above are now spent

            if (resultText != null)
            {
                string attackLine = result.attackSucceeded
                    ? $"Attack {result.attack:0} vs Threshold {thresholdThisTurn}: SUCCESS (-{result.excess} enemy HP)"
                    : $"Attack {result.attack:0} vs Threshold {thresholdThisTurn}: failed";
                string defenseLine = result.defenseHeld
                    ? $"Defense {result.defense:0} vs enemy Attack {enemyAttackThisTurn}: held"
                    : $"Defense {result.defense:0} vs enemy Attack {enemyAttackThisTurn}: -{result.damageTaken} HP";
                string extras = "";
                if (result.bonusDamageToEnemy > 0) extras += $"\nBacklash: -{result.bonusDamageToEnemy} extra enemy HP";
                if (result.scrapGained > 0) extras += $"\nCushion: +{result.scrapGained} Scrap";
                if (result.hpHealed > 0) extras += $"\nDrain: +{result.hpHealed} HP healed";
                if (result.defenseBonusFromOverflow > 0) extras += $"\nOverflow: +{result.defenseBonusFromOverflow} Defense this turn";
                if (result.enemyAttackReductionNextTurn > 0) extras += $"\nSuppress: enemy Attack -{result.enemyAttackReductionNextTurn} next turn";
                resultText.text = $"{attackLine}\n{defenseLine}{extras}";
            }

            Debug.Log($"[RollKeepUIController] Resolve: Attack {result.attack:0.#} vs Threshold {thresholdThisTurn} ({(result.attackSucceeded ? $"success, Excess {result.excess}" : "failed")}), Defense {result.defense:0.#} vs enemy Attack {enemyAttackThisTurn} ({(result.defenseHeld ? "held" : $"-{result.damageTaken} HP")})");

            // Without this call GameHUD only refreshed once at scene start - HP changed
            // inside GameState/EnemyController but the on-screen text stayed frozen.
            gameHUD?.RefreshStats();

            if (state != null && state.IsDefeated)
            {
                if (statusText != null) statusText.text = "Defeated! Fragmentation in progress...";
                RunManager.Instance?.HandleDefeat();
                return; // scene is about to unload - don't touch turn state below
            }

            if (enemyController.IsDefeated)
            {
                if (statusText != null) statusText.text = $"{enemyController.Data?.displayName} defeated!";
                _turnInProgress = false; // stop any further input before the scene unloads

                if (state != null) state.enemiesDefeatedThisRun++; // Meta screen's run summary (design doc Section 7)

                if (enemyController.Data != null && enemyController.Data.tier == EnemyTier.Guardian)
                {
                    RunManager.Instance?.ApplyGuardianVictory(enemyController.Data); // Core evolution + Tassa di Sfarzo + Grade advance
                }

                Rollrate.Core.NodeSceneLoader.ExitNode(combatSceneName); // returns to the Map, which advances on its own (see MapController.OnAnySceneUnloaded)
                return;
            }

            _turnInProgress = false;
            SetTurnButtonsState(canRoll: true, canResolve: false);
            RefreshRerollsUI(); // also disables doneRerollingButton correctly (turn is over)
            gameHUD?.ShowVibrationSummary(string.Empty);
        }
    }
}

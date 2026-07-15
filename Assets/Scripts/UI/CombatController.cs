using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;
using Rollrate.Core;
using Rollrate.Data;
using Rollrate.Combat;

namespace Rollrate.UI
{
    /// <summary>
    /// Orchestrates one CHECK phase: reads the Core Die roll, reads what's
    /// placed in each of the 4 slots, applies Flow's target-die manipulation
    /// (Mirror/Shift/Reverse) and enemy die-value modifiers (Architect/
    /// Prism/Null-Pointer), detects Resonance (Legame/Totale), resolves each
    /// slot's Static + Frequency effect via ModuleResolver, and sums the
    /// Power slot total to check success/failure against the real enemy's
    /// Threshold (via EnemyController).
    ///
    /// On success, damages the enemy and grants Scrap based on the excess
    /// over Threshold. On failure, damages the player (reduced by Stability
    /// damage reduction, plus any enemy bonus failure damage). Checks
    /// victory/defeat afterward, then notifies the enemy's ability that the
    /// turn ended (for Sovereign's Delete).
    /// </summary>
    public class CombatController : MonoBehaviour
    {
        public static CombatController Instance { get; private set; }

        [Header("References")]
        [SerializeField] private DiceRoller diceRoller;
        [SerializeField] private EnemyController enemyController;
        [SerializeField] private SlotDropZone powerSlot;
        [SerializeField] private SlotDropZone stabilitySlot;
        [SerializeField] private SlotDropZone flowSlot;
        [SerializeField] private SlotDropZone echoSlot;
        [SerializeField] private UnityEngine.UI.Button resolveButton; // disabled after resolving, re-enabled once the player rolls again

        [Header("Fallback Debug Values (used only if Enemy Controller is not assigned)")]
        [SerializeField] private int debugThreshold = 10;
        [SerializeField] private int debugInhibitedValue = 0;

        [Header("Optional HUD")]
        [SerializeField] private GameHUD gameHUD;

        private bool _needsReroll = true; // blocks ResolveTurn until the player rolls for the first time

        // --- Flow target-die selection (Mirror/Shift/Reverse) ---
        private bool _awaitingTargetSelection = false;
        private SlotType? _flowTargetSlot = null;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            // Whenever a node scene (Shop/Collection/Dismantle/Rest) unloads
            // and we're back looking at Combat, refresh immediately instead
            // of waiting for the next Roll - purchases/equips/heals done in
            // that node should be visible right away.
            SceneManager.sceneUnloaded += OnAnySceneUnloaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneUnloaded -= OnAnySceneUnloaded;
        }

        private void OnAnySceneUnloaded(Scene unloadedScene)
        {
            RefreshSlotLabels();
            gameHUD?.RefreshStats();
        }

        private void Start()
        {
            StartCoroutine(InitializeAfterFirstFrame());
        }

        private IEnumerator InitializeAfterFirstFrame()
        {
            yield return null;
            RefreshSlotLabels();
            gameHUD?.RefreshStats();
        }

        private void Update()
        {
            CheckIfTargetSelectionNeeded();
        }

        /// <summary>
        /// Watches the Flow slot: if its installed module needs a target die
        /// (Mirror/Shift/Reverse) and none has been chosen yet, starts
        /// prompting the player to click another placed die. Resets the
        /// request if the Flow die is removed or the module doesn't need one.
        /// Only active DURING a turn (after Roll, before Resolve) - stays
        /// quiet while waiting for the next Roll, so it doesn't overwrite
        /// the "turn resolved, roll again" message.
        /// </summary>
        private void CheckIfTargetSelectionNeeded()
        {
            if (flowSlot == null || RunManager.Instance == null) return;
            if (_needsReroll) return; // between turns - nothing to target yet

            var state = RunManager.Instance.State;
            state.installedModules.TryGetValue(SlotType.Flow, out ModuleData flowModule);

            bool needsTarget = flowSlot.placedDie != null && ModuleNeedsTarget(flowModule);

            if (!needsTarget)
            {
                if (_awaitingTargetSelection) gameHUD?.ClearMessage();
                _awaitingTargetSelection = false;
                _flowTargetSlot = null;
                return;
            }

            if (_flowTargetSlot == null && !_awaitingTargetSelection)
            {
                _awaitingTargetSelection = true;
                gameHUD?.ShowMessage($"{flowModule.displayName}: click another placed die to choose its target.");
            }
        }

        private bool ModuleNeedsTarget(ModuleData module)
        {
            if (module == null) return false;
            return module.id == ModuleId.Mirror || module.id == ModuleId.Shift || module.id == ModuleId.Reverse;
        }

        /// <summary>
        /// Called by SlotDropZone.OnPointerClick. If a target selection is
        /// currently awaited, and the clicked slot holds a die and isn't
        /// Flow itself, that slot becomes Flow's chosen target - and its
        /// die is immediately previewed with Mirror/Shift/Reverse's Static
        /// (and Frequency, if already met) effect applied, instead of
        /// waiting for Resolve Turn.
        /// </summary>
        public void TryOnSlotClicked(SlotType clickedSlot)
        {
            if (!_awaitingTargetSelection) return;
            if (clickedSlot == SlotType.Flow) return;

            SlotDropZone clickedZone = GetSlotDropZone(clickedSlot);
            if (clickedZone == null || clickedZone.placedDie == null) return;

            _flowTargetSlot = clickedSlot;
            _awaitingTargetSelection = false;
            gameHUD?.ShowMessage($"Flow target selected: {clickedSlot} (showing {clickedZone.placedDie.rolledValue}).");
            Debug.Log($"[CombatController] Flow target selected: {clickedSlot}");

            PreviewFlowTargetEffect(clickedZone);
        }

        /// <summary>
        /// Immediately computes and shows Mirror/Shift/Reverse's effect on
        /// the just-chosen target die, using a lightweight preview context.
        /// This is a live preview only - ResolveTurn recomputes everything
        /// properly (including Resonance/enemy modifiers) when the turn is
        /// actually resolved, and will simply confirm the same result for
        /// Mirror/Shift/Reverse's own single-target effect. Total Inversion
        /// (Reverse's board-wide Frequency) is NOT previewed here - it only
        /// applies at Resolve Turn, since it needs every slot's die.
        /// </summary>
        private void PreviewFlowTargetEffect(SlotDropZone targetZone)
        {
            var state = RunManager.Instance.State;
            state.installedModules.TryGetValue(SlotType.Flow, out ModuleData flowModule);
            if (!ModuleNeedsTarget(flowModule)) return;
            if (flowSlot == null || flowSlot.placedDie == null) return;

            int coreValue = diceRoller.LastCoreRolledValue;
            var previewCtx = new CombatContext
            {
                CoreValue = coreValue,
                CoreRange = state.coreDie != null ? state.coreDie.GetRange(coreValue) : ValueRange.DeadZone,
                CoreIsEven = state.coreDie != null && state.coreDie.IsEven(coreValue),
                InhibitedValue = enemyController != null ? enemyController.LastInhibitedValue : debugInhibitedValue,
                TargetDieValue = targetZone.placedDie.rolledValue,
                TargetDieFaces = targetZone.placedDie.dieType != null ? targetZone.placedDie.dieType.faces : 6
            };

            ModuleResult preview = ModuleResolver.ResolveSlot(flowModule, flowSlot.placedDie.rolledValue, previewCtx, ignoreInhibition: false, forceFrequency: false);

            if (preview.NewTargetValue.HasValue)
            {
                bool nowInhibited = ModuleResolver.IsValueInhibited(preview.NewTargetValue.Value, previewCtx);
                targetZone.placedDie.OverrideValue(preview.NewTargetValue.Value, nowInhibited);
                Debug.Log($"[CombatController] Preview: {flowModule.displayName} changes target to {preview.NewTargetValue.Value}");
            }
        }

        /// <summary>
        /// Called by SlotDropZone.Clear() whenever a slot loses its placed
        /// die. If the cleared slot was Flow's chosen target, resets the
        /// target selection so the player can pick a new one - the die's
        /// value itself is already restored to its original roll by
        /// DraggableDie.ReturnToStart(), so this only resets Flow's memory
        /// of "which slot is the target", not the value.
        /// </summary>
        public void NotifySlotCleared(SlotType clearedSlot)
        {
            // If the Flow module's own die is removed, there's nothing left
            // to activate Mirror/Shift/Reverse with - cancel any pending
            // target request entirely.
            if (clearedSlot == SlotType.Flow)
            {
                if (_flowTargetSlot.HasValue || _awaitingTargetSelection)
                {
                    _flowTargetSlot = null;
                    _awaitingTargetSelection = false;
                    gameHUD?.ClearMessage();
                    Debug.Log("[CombatController] Flow's own die was removed - target selection cancelled.");
                }
                return;
            }

            if (!_flowTargetSlot.HasValue || _flowTargetSlot.Value != clearedSlot) return;

            _flowTargetSlot = null;

            var state = RunManager.Instance?.State;
            if (state != null)
            {
                state.installedModules.TryGetValue(SlotType.Flow, out ModuleData flowModule);
                if (ModuleNeedsTarget(flowModule) && flowSlot != null && flowSlot.placedDie != null)
                {
                    _awaitingTargetSelection = true;
                    gameHUD?.ShowMessage($"Target removed. {flowModule.displayName}: click another placed die to choose its target.");
                    Debug.Log("[CombatController] Flow target slot was cleared - awaiting a new target selection.");
                }
            }
        }

        /// <summary>
        /// Call this after the player has placed dice in the slots and wants
        /// to resolve the turn (the CHECK phase).
        /// </summary>
        public void ResolveTurn()
        {
            if (_needsReroll)
            {
                gameHUD?.ShowMessage("Roll the dice before resolving the next turn!");
                Debug.LogWarning("[CombatController] ResolveTurn blocked - dice haven't been rolled for this turn yet.");
                return;
            }

            var state = RunManager.Instance.State;

            state.installedModules.TryGetValue(SlotType.Flow, out ModuleData flowModuleCheck);
            bool flowHasDiePlaced = flowSlot != null && flowSlot.placedDie != null;
            if (flowHasDiePlaced && ModuleNeedsTarget(flowModuleCheck) && _flowTargetSlot == null)
            {
                gameHUD?.ShowMessage($"Choose {flowModuleCheck.displayName}'s target die first (click another placed die)!");
                Debug.LogWarning("[CombatController] ResolveTurn blocked - Flow's target die hasn't been chosen yet.");
                return;
            }

            // --- Gather raw placed values + die types ---
            var placedValues = new Dictionary<SlotType, int>();
            var placedDieTypes = new Dictionary<SlotType, DieData>();
            if (powerSlot != null && powerSlot.placedDie != null) { placedValues[SlotType.Power] = powerSlot.placedDie.rolledValue; placedDieTypes[SlotType.Power] = powerSlot.placedDie.dieType; }
            if (stabilitySlot != null && stabilitySlot.placedDie != null) { placedValues[SlotType.Stability] = stabilitySlot.placedDie.rolledValue; placedDieTypes[SlotType.Stability] = stabilitySlot.placedDie.dieType; }
            if (flowSlot != null && flowSlot.placedDie != null) { placedValues[SlotType.Flow] = flowSlot.placedDie.rolledValue; placedDieTypes[SlotType.Flow] = flowSlot.placedDie.dieType; }
            if (echoSlot != null && echoSlot.placedDie != null) { placedValues[SlotType.Echo] = echoSlot.placedDie.rolledValue; placedDieTypes[SlotType.Echo] = echoSlot.placedDie.dieType; }

            CombatContext ctx = BuildContext(state, placedValues.ContainsKey(SlotType.Flow));

            if (_flowTargetSlot.HasValue)
            {
                SlotDropZone targetZone = GetSlotDropZone(_flowTargetSlot.Value);
                if (targetZone != null && targetZone.placedDie != null)
                {
                    ctx.TargetDieValue = targetZone.placedDie.rolledValue;
                    ctx.TargetDieFaces = targetZone.placedDie.dieType != null ? targetZone.placedDie.dieType.faces : 6;
                }
            }

            // --- Resolve Flow's static/frequency manipulation FIRST (Mirror/Shift/Reverse),
            //     since it can change values that Power/Stability/Echo then need to use ---
            ModuleResult flowResult = default;
            bool flowIsManipulator = ModuleNeedsTarget(flowModuleCheck);
            if (placedValues.ContainsKey(SlotType.Flow))
            {
                bool flowSelfIgnoresInhibition = false; // Legame/Resonance status isn't known yet at this point - acceptable simplification
                flowResult = ModuleResolver.ResolveSlot(flowModuleCheck, placedValues[SlotType.Flow], ctx, flowSelfIgnoresInhibition, forceFrequency: false);

                if (flowIsManipulator)
                {
                    ApplyFlowTargetEffect(flowModuleCheck, flowResult, placedValues, placedDieTypes);
                }
            }

            // --- Enemy ability die-value modifiers (Architect/Prism/Null-Pointer) ---
            var abilityCtx = new EnemyAbilityContext { Ctx = ctx, PlacedValues = placedValues, InstalledModules = state.installedModules, State = state, Enemy = enemyController };
            if (enemyController != null)
            {
                var keys = new List<SlotType>(placedValues.Keys);
                foreach (var slot in keys)
                {
                    int modified = enemyController.ModifyDieValue(slot, placedValues[slot], placedDieTypes.ContainsKey(slot) ? placedDieTypes[slot] : null, abilityCtx);
                    if (modified != placedValues[slot])
                    {
                        placedValues[slot] = modified;
                        Debug.Log($"[CombatController] Enemy ability modified {slot} die to {modified}");
                    }
                }
            }

            // --- Second Chance reroll heuristic ---
            // No interactive "pick a die to reroll" UI exists yet, so this
            // automatically rerolls the LOWEST-value unplaced hand dice
            // first (the most sensible default choice for an automated
            // heuristic: improve your worst options). Frequency (High DCD)
            // rerolls up to 3 dice and keeps the better of old/new per die.
            if (flowResult.DiceToReroll > 0 && diceRoller != null)
            {
                var handDice = diceRoller.GetUnplacedHandDice();
                handDice.Sort((a, b) => a.rolledValue.CompareTo(b.rolledValue));

                int rerolled = 0;
                for (int i = 0; i < handDice.Count && rerolled < flowResult.DiceToReroll; i++)
                {
                    DraggableDie die = handDice[i];
                    if (die.dieType == null) continue;

                    int oldValue = die.rolledValue;
                    int newValue = Random.Range(1, die.dieType.faces + 1);
                    int finalValue = flowResult.RerollKeepsBetterResult ? Mathf.Max(oldValue, newValue) : newValue;

                    bool nowInhibited = ModuleResolver.IsValueInhibited(finalValue, ctx);
                    die.Setup(die.dieType, finalValue, locked: false, inhibited: nowInhibited);
                    Debug.Log($"[CombatController] Second Chance reroll: {oldValue} -> {finalValue}");
                    rerolled++;
                }

                if (rerolled > 0 && enemyController != null)
                {
                    int wardenDamage = enemyController.NotifyFlowRerollUsed(abilityCtx);
                    if (wardenDamage > 0)
                    {
                        state.currentHp = Mathf.Max(0, state.currentHp - wardenDamage);
                        Debug.Log($"[CombatController] Warden Stasis: reroll used, -{wardenDamage} HP direct (HP now {state.currentHp}/{state.maxHp})");
                    }
                }
            }

            // --- Extra/permanent Inhibited values from enemy abilities (Sentinel/Judge) ---
            // Computed BEFORE SyncDieVisuals so the die's inhibited indicator
            // can account for all three inhibition sources, not just the base roll.
            if (enemyController != null)
            {
                ctx.ExtraInhibitedValue = enemyController.GetExtraInhibitedValue(abilityCtx);
                ctx.PermanentlyInhibitedValues = enemyController.GetPermanentlyInhibitedValues();
            }

            // Sync the actual on-screen dice with any manipulation so far (Flow + enemy modifiers).
            SyncDieVisuals(placedValues, ctx);

            // --- Resonance detection uses the final (post-manipulation) values ---
            HashSet<SlotType> legameSlots = ResonanceDetector.DetectLegameSlots(placedValues);
            bool fullResonance = ResonanceDetector.DetectFullResonance(placedValues, ctx.CoreValue);
            ctx.FullResonanceActive = fullResonance;

            if (fullResonance)
            {
                Debug.Log("[CombatController] *** FULL RESONANCE *** Automatic Critical Success, Module Overload (Static+Frequency forced on all 4), double Scrap.");
            }
            else if (legameSlots.Count > 0)
            {
                Debug.Log($"[CombatController] Resonance Legame active on: {string.Join(", ", legameSlots)} (these slots ignore Inhibition)");
            }

            // --- Real enemy Threshold (base + ability modifier), or debug fallback ---
            int effectiveThreshold = enemyController != null
                ? enemyController.GetEffectiveThreshold(abilityCtx)
                : debugThreshold;

            int powerDieBonus = state.pendingNextTurnPowerBonus;
            state.pendingNextTurnPowerBonus = 0;

            // --- Pass 1: Power ---
            state.installedModules.TryGetValue(SlotType.Power, out ModuleData powerModule);
            int? powerDieValue = placedValues.ContainsKey(SlotType.Power) ? placedValues[SlotType.Power] : (int?)null;

            bool powerIgnoresInhibition = fullResonance || legameSlots.Contains(SlotType.Power);
            ModuleResult powerResult = ModuleResolver.ResolveSlot(powerModule, powerDieValue, ctx, powerIgnoresInhibition, forceFrequency: fullResonance);
            int powerTotal = powerResult.ValueBonus + powerDieBonus;
            Debug.Log($"[CombatController] Power: {powerResult.DebugLog} (+{powerDieBonus} carried Overload bonus) = {powerTotal}");

            bool success = fullResonance || powerTotal >= effectiveThreshold;
            int excess = Mathf.Max(0, powerTotal - effectiveThreshold);
            ctx.PointsExceedingThreshold = excess;

            // --- Pass 2: Stability, Echo (Flow was already resolved in the pre-pass above) ---
            ModuleResult stabilityResult = ResolveAndLog(SlotType.Stability, stabilitySlot, state, ctx, placedValues, legameSlots, fullResonance);
            ModuleResult echoResult = ResolveAndLog(SlotType.Echo, echoSlot, state, ctx, placedValues, legameSlots, fullResonance);

            ApplyResultToState(state, powerResult, fullResonance);
            ApplyResultToState(state, stabilityResult, fullResonance);
            ApplyResultToState(state, flowResult, fullResonance);
            ApplyResultToState(state, echoResult, fullResonance);

            if (success && stabilityResult.NextThresholdReductionPercent > 0f)
            {
                state.pendingThresholdReductionPercent = stabilityResult.NextThresholdReductionPercent;
                Debug.Log($"[CombatController] Aiming applied: next Threshold will drop by {stabilityResult.NextThresholdReductionPercent:P0}");
            }

            Debug.Log($"[CombatController] Power Total = {powerTotal} vs Threshold {effectiveThreshold} -> {(success ? "SUCCESS" : "FAILURE")}{(fullResonance ? " (forced by Full Resonance)" : "")}");

            if (success)
            {
                int scrapFromCombat = fullResonance ? excess * 2 : excess;
                state.scrap += scrapFromCombat;
                Debug.Log($"[CombatController] Combat Scrap gained: {scrapFromCombat} (excess over Threshold{(fullResonance ? ", doubled by Resonance" : "")})");

                int damageToEnemy = Mathf.Max(1, excess);
                enemyController?.ApplyDamage(damageToEnemy);
            }
            else
            {
                int rawDamage = Mathf.Max(1, effectiveThreshold - powerTotal);
                int bonusEnemyDamage = enemyController != null ? enemyController.GetBonusDamageOnFailure(abilityCtx) : 0;
                int reducedDamage = Mathf.Max(1, rawDamage - stabilityResult.DamageReduction) + bonusEnemyDamage;
                state.currentHp = Mathf.Max(0, state.currentHp - reducedDamage);
                Debug.Log($"[CombatController] Player took {reducedDamage} damage (raw {rawDamage}, reduced by {stabilityResult.DamageReduction}, +{bonusEnemyDamage} enemy ability bonus). HP now {state.currentHp}/{state.maxHp}");
            }

            CheckFightOutcome();

            // --- Notify the enemy's ability that the turn ended (e.g. Sovereign locks in the Core value) ---
            enemyController?.NotifyTurnEnd(abilityCtx);

            _flowTargetSlot = null;
            _awaitingTargetSelection = false;
            _needsReroll = true;
            diceRoller?.DiscardCurrentHand();
            diceRoller?.SetRollButtonInteractable(true);
            if (resolveButton != null) resolveButton.interactable = false;
            gameHUD?.RefreshStats();
            gameHUD?.ShowMessage("Turn resolved. Roll the dice to continue.");
        }

        /// <summary>
        /// Applies Mirror/Shift/Reverse's target-die mutation directly into
        /// the working placedValues dictionary (used before Resonance and
        /// enemy modifiers run). Reverse is special-cased: if its Frequency
        /// (Total Inversion) triggered, every occupied slot is flipped
        /// instead of just the chosen target.
        /// </summary>
        private void ApplyFlowTargetEffect(ModuleData flowModule, ModuleResult flowResult, Dictionary<SlotType, int> placedValues, Dictionary<SlotType, DieData> placedDieTypes)
        {
            if (flowModule == null) return;

            if (flowModule.id == ModuleId.Reverse && flowResult.FrequencyTriggered)
            {
                foreach (SlotType slot in new[] { SlotType.Power, SlotType.Stability, SlotType.Flow, SlotType.Echo })
                {
                    if (!placedValues.ContainsKey(slot) || !placedDieTypes.ContainsKey(slot) || placedDieTypes[slot] == null) continue;
                    int faces = placedDieTypes[slot].faces;
                    int current = placedValues[slot];
                    placedValues[slot] = faces + 1 - current;
                    Debug.Log($"[CombatController] Total Inversion: {slot} die {current} -> {placedValues[slot]}");
                }
                return;
            }

            if (_flowTargetSlot.HasValue && flowResult.NewTargetValue.HasValue)
            {
                int oldValue = placedValues.ContainsKey(_flowTargetSlot.Value) ? placedValues[_flowTargetSlot.Value] : 0;
                placedValues[_flowTargetSlot.Value] = flowResult.NewTargetValue.Value;
                Debug.Log($"[CombatController] Flow target {_flowTargetSlot.Value}: {oldValue} -> {flowResult.NewTargetValue.Value}");
            }
        }

        /// <summary>
        /// Pushes final effective values onto the actual on-screen dice
        /// (so any Mirror/Shift/Reverse/enemy-ability manipulation is
        /// visible), and recalculates whether each changed die now counts
        /// as inhibited under the current turn's full inhibition context.
        /// </summary>
        private void SyncDieVisuals(Dictionary<SlotType, int> placedValues, CombatContext ctx)
        {
            foreach (var kvp in placedValues)
            {
                SlotDropZone zone = GetSlotDropZone(kvp.Key);
                if (zone != null && zone.placedDie != null && zone.placedDie.rolledValue != kvp.Value)
                {
                    bool nowInhibited = ModuleResolver.IsValueInhibited(kvp.Value, ctx);
                    zone.placedDie.OverrideValue(kvp.Value, nowInhibited);
                }
            }
        }

        private SlotDropZone GetSlotDropZone(SlotType slot)
        {
            switch (slot)
            {
                case SlotType.Power: return powerSlot;
                case SlotType.Stability: return stabilitySlot;
                case SlotType.Flow: return flowSlot;
                case SlotType.Echo: return echoSlot;
                default: return null;
            }
        }

        /// <summary>
        /// Called by DiceRoller right after RollAllDice(). Clears the
        /// "needs reroll" gate, resets Flow's target selection (dice are
        /// fresh this turn), and refreshes each slot's module info labels.
        /// </summary>
        public void NotifyDiceRolled()
        {
            _needsReroll = false;
            _flowTargetSlot = null;
            _awaitingTargetSelection = false;
            if (resolveButton != null) resolveButton.interactable = true;
            gameHUD?.ClearMessage();
            RefreshSlotLabels();
        }

        public void RefreshSlotLabels()
        {
            var state = RunManager.Instance.State;

            state.installedModules.TryGetValue(SlotType.Power, out ModuleData powerModule);
            state.installedModules.TryGetValue(SlotType.Stability, out ModuleData stabilityModule);
            state.installedModules.TryGetValue(SlotType.Flow, out ModuleData flowModule);
            state.installedModules.TryGetValue(SlotType.Echo, out ModuleData echoModule);

            powerSlot?.SetModuleInfo(powerModule);
            stabilitySlot?.SetModuleInfo(stabilityModule);
            flowSlot?.SetModuleInfo(flowModule);
            echoSlot?.SetModuleInfo(echoModule);
        }

        private void CheckFightOutcome()
        {
            var state = RunManager.Instance.State;

            if (enemyController != null && enemyController.IsDefeated)
            {
                Debug.Log("[CombatController] VICTORY! Enemy defeated.");
            }
            else if (state.IsDefeated)
            {
                Debug.Log("[CombatController] DEFEAT! Player HP reached 0 - Fragmentation.");
                RunManager.Instance.HandleDefeat();
            }
        }

        private void ApplyResultToState(GameState state, ModuleResult result, bool fullResonance)
        {
            if (result.HpRecovered > 0)
            {
                state.currentHp = Mathf.Min(state.maxHp, state.currentHp + result.HpRecovered);
            }

            if (result.ScrapGained > 0)
            {
                int scrapToAdd = fullResonance ? result.ScrapGained * 2 : result.ScrapGained;
                state.scrap += scrapToAdd;
            }

            if (result.ChargesGenerated > 0)
            {
                state.changeoverCharges += result.ChargesGenerated;
                while (state.changeoverCharges >= 10)
                {
                    state.changeoverCharges -= 10;
                    if (state.coreDie != null)
                    {
                        state.pendingChangeoverBonusDice.Add(state.coreDie);
                        Debug.Log($"[CombatController] Changeover: 10 Charges reached - a bonus {state.coreDie.displayName} (copy of the Core Die) will be rolled on the NEXT turn only, then removed from the game.");
                    }
                }
            }

            if (result.NextShopDiscountPercent > 0f)
            {
                // Keep the best discount if somehow more than one source triggers in the same turn.
                state.pendingShopDiscountPercent = Mathf.Max(state.pendingShopDiscountPercent, result.NextShopDiscountPercent);
            }

            if (result.NextTurnPowerBonus > 0)
            {
                state.pendingNextTurnPowerBonus += result.NextTurnPowerBonus;
            }
        }

        public void ResetPlacement()
        {
            powerSlot?.ReturnPlacedDie();
            stabilitySlot?.ReturnPlacedDie();
            flowSlot?.ReturnPlacedDie();
            echoSlot?.ReturnPlacedDie();

            _flowTargetSlot = null;
            _awaitingTargetSelection = false;

            Debug.Log("[CombatController] Placement reset - all dice returned to hand.");
        }

        /// <summary>
        /// Computes the enemy's current effective Threshold for display in
        /// the HUD (base Threshold + permanent bonuses + ability modifier),
        /// using whatever is known right now (Core roll, Inhibitor roll).
        /// Call after Roll and after Resolve to keep the HUD in sync.
        /// Note: a few abilities that react to specific slot placements
        /// (evaluated only at Resolve time) won't be reflected in this
        /// preview - this is the best available estimate before placement.
        /// </summary>
        public int PreviewEffectiveThreshold()
        {
            if (enemyController == null) return debugThreshold;

            var state = RunManager.Instance.State;
            CombatContext ctx = BuildContext(state, flowSlotOccupied: false);
            var abilityCtx = new EnemyAbilityContext
            {
                Ctx = ctx,
                PlacedValues = new Dictionary<SlotType, int>(),
                InstalledModules = state.installedModules,
                State = state,
                Enemy = enemyController
            };
            return enemyController.GetEffectiveThreshold(abilityCtx);
        }

        private CombatContext BuildContext(GameState state, bool flowSlotOccupied)
        {
            int coreValue = diceRoller.LastCoreRolledValue;
            ValueRange coreRange = state.coreDie != null ? state.coreDie.GetRange(coreValue) : ValueRange.DeadZone;
            bool coreIsEven = state.coreDie != null && state.coreDie.IsEven(coreValue);

            int inhibitedValue = enemyController != null ? enemyController.LastInhibitedValue : debugInhibitedValue;

            return new CombatContext
            {
                CoreValue = coreValue,
                CoreRange = coreRange,
                CoreIsEven = coreIsEven,
                InhibitedValue = inhibitedValue,
                PlayerHp = state.currentHp,
                PlayerHpMax = state.maxHp,
                FlowSlotOccupied = flowSlotOccupied,
                FullResonanceActive = false
            };
        }

        private ModuleResult ResolveAndLog(
            SlotType slot,
            SlotDropZone dropZone,
            GameState state,
            CombatContext ctx,
            Dictionary<SlotType, int> placedValues,
            HashSet<SlotType> legameSlots,
            bool fullResonance)
        {
            state.installedModules.TryGetValue(slot, out ModuleData module);

            int? placedValue = placedValues.ContainsKey(slot) ? placedValues[slot] : (int?)null;
            bool ignoreInhibition = fullResonance || legameSlots.Contains(slot);

            ModuleResult result = ModuleResolver.ResolveSlot(module, placedValue, ctx, ignoreInhibition, forceFrequency: fullResonance);
            Debug.Log($"[CombatController] {slot}: {result.DebugLog} (ValueBonus={result.ValueBonus}, Hp+={result.HpRecovered}, Scrap+={result.ScrapGained})");

            return result;
        }
    }
}

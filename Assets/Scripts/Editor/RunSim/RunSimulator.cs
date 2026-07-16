using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rollrate.Core;
using Rollrate.Data;
using Rollrate.Combat;
using Rollrate.Map;
using Rollrate.Shop;

namespace Rollrate.Simulation
{
    /// <summary>
    /// Headless simulator for the FULL game loop (Map -> every node type ->
    /// Terminal/Recalibration -> Meta), not just isolated fights like
    /// BalanceSimulationRunner. Reuses the same rule classes for combat
    /// (ModuleResolver/ResonanceDetector/EnemyController) and the same data
    /// assets (ShopOfferPools, ArchiveTestTable, DismantleRewardTable,
    /// EnemyRegistry) as the real game.
    ///
    /// Definitions (per user's own terms):
    /// - "Run": one attempt from Grade I Page 1 until either Defeat or
    ///   reaching full Victory (Grade V Guardian defeated).
    /// - "Campaign" (una "partita"): repeated runs, carrying Frammenti
    ///   Residui/unlocks between them (SimMetaState, NOT the real
    ///   PlayerPrefs-backed MetaProgressionManager), until Victory. Then a
    ///   brand new Campaign starts with a completely fresh SimMetaState -
    ///   simulating an independent player's first-ever playthrough.
    ///
    /// SIMPLIFICATIONS (documented, not silent - same spirit as
    /// BalanceSimulationRunner's heuristics):
    /// - Combat/placement heuristics are identical to BalanceSimulationRunner's
    ///   (highest die -> Power/Stability/Flow/Echo priority order, lowest-
    ///   value Second Chance rerolls, lowest-other-die Mirror/Shift/Reverse
    ///   targeting).
    /// - Every turn rolls the ENTIRE dicePool (not a drawn 6-hand) - same
    ///   simplification as the combat-only simulator.
    /// - Shop buying: prioritizes an empty-slot module first, then dice,
    ///   then spare modules, buying anything affordable until Scrap runs low.
    /// - Dismantle: dismantles a spare die whenever the pool exceeds 6, and
    ///   occasionally (30% chance) a spare owned module beyond the first per slot.
    /// - Bonfire also randomly (30% chance per slot) swaps the equipped
    ///   module for a different owned one, to generate SOME Collection-swap
    ///   activity for stats - not a "smart" evaluation of which is better.
    /// - Glitch reveals as a uniformly random OTHER type (Conflict/Merchant/
    ///   Archive/Overload/Bonfire/Dismantle), approximating the real reveal
    ///   without needing MapController's exact table.
    /// </summary>
    public static class RunSimulator
    {
        public static RunSimStats SimulateCampaigns(RunSimulatorConfig config, int campaignCount, Action<string> logProgress = null)
        {
            var stats = new RunSimStats();
            for (int i = 0; i < campaignCount; i++)
            {
                SimulateOneCampaign(config, stats);
                if (logProgress != null && (i + 1) % Mathf.Max(1, campaignCount / 20) == 0)
                {
                    logProgress($"Campagna {i + 1}/{campaignCount} completata.");
                }
            }
            return stats;
        }

        private static void SimulateOneCampaign(RunSimulatorConfig config, RunSimStats stats)
        {
            var simMeta = new SimMetaState();
            int runs = 0;
            bool victory = false;

            while (!victory && runs < config.maxRunsPerCampaign)
            {
                runs++;
                victory = SimulateOneRun(config, simMeta, stats);
            }

            if (!victory) stats.AbandonedCampaigns++;
            stats.RunsPerCampaign.Add(runs);
        }

        private static bool SimulateOneRun(RunSimulatorConfig config, SimMetaState simMeta, RunSimStats stats)
        {
            var state = new GameState();
            state.ResetForNewRun(config.startingCoreDie, config.startingHp);

            if (config.startingPool != null)
            {
                foreach (DieData die in config.startingPool)
                {
                    if (die != null) state.AddDieToPool(die);
                }
            }

            InstallStarting(state, SlotType.Power, config.startingPowerModule);
            InstallStarting(state, SlotType.Stability, config.startingStabilityModule);
            InstallStarting(state, SlotType.Flow, config.startingFlowModule);
            InstallStarting(state, SlotType.Echo, config.startingEchoModule);

            var enemyGO = new GameObject("Sim_Enemy_Temp") { hideFlags = HideFlags.HideAndDontSave };
            var enemyController = enemyGO.AddComponent<EnemyController>();
            EnemyController.VerboseLogging = false;

            try
            {
                while (true)
                {
                    MapPage page = MapGenerator.GeneratePage(state.currentPage, state.currentEchelon);
                    MapNode current = page.EntryColumn[0];
                    bool advancedGradeViaTerminal = false;

                    while (current.column < page.columns.Count - 1)
                    {
                        if (current.connectionsToNextColumn.Count == 0) break; // safety, shouldn't happen
                        int nextRow = current.connectionsToNextColumn[UnityEngine.Random.Range(0, current.connectionsToNextColumn.Count)];
                        MapNode next = page.columns[current.column + 1][nextRow];

                        NodeType resolvedType = next.type;
                        if (resolvedType == NodeType.Glitch)
                        {
                            NodeType[] options = { NodeType.Conflict, NodeType.Merchant, NodeType.Archive, NodeType.Overload, NodeType.Bonfire, NodeType.Dismantle };
                            resolvedType = options[UnityEngine.Random.Range(0, options.Length)];
                        }

                        bool isTerminal = next.type == NodeType.Terminal;
                        EnemyTier? fightTier = null;
                        if (isTerminal) fightTier = EnemyTier.Guardian;
                        else if (resolvedType == NodeType.Overload) fightTier = EnemyTier.Elite;
                        else if (resolvedType == NodeType.Conflict) fightTier = EnemyTier.Base;

                        if (fightTier.HasValue)
                        {
                            if (!ResolveFight(config, state, enemyController, fightTier.Value, stats)) return false; // died
                        }
                        else
                        {
                            switch (resolvedType)
                            {
                                case NodeType.Merchant: ResolveShop(config, state, simMeta, stats); break;
                                case NodeType.Archive: if (!ResolveArchive(config, state, stats)) return false; break;
                                case NodeType.Bonfire: ResolveBonfire(state, stats); break;
                                case NodeType.Dismantle: ResolveDismantle(config, state, stats); break;
                            }
                        }

                        if (isTerminal)
                        {
                            bool wonGradeFive = ApplyRecalibration(state, simMeta);
                            if (wonGradeFive) return true; // VICTORY
                            advancedGradeViaTerminal = true;
                            break;
                        }

                        current = next;
                    }

                    if (!advancedGradeViaTerminal)
                    {
                        state.currentPage++;
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyGO);
            }
        }

        /// <summary>Applies Tax + mandatory Core evolution, then advances Grade. Returns true if this was the Grade V victory.</summary>
        private static bool ApplyRecalibration(GameState state, SimMetaState simMeta)
        {
            float[] taxByGrade = { 0.10f, 0.15f, 0.20f, 0.25f };
            if (state.currentEchelon <= taxByGrade.Length)
            {
                int tax = Mathf.CeilToInt(state.scrap * taxByGrade[state.currentEchelon - 1]);
                state.scrap -= tax;
            }

            if (state.coreDie.nextTier != null) state.coreDie = state.coreDie.nextTier;

            if (state.currentEchelon < 5)
            {
                state.currentEchelon++;
                state.currentPage = 1;
                return false;
            }

            simMeta.AwardForRunComplete();
            return true;
        }

        private static void InstallStarting(GameState state, SlotType slot, ModuleData module)
        {
            if (module == null) return;
            state.installedModules[slot] = module;
            state.AddOwnedModule(module);
        }

        // ------------------------------------------------------------------
        // Node resolvers
        // ------------------------------------------------------------------

        private static bool ResolveFight(RunSimulatorConfig config, GameState state, EnemyController enemyController, EnemyTier tier, RunSimStats stats)
        {
            EnemyData enemy = config.enemyRegistry.GetRandom(state.currentEchelon, tier);
            if (enemy == null) return true; // no matching enemy configured - skip rather than crash the simulation

            foreach (var kvp in state.installedModules)
            {
                stats.RecordModuleUsage(kvp.Value != null ? kvp.Value.displayName : null);
            }

            enemyController.StartFight(enemy);
            return SimulateFightToCompletion(state, enemyController, config.maxTurnsPerFight);
        }

        private static void ResolveShop(RunSimulatorConfig config, GameState state, SimMetaState simMeta, RunSimStats stats)
        {
            GradeOfferPool pool = GetPoolWithSimUnlocks(config.offerPools, state.currentEchelon, simMeta);

            // Build up to 6 offers, 2-4 split between dice/modules (mirrors ShopController).
            int moduleCount = UnityEngine.Random.Range(2, 5);
            int dieCount = 6 - moduleCount;

            var offerModules = PickRandom(pool.modules, moduleCount);
            var offerDice = PickRandom(pool.dice, dieCount);

            foreach (DieData die in offerDice)
            {
                int cost = config.costTable.GetNewDieCost(state.currentEchelon);
                if (state.scrap < cost) continue;
                state.scrap -= cost;
                state.AddDieToPool(die);
                stats.RecordPurchase(die.displayName);
            }

            foreach (ModuleData module in offerModules)
            {
                int cost = config.costTable.GetNewModuleCost(state.currentEchelon);
                if (state.scrap < cost) continue;

                bool slotEmpty = !state.installedModules.ContainsKey(module.slot) || state.installedModules[module.slot] == null;
                // Buy if the slot is empty (fills a gap) or we can still comfortably afford it afterward.
                if (slotEmpty || state.scrap >= cost * 2)
                {
                    state.scrap -= cost;
                    state.AddOwnedModule(module);
                    if (slotEmpty) state.EquipModule(module);
                    stats.RecordPurchase($"{module.displayName} ({module.slot})");
                }
            }
        }

        private static GradeOfferPool GetPoolWithSimUnlocks(ShopOfferPools offerPools, int currentEchelon, SimMetaState simMeta)
        {
            GradeOfferPool basePool = offerPools.GetForGrade(currentEchelon);
            var combinedDice = new List<DieData>(basePool.dice);
            var combinedModules = new List<ModuleData>(basePool.modules);

            GradeOfferPool[] all = { offerPools.gradeI, offerPools.gradeII, offerPools.gradeIII, offerPools.gradeIV, offerPools.gradeV };
            foreach (GradeOfferPool p in all)
            {
                foreach (DieData d in p.dice)
                {
                    if (d == null || d.grade <= currentEchelon) continue;
                    if (simMeta.GetEffectiveGrade(d.name, d.grade) <= currentEchelon) combinedDice.Add(d);
                }
                foreach (ModuleData m in p.modules)
                {
                    if (m == null || m.grade <= currentEchelon) continue;
                    if (simMeta.GetEffectiveGrade(m.name, m.grade) <= currentEchelon) combinedModules.Add(m);
                }
            }

            return new GradeOfferPool { dice = combinedDice.Distinct().ToArray(), modules = combinedModules.Distinct().ToArray() };
        }

        private static List<T> PickRandom<T>(T[] pool, int count) where T : UnityEngine.Object
        {
            var result = new List<T>();
            if (pool == null || pool.Length == 0 || count <= 0) return result;
            var copy = new List<T>(pool);
            for (int i = 0; i < count; i++)
            {
                if (copy.Count == 0) copy = new List<T>(pool);
                int idx = UnityEngine.Random.Range(0, copy.Count);
                result.Add(copy[idx]);
                copy.RemoveAt(idx);
            }
            return result;
        }

        /// <summary>Returns false if this Test's failure dropped the player's HP to 0 (Ambizione can kill).</summary>
        private static bool ResolveArchive(RunSimulatorConfig config, GameState state, RunSimStats stats)
        {
            var table = config.archiveTestTable;
            int grade = state.currentEchelon;
            int testRoll = UnityEngine.Random.Range(0, 3);

            if (testRoll == 0) // Resonance
            {
                int coreRoll = UnityEngine.Random.Range(1, state.coreDie.faces + 1);
                bool win = coreRoll >= table.GetResonanceThreshold(grade);
                stats.ArchiveResonanceTotal++;
                if (win) { stats.ArchiveResonanceWins++; state.scrap += table.GetResonanceReward(grade); }
                else { state.scrap = Mathf.Max(0, state.scrap - table.GetResonancePenalty(grade)); }
            }
            else if (testRoll == 1) // Tribute
            {
                int sum = 0;
                foreach (DieData die in state.dicePool) sum += UnityEngine.Random.Range(1, die.faces + 1);
                bool win = sum >= table.GetTributeThreshold(grade);
                stats.ArchiveTributeTotal++;

                if (state.dicePool.Count > 0)
                {
                    DieData target = state.dicePool[UnityEngine.Random.Range(0, state.dicePool.Count)];
                    if (win)
                    {
                        stats.ArchiveTributeWins++;
                        if (target.nextTier != null) state.ReplaceDieEverywhere(target, target.nextTier);
                    }
                    else
                    {
                        state.RemoveDiePermanently(target);
                    }
                }
            }
            else // Ambition
            {
                int coreRoll = UnityEngine.Random.Range(1, state.coreDie.faces + 1);
                int bestPool = 0;
                foreach (DieData die in state.dicePool)
                {
                    int roll = UnityEngine.Random.Range(1, die.faces + 1);
                    if (roll > bestPool) bestPool = roll;
                }
                bool win = (coreRoll + bestPool) >= table.GetAmbitionThreshold(grade);
                stats.ArchiveAmbitionTotal++;

                if (win)
                {
                    stats.ArchiveAmbitionWins++;
                    state.scrap += table.GetAmbitionRewardScrap(grade);
                }
                else
                {
                    int hpLoss = Mathf.CeilToInt(state.maxHp * 0.2f);
                    state.currentHp = Mathf.Max(0, state.currentHp - hpLoss);
                    if (state.IsDefeated) return false; // Ambizione killed the player
                }
            }

            return true;
        }

        private static void ResolveBonfire(GameState state, RunSimStats stats)
        {
            int missing = state.maxHp - state.currentHp;
            state.currentHp = Mathf.Min(state.maxHp, state.currentHp + Mathf.CeilToInt(missing / 2f));

            // Heuristic Collection activity: 30% chance per slot to swap to a different owned module.
            foreach (SlotType slot in System.Enum.GetValues(typeof(SlotType)))
            {
                if (!state.ownedModules.TryGetValue(slot, out var owned) || owned.Count < 2) continue;
                if (UnityEngine.Random.value >= 0.3f) continue;

                state.installedModules.TryGetValue(slot, out ModuleData current);
                var alternatives = owned.Where(m => m != current).ToList();
                if (alternatives.Count == 0) continue;

                state.EquipModule(alternatives[UnityEngine.Random.Range(0, alternatives.Count)]);
                stats.CollectionSwapCount++;
            }
        }

        private static void ResolveDismantle(RunSimulatorConfig config, GameState state, RunSimStats stats)
        {
            if (state.dicePool.Count > 6 && state.CanDismantleDie())
            {
                DieData target = state.dicePool[UnityEngine.Random.Range(0, state.dicePool.Count)];
                state.RemoveDiePermanently(target);
                state.scrap += config.dismantleRewardTable.GetDieScrap(target.grade);
                stats.DismantleCount++;
            }

            if (UnityEngine.Random.value < 0.3f)
            {
                foreach (SlotType slot in System.Enum.GetValues(typeof(SlotType)))
                {
                    if (!state.ownedModules.TryGetValue(slot, out var owned) || owned.Count < 2) continue;
                    state.installedModules.TryGetValue(slot, out ModuleData equipped);
                    var spares = owned.Where(m => m != equipped).ToList();
                    if (spares.Count == 0) continue;

                    ModuleData target = spares[UnityEngine.Random.Range(0, spares.Count)];
                    if (state.CanDismantleModule(target) && state.RemoveOwnedModule(target))
                    {
                        state.scrap += config.dismantleRewardTable.GetModuleScrap(target.grade);
                        stats.DismantleCount++;
                    }
                    break; // one dismantle attempt per visit is plenty
                }
            }
        }

        // ------------------------------------------------------------------
        // Combat turn loop - deliberately duplicated from
        // BalanceSimulationRunner.SimulateOneFight rather than refactored
        // into a shared method, so as not to risk changing behavior of the
        // already-validated combat-only Balance Simulator. Differs only in
        // that it operates on a PERSISTING GameState (HP/Scrap/pool carry
        // over between fights within a run) instead of resetting them.
        // ------------------------------------------------------------------
        private static bool SimulateFightToCompletion(GameState state, EnemyController enemyController, int maxTurns)
        {
            int turn = 0;
            while (turn < maxTurns)
            {
                turn++;

                int coreValue = UnityEngine.Random.Range(1, state.coreDie.faces + 1);
                enemyController.RollInhibitor();

                var hand = new List<(DieData type, int value)>();
                foreach (DieData die in state.dicePool)
                {
                    hand.Add((die, UnityEngine.Random.Range(1, die.faces + 1)));
                }

                var sortedIndices = new List<int>();
                for (int i = 0; i < hand.Count; i++) sortedIndices.Add(i);
                sortedIndices.Sort((a, b) => hand[b].value.CompareTo(hand[a].value));

                var placedValues = new Dictionary<SlotType, int>();
                var placedDieTypes = new Dictionary<SlotType, DieData>();
                var usedIndices = new HashSet<int>();
                SlotType[] slotPriority = { SlotType.Power, SlotType.Stability, SlotType.Flow, SlotType.Echo };
                for (int i = 0; i < slotPriority.Length && i < sortedIndices.Count; i++)
                {
                    int idx = sortedIndices[i];
                    placedValues[slotPriority[i]] = hand[idx].value;
                    placedDieTypes[slotPriority[i]] = hand[idx].type;
                    usedIndices.Add(idx);
                }

                var unplacedHand = new List<int>();
                for (int i = 0; i < hand.Count; i++) if (!usedIndices.Contains(i)) unplacedHand.Add(i);

                var ctx = new CombatContext
                {
                    CoreValue = coreValue,
                    CoreRange = state.coreDie.GetRange(coreValue),
                    CoreIsEven = state.coreDie.IsEven(coreValue),
                    InhibitedValue = enemyController.LastInhibitedValue,
                    PlayerHp = state.currentHp,
                    PlayerHpMax = state.maxHp,
                    FlowSlotOccupied = placedValues.ContainsKey(SlotType.Flow),
                    FullResonanceActive = false,
                };

                state.installedModules.TryGetValue(SlotType.Flow, out ModuleData flowModule);
                ModuleResult flowResult = default;
                bool flowNeedsTarget = flowModule != null && (flowModule.id == ModuleId.Mirror || flowModule.id == ModuleId.Shift || flowModule.id == ModuleId.Reverse);

                if (placedValues.ContainsKey(SlotType.Flow))
                {
                    flowResult = ModuleResolver.ResolveSlot(flowModule, placedValues[SlotType.Flow], ctx, false, forceFrequency: false);

                    if (flowNeedsTarget)
                    {
                        SlotType? targetSlot = null;
                        int lowestValue = int.MaxValue;
                        foreach (var kvp in placedValues)
                        {
                            if (kvp.Key == SlotType.Flow) continue;
                            if (kvp.Value < lowestValue) { lowestValue = kvp.Value; targetSlot = kvp.Key; }
                        }

                        if (targetSlot.HasValue)
                        {
                            ctx.TargetDieValue = placedValues[targetSlot.Value];
                            ctx.TargetDieFaces = placedDieTypes.ContainsKey(targetSlot.Value) ? placedDieTypes[targetSlot.Value].faces : 6;
                            flowResult = ModuleResolver.ResolveSlot(flowModule, placedValues[SlotType.Flow], ctx, false, forceFrequency: false);

                            if (flowModule.id == ModuleId.Reverse && flowResult.FrequencyTriggered)
                            {
                                var keys = new List<SlotType>(placedValues.Keys);
                                foreach (var slot in keys)
                                {
                                    int faces = placedDieTypes.ContainsKey(slot) ? placedDieTypes[slot].faces : 6;
                                    placedValues[slot] = faces + 1 - placedValues[slot];
                                }
                            }
                            else if (flowResult.NewTargetValue.HasValue)
                            {
                                placedValues[targetSlot.Value] = flowResult.NewTargetValue.Value;
                            }
                        }
                    }
                }

                var abilityCtx = new EnemyAbilityContext { Ctx = ctx, PlacedValues = placedValues, InstalledModules = state.installedModules, State = state, Enemy = enemyController };
                var slotsToModify = new List<SlotType>(placedValues.Keys);
                foreach (SlotType slot in slotsToModify)
                {
                    int modified = enemyController.ModifyDieValue(slot, placedValues[slot], placedDieTypes.ContainsKey(slot) ? placedDieTypes[slot] : null, abilityCtx);
                    placedValues[slot] = modified;
                }

                if (flowResult.DiceToReroll > 0)
                {
                    unplacedHand.Sort((a, b) => hand[a].value.CompareTo(hand[b].value));
                    int rerolled = 0;
                    for (int i = 0; i < unplacedHand.Count && rerolled < flowResult.DiceToReroll; i++)
                    {
                        int idx = unplacedHand[i];
                        DieData type = hand[idx].type;
                        int oldValue = hand[idx].value;
                        int newValue = UnityEngine.Random.Range(1, type.faces + 1);
                        int finalValue = flowResult.RerollKeepsBetterResult ? Mathf.Max(oldValue, newValue) : newValue;
                        hand[idx] = (type, finalValue);
                        rerolled++;
                    }
                    if (rerolled > 0)
                    {
                        int wardenDamage = enemyController.NotifyFlowRerollUsed(abilityCtx);
                        if (wardenDamage > 0) state.currentHp = Mathf.Max(0, state.currentHp - wardenDamage);
                    }
                }

                ctx.ExtraInhibitedValue = enemyController.GetExtraInhibitedValue(abilityCtx);
                ctx.PermanentlyInhibitedValues = enemyController.GetPermanentlyInhibitedValues();

                HashSet<SlotType> legameSlots = ResonanceDetector.DetectLegameSlots(placedValues);
                bool fullResonance = ResonanceDetector.DetectFullResonance(placedValues, ctx.CoreValue);
                ctx.FullResonanceActive = fullResonance;

                int effectiveThreshold = enemyController.GetEffectiveThreshold(abilityCtx);

                int powerDieBonus = state.pendingNextTurnPowerBonus;
                state.pendingNextTurnPowerBonus = 0;

                state.installedModules.TryGetValue(SlotType.Power, out ModuleData powerModule);
                int? powerDieValue = placedValues.ContainsKey(SlotType.Power) ? placedValues[SlotType.Power] : (int?)null;
                bool powerIgnoresInhibition = fullResonance || legameSlots.Contains(SlotType.Power);
                ModuleResult powerResult = ModuleResolver.ResolveSlot(powerModule, powerDieValue, ctx, powerIgnoresInhibition, forceFrequency: fullResonance);
                int powerTotal = powerResult.ValueBonus + powerDieBonus;

                bool success = fullResonance || powerTotal >= effectiveThreshold;
                int excess = Mathf.Max(0, powerTotal - effectiveThreshold);
                ctx.PointsExceedingThreshold = excess;

                state.installedModules.TryGetValue(SlotType.Stability, out ModuleData stabilityModule);
                int? stabilityDieValue = placedValues.ContainsKey(SlotType.Stability) ? placedValues[SlotType.Stability] : (int?)null;
                bool stabilityIgnoresInhibition = fullResonance || legameSlots.Contains(SlotType.Stability);
                ModuleResult stabilityResult = ModuleResolver.ResolveSlot(stabilityModule, stabilityDieValue, ctx, stabilityIgnoresInhibition, forceFrequency: fullResonance);

                state.installedModules.TryGetValue(SlotType.Echo, out ModuleData echoModule);
                int? echoDieValue = placedValues.ContainsKey(SlotType.Echo) ? placedValues[SlotType.Echo] : (int?)null;
                bool echoIgnoresInhibition = fullResonance || legameSlots.Contains(SlotType.Echo);
                ModuleResult echoResult = ModuleResolver.ResolveSlot(echoModule, echoDieValue, ctx, echoIgnoresInhibition, forceFrequency: fullResonance);

                ApplyResult(state, powerResult, fullResonance);
                ApplyResult(state, stabilityResult, fullResonance);
                ApplyResult(state, flowResult, fullResonance);
                ApplyResult(state, echoResult, fullResonance);

                if (success && stabilityResult.NextThresholdReductionPercent > 0f)
                {
                    state.pendingThresholdReductionPercent = stabilityResult.NextThresholdReductionPercent;
                }

                if (success)
                {
                    int scrapFromCombat = fullResonance ? excess * 2 : excess;
                    state.scrap += scrapFromCombat;
                    int damageToEnemy = Mathf.Max(1, excess);
                    enemyController.ApplyDamage(damageToEnemy);
                }
                else
                {
                    int rawDamage = Mathf.Max(1, effectiveThreshold - powerTotal);
                    int bonusEnemyDamage = enemyController.GetBonusDamageOnFailure(abilityCtx);
                    int reducedDamage = Mathf.Max(1, rawDamage - stabilityResult.DamageReduction) + bonusEnemyDamage;
                    state.currentHp = Mathf.Max(0, state.currentHp - reducedDamage);
                }

                enemyController.NotifyTurnEnd(abilityCtx);

                if (enemyController.IsDefeated) return true;
                if (state.IsDefeated) return false;
            }

            // Timed out - treat as a loss for run-progression purposes (shouldn't normally trigger).
            return false;
        }

        private static void ApplyResult(GameState state, ModuleResult result, bool fullResonance)
        {
            if (result.HpRecovered > 0)
            {
                state.currentHp = Mathf.Min(state.maxHp, state.currentHp + result.HpRecovered);
            }
            if (result.ScrapGained > 0)
            {
                state.scrap += fullResonance ? result.ScrapGained * 2 : result.ScrapGained;
            }
            if (result.ChargesGenerated > 0)
            {
                state.changeoverCharges += result.ChargesGenerated;
                while (state.changeoverCharges >= 10)
                {
                    state.changeoverCharges -= 10;
                    state.pendingChangeoverBonusDice.Add(state.coreDie);
                }
            }
            if (result.NextTurnPowerBonus > 0)
            {
                state.pendingNextTurnPowerBonus += result.NextTurnPowerBonus;
            }
        }
    }
}

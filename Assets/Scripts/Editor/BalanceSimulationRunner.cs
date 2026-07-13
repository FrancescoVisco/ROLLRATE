using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using Rollrate.Core;
using Rollrate.Data;
using Rollrate.Combat;

namespace Rollrate.Simulation
{
    /// <summary>
    /// Result of a single simulated fight.
    /// </summary>
    public struct FightResult
    {
        public bool PlayerWon;
        public bool TimedOut;
        public int TurnsTaken;
        public int TotalScrapGained;
        public int FullResonanceTurns;
        public int LegameResonanceTurns;
        public int HpRemaining;
    }

    /// <summary>
    /// Aggregated stats for one Core x Pool x Loadout x Enemy combination,
    /// across all simulated fights for that combination. One CSV row.
    /// </summary>
    public struct AggregatedResult
    {
        public string coreDieName;
        public string poolPresetName;
        public string loadoutName;
        public string enemyName;
        public int fightCount;
        public float winRate;
        public float avgTurnsToWin;
        public float avgTurnsToLose;
        public float fullResonanceRate; // fraction of TURNS (not fights) with Full Resonance
        public float avgScrapPerFight;
        public float timeoutRate;
    }

    /// <summary>
    /// Headless simulation engine. Reuses the exact same rule classes as the
    /// real game (ModuleResolver, ResonanceDetector, EnemyController/
    /// EnemyAbilityRegistry) so results reflect real game balance - only the
    /// player's decisions (dice placement, Mirror/Shift/Reverse target,
    /// Second Chance reroll choice) are replaced with fixed heuristics,
    /// since there's no human player driving these in a simulation.
    ///
    /// HEURISTICS USED (documented here so results can be interpreted correctly):
    /// - Placement: roll Core + all pool dice, sort descending by value,
    ///   assign the top 4 to Power/Stability/Flow/Echo in that priority
    ///   order. Leftover dice stay as "hand" for Second Chance.
    /// - Mirror/Shift/Reverse target: the OTHER currently placed die with
    ///   the LOWEST value (skip improving an already-strong die).
    /// - Second Chance reroll: the lowest-value unplaced hand dice, same
    ///   heuristic as the real game's implementation.
    ///
    /// KNOWN SIMPLIFICATIONS vs the real game:
    /// - No Shop between fights - Scrap/Charges/pending bonuses are tracked
    ///   within a single fight only, exactly like the real GameState would
    ///   before ever visiting a Merchant node.
    /// - Reinforcements' temporary dice (TempDiceToAddNextTurn) are NOT
    ///   added to the simulated pool between turns (would require deciding
    ///   how the temp die is chosen/discarded across many turns) - flagged
    ///   as a simplification, not a silent gap.
    /// </summary>
    public static class BalanceSimulationRunner
    {
        public static List<AggregatedResult> RunAll(BalanceSimulatorConfig config, Action<string> logProgress = null)
        {
            var results = new List<AggregatedResult>();

            // Simulations run millions of turns - suppress the per-turn
            // Debug.Log calls in EnemyController for the duration, since
            // their string formatting + console writes are a major cost
            // at this scale. Restored afterward for normal gameplay.
            bool previousVerboseSetting = EnemyController.VerboseLogging;
            EnemyController.VerboseLogging = false;

            try
            {

            var allDice = SimulationAutoDiscovery.FindAllOfType<DieData>(config.assetsFolderPath);
            var allModules = SimulationAutoDiscovery.FindAllOfType<ModuleData>(config.assetsFolderPath);
            var allEnemies = SimulationAutoDiscovery.FindAllOfType<EnemyData>(config.assetsFolderPath);

            var coreDice = (config.coreDiceOverride != null && config.coreDiceOverride.Length > 0)
                ? new List<DieData>(config.coreDiceOverride) : allDice;
            var enemies = (config.enemiesOverride != null && config.enemiesOverride.Length > 0)
                ? new List<EnemyData>(config.enemiesOverride) : allEnemies;

            var poolPresets = SimulationAutoDiscovery.GeneratePoolPresets(allDice, config.poolSizesToTest);
            if (config.includeMixedPools)
            {
                poolPresets.AddRange(SimulationAutoDiscovery.GenerateMixedPoolPresets(allDice, config.poolSizesToTest));
            }
            var moduleLoadouts = SimulationAutoDiscovery.GenerateModuleLoadouts(allModules, config.includeEmptySlotOption);

            int totalCombinations = coreDice.Count * poolPresets.Count * moduleLoadouts.Count * enemies.Count;
            int done = 0;
            bool cancelled = false;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            foreach (DieData coreDie in coreDice)
            {
                if (cancelled) break;
                foreach (PoolPreset pool in poolPresets)
                {
                    if (cancelled) break;
                    foreach (ModuleLoadout loadout in moduleLoadouts)
                    {
                        if (cancelled) break;
                        foreach (EnemyData enemy in enemies)
                        {
                            var agg = RunCombination(config, coreDie, pool, loadout, enemy);
                            results.Add(agg);

                            done++;

                            // Progress bar forces a redraw even during this
                            // blocking loop (unlike the log list, which only
                            // repaints once the whole run finishes), and
                            // gives an estimated time remaining based on the
                            // average time per combination so far.
                            if (done % 5 == 0 || done == totalCombinations)
                            {
                                float elapsedSeconds = (float)stopwatch.Elapsed.TotalSeconds;
                                float avgPerCombination = elapsedSeconds / done;
                                float remainingSeconds = avgPerCombination * (totalCombinations - done);

                                string title = "Balance Simulator";
                                string message = $"{done}/{totalCombinations} combinations - " +
                                                  $"elapsed {FormatTime(elapsedSeconds)} - " +
                                                  $"est. remaining {FormatTime(remainingSeconds)}";

                                if (EditorUtility.DisplayCancelableProgressBar(title, message, (float)done / totalCombinations))
                                {
                                    cancelled = true;
                                    logProgress?.Invoke($"Cancelled by user at {done}/{totalCombinations} - returning partial results.");
                                    break;
                                }
                            }

                            if (done % 50 == 0 || done == totalCombinations)
                            {
                                logProgress?.Invoke($"[{done}/{totalCombinations}] {coreDie.displayName} | {pool.presetName} | {loadout.loadoutName} | {enemy.displayName} -> win rate {agg.winRate:P0}");
                            }
                        }
                    }
                }
            }

            return results;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                EnemyController.VerboseLogging = previousVerboseSetting;
            }
        }

        private static string FormatTime(float seconds)
        {
            if (seconds < 60) return $"{seconds:F0}s";
            if (seconds < 3600) return $"{Mathf.FloorToInt(seconds / 60)}m {Mathf.FloorToInt(seconds % 60)}s";
            return $"{Mathf.FloorToInt(seconds / 3600)}h {Mathf.FloorToInt((seconds % 3600) / 60)}m";
        }

        /// <summary>
        /// Computes the total fight count a config would run, WITHOUT
        /// running anything - used by the window to warn before a huge run.
        /// </summary>
        public static (int combinations, int totalFights, int diceFound, int modulesFound, int enemiesFound, int poolPresetCount, int loadoutCount) PreviewCounts(BalanceSimulatorConfig config)
        {
            var allDice = SimulationAutoDiscovery.FindAllOfType<DieData>(config.assetsFolderPath);
            var allModules = SimulationAutoDiscovery.FindAllOfType<ModuleData>(config.assetsFolderPath);
            var allEnemies = SimulationAutoDiscovery.FindAllOfType<EnemyData>(config.assetsFolderPath);

            int coreDiceCount = (config.coreDiceOverride != null && config.coreDiceOverride.Length > 0) ? config.coreDiceOverride.Length : allDice.Count;
            int enemyCount = (config.enemiesOverride != null && config.enemiesOverride.Length > 0) ? config.enemiesOverride.Length : allEnemies.Count;

            int purePoolCount = allDice.Count * config.poolSizesToTest.Count(s => s > 0);
            int mixedPoolCount = config.includeMixedPools ? config.poolSizesToTest.Count(s => s > 0) * (allDice.Count >= 2 ? 2 : 0) : 0;
            int poolCount = purePoolCount + mixedPoolCount;

            int perSlot = config.includeEmptySlotOption ? 5 : 4; // 4 modules per slot + optional "none"
            int loadoutCount = perSlot * perSlot * perSlot * perSlot;

            int combinations = coreDiceCount * poolCount * loadoutCount * enemyCount;
            int totalFights = combinations * config.fightsPerCombination;

            return (combinations, totalFights, allDice.Count, allModules.Count, allEnemies.Count, poolCount, loadoutCount);
        }

        private static AggregatedResult RunCombination(BalanceSimulatorConfig config, DieData coreDie, PoolPreset pool, ModuleLoadout loadout, EnemyData enemy)
        {
            int wins = 0;
            int timeouts = 0;
            long turnsToWinSum = 0;
            int winCount = 0;
            long turnsToLoseSum = 0;
            int loseCount = 0;
            long fullResonanceTurnsSum = 0;
            long totalTurnsSum = 0;
            long scrapSum = 0;

            // One temporary EnemyController GameObject reused across all
            // fights in this combination (StartFight resets its state each time).
            var enemyGO = new GameObject("Sim_Enemy_Temp") { hideFlags = HideFlags.HideAndDontSave };
            var enemyController = enemyGO.AddComponent<EnemyController>();

            for (int i = 0; i < config.fightsPerCombination; i++)
            {
                FightResult result = SimulateOneFight(config, coreDie, pool, loadout, enemy, enemyController);

                if (result.PlayerWon) { wins++; turnsToWinSum += result.TurnsTaken; winCount++; }
                else if (!result.TimedOut) { turnsToLoseSum += result.TurnsTaken; loseCount++; }
                if (result.TimedOut) timeouts++;

                fullResonanceTurnsSum += result.FullResonanceTurns;
                totalTurnsSum += result.TurnsTaken;
                scrapSum += result.TotalScrapGained;
            }

            UnityEngine.Object.DestroyImmediate(enemyGO);

            int fightCount = config.fightsPerCombination;
            return new AggregatedResult
            {
                coreDieName = coreDie.displayName,
                poolPresetName = pool.presetName,
                loadoutName = loadout.loadoutName,
                enemyName = enemy.displayName,
                fightCount = fightCount,
                winRate = (float)wins / fightCount,
                avgTurnsToWin = winCount > 0 ? (float)turnsToWinSum / winCount : 0f,
                avgTurnsToLose = loseCount > 0 ? (float)turnsToLoseSum / loseCount : 0f,
                fullResonanceRate = totalTurnsSum > 0 ? (float)fullResonanceTurnsSum / totalTurnsSum : 0f,
                avgScrapPerFight = (float)scrapSum / fightCount,
                timeoutRate = (float)timeouts / fightCount,
            };
        }

        private static FightResult SimulateOneFight(BalanceSimulatorConfig config, DieData coreDie, PoolPreset pool, ModuleLoadout loadout, EnemyData enemy, EnemyController enemyController)
        {
            var state = new GameState();
            state.coreDie = coreDie;
            state.currentHp = config.startingHp;
            state.maxHp = config.startingHp;
            state.dicePool = new List<DieData>(pool.dice);
            state.installedModules = new Dictionary<SlotType, ModuleData>();
            if (loadout.power != null) state.installedModules[SlotType.Power] = loadout.power;
            if (loadout.stability != null) state.installedModules[SlotType.Stability] = loadout.stability;
            if (loadout.flow != null) state.installedModules[SlotType.Flow] = loadout.flow;
            if (loadout.echo != null) state.installedModules[SlotType.Echo] = loadout.echo;
            state.scrap = 0;
            state.changeoverCharges = 0;
            state.pendingNextTurnPowerBonus = 0;
            state.pendingThresholdReductionPercent = 0f;

            enemyController.StartFight(enemy);

            int turn = 0;
            int fullResonanceTurns = 0;
            int legameTurns = 0;
            int totalScrap = 0;

            while (turn < config.maxTurnsPerFight)
            {
                turn++;

                // --- ROLL (Core + pool) and enemy SET-UP (Inhibitor) ---
                int coreValue = UnityEngine.Random.Range(1, coreDie.faces + 1);
                enemyController.RollInhibitor();

                var hand = new List<(DieData type, int value)>();
                foreach (DieData die in state.dicePool)
                {
                    hand.Add((die, UnityEngine.Random.Range(1, die.faces + 1)));
                }

                // --- Placement heuristic: highest dice first, Power > Stability > Flow > Echo ---
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

                var unplacedHand = new List<int>(); // indices into `hand` still unplaced
                for (int i = 0; i < hand.Count; i++)
                {
                    if (!usedIndices.Contains(i)) unplacedHand.Add(i);
                }

                var ctx = new CombatContext
                {
                    CoreValue = coreValue,
                    CoreRange = coreDie.GetRange(coreValue),
                    CoreIsEven = coreDie.IsEven(coreValue),
                    InhibitedValue = enemyController.LastInhibitedValue,
                    PlayerHp = state.currentHp,
                    PlayerHpMax = state.maxHp,
                    FlowSlotOccupied = placedValues.ContainsKey(SlotType.Flow),
                    FullResonanceActive = false,
                };

                // --- Flow pre-pass: resolve Flow's module (Mirror/Shift/Reverse/Second Chance/etc.) ---
                state.installedModules.TryGetValue(SlotType.Flow, out ModuleData flowModule);
                ModuleResult flowResult = default;
                bool flowNeedsTarget = ModuleNeedsTarget(flowModule);

                if (placedValues.ContainsKey(SlotType.Flow))
                {
                    flowResult = ModuleResolver.ResolveSlot(flowModule, placedValues[SlotType.Flow], ctx, false, forceFrequency: false);

                    if (flowNeedsTarget)
                    {
                        // Heuristic: target the OTHER placed die with the lowest value.
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
                            // Recompute now that target info is known (Static/Frequency formulas read ctx.TargetDieValue).
                            flowResult = ModuleResolver.ResolveSlot(flowModule, placedValues[SlotType.Flow], ctx, false, forceFrequency: false);

                            if (flowModule.id == ModuleId.Reverse && flowResult.FrequencyTriggered)
                            {
                                // Total Inversion: flip every occupied slot's value.
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

                // --- Enemy ability die-value modifiers ---
                var abilityCtx = new EnemyAbilityContext { Ctx = ctx, PlacedValues = placedValues, InstalledModules = state.installedModules, State = state, Enemy = enemyController };
                var slotsToModify = new List<SlotType>(placedValues.Keys);
                foreach (SlotType slot in slotsToModify)
                {
                    int modified = enemyController.ModifyDieValue(slot, placedValues[slot], placedDieTypes.ContainsKey(slot) ? placedDieTypes[slot] : null, abilityCtx);
                    placedValues[slot] = modified;
                }

                // --- Second Chance reroll heuristic: lowest-value unplaced hand dice first ---
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

                // --- Extra/permanent Inhibited values (Sentinel/Judge) ---
                ctx.ExtraInhibitedValue = enemyController.GetExtraInhibitedValue(abilityCtx);
                ctx.PermanentlyInhibitedValues = enemyController.GetPermanentlyInhibitedValues();

                // --- Resonance detection (uses final, post-manipulation values) ---
                HashSet<SlotType> legameSlots = ResonanceDetector.DetectLegameSlots(placedValues);
                bool fullResonance = ResonanceDetector.DetectFullResonance(placedValues, ctx.CoreValue);
                ctx.FullResonanceActive = fullResonance;
                if (fullResonance) fullResonanceTurns++;
                else if (legameSlots.Count > 0) legameTurns++;

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

                ApplyResult(state, powerResult, fullResonance, config, ref totalScrap);
                ApplyResult(state, stabilityResult, fullResonance, config, ref totalScrap);
                ApplyResult(state, flowResult, fullResonance, config, ref totalScrap);
                ApplyResult(state, echoResult, fullResonance, config, ref totalScrap);

                if (success && stabilityResult.NextThresholdReductionPercent > 0f)
                {
                    state.pendingThresholdReductionPercent = stabilityResult.NextThresholdReductionPercent;
                }

                if (success)
                {
                    int scrapFromCombat = fullResonance ? excess * 2 : excess;
                    state.scrap += scrapFromCombat;
                    totalScrap += scrapFromCombat;

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

                if (enemyController.IsDefeated)
                {
                    return new FightResult
                    {
                        PlayerWon = true,
                        TimedOut = false,
                        TurnsTaken = turn,
                        TotalScrapGained = totalScrap,
                        FullResonanceTurns = fullResonanceTurns,
                        LegameResonanceTurns = legameTurns,
                        HpRemaining = state.currentHp,
                    };
                }

                if (state.IsDefeated)
                {
                    return new FightResult
                    {
                        PlayerWon = false,
                        TimedOut = false,
                        TurnsTaken = turn,
                        TotalScrapGained = totalScrap,
                        FullResonanceTurns = fullResonanceTurns,
                        LegameResonanceTurns = legameTurns,
                        HpRemaining = 0,
                    };
                }
            }

            // Ran out of turns without a winner - balance issue signal, not a clean result.
            return new FightResult
            {
                PlayerWon = false,
                TimedOut = true,
                TurnsTaken = turn,
                TotalScrapGained = totalScrap,
                FullResonanceTurns = fullResonanceTurns,
                LegameResonanceTurns = legameTurns,
                HpRemaining = state.currentHp,
            };
        }

        private static bool ModuleNeedsTarget(ModuleData module)
        {
            if (module == null) return false;
            return module.id == ModuleId.Mirror || module.id == ModuleId.Shift || module.id == ModuleId.Reverse;
        }

        private static void ApplyResult(GameState state, ModuleResult result, bool fullResonance, BalanceSimulatorConfig config, ref int totalScrap)
        {
            if (result.HpRecovered > 0)
            {
                state.currentHp = Mathf.Min(state.maxHp, state.currentHp + result.HpRecovered);
            }

            if (result.ScrapGained > 0)
            {
                int scrapToAdd = fullResonance ? result.ScrapGained * 2 : result.ScrapGained;
                state.scrap += scrapToAdd;
                totalScrap += scrapToAdd;
            }

            if (result.ChargesGenerated > 0)
            {
                state.changeoverCharges += result.ChargesGenerated;
                while (state.changeoverCharges >= 10)
                {
                    state.changeoverCharges -= 10;
                    // Simplification: always add a D4-equivalent by reusing the
                    // smallest die already in the pool if available, otherwise
                    // skip. Real game uses a fixed configurable reward die -
                    // for simulation purposes the exact type matters less than
                    // "the pool grows by one die", so this is an acceptable stand-in.
                    if (state.dicePool.Count > 0) state.dicePool.Add(state.dicePool[0]);
                }
            }

            if (result.NextTurnPowerBonus > 0)
            {
                state.pendingNextTurnPowerBonus += result.NextTurnPowerBonus;
            }
        }

        /// <summary>Writes the aggregated results to a CSV file at the given path.</summary>
        public static void WriteCsv(List<AggregatedResult> results, string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("CoreDie,PoolPreset,ModuleLoadout,Enemy,Fights,WinRate,AvgTurnsToWin,AvgTurnsToLose,FullResonanceRate,AvgScrapPerFight,TimeoutRate");

            foreach (var r in results)
            {
                sb.AppendLine(string.Join(",",
                    Escape(r.coreDieName), Escape(r.poolPresetName), Escape(r.loadoutName), Escape(r.enemyName),
                    r.fightCount.ToString(),
                    r.winRate.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
                    r.avgTurnsToWin.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    r.avgTurnsToLose.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    r.fullResonanceRate.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
                    r.avgScrapPerFight.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    r.timeoutRate.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)));
            }

            File.WriteAllText(path, sb.ToString());
        }

        private static string Escape(string s) => s != null && s.Contains(",") ? $"\"{s}\"" : (s ?? "");
    }
}

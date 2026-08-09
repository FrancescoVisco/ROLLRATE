using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rollrate.Core;
using Rollrate.Data;
using Rollrate.Combat;
using Rollrate.Map;

namespace Rollrate.Simulation
{
    /// <summary>
    /// Plays the ENTIRE game automatically - Map traversal, Combat, Shop,
    /// Furnace, Archive, Rest, Meta, Fragmentation, Guardian victories -
    /// for however many campaigns requested, using the REAL resolution
    /// classes directly (GameState, TurnContext, TurnController,
    /// MapGenerator are all plain C#, no scenes/MonoBehaviours needed).
    ///
    /// SIMPLIFICATIONS (documented, not silent):
    /// - Combat uses only each enemy's base Threshold/Attack + Inhibitor
    ///   Parity - the 15 unique Abilities (EnemyAbilityResolver) are NOT
    ///   replicated here, since that resolver's methods take a concrete
    ///   EnemyController (a MonoBehaviour), which the simulator
    ///   deliberately avoids for speed running hundreds of campaigns.
    ///   Real per-enemy difficulty will run somewhat harder than these
    ///   numbers suggest.
    /// - Map path choice is uniformly RANDOM among available connections
    ///   at every fork - this gives an unbiased statistical sample of
    ///   the generated content, not an "optimal player" simulation.
    /// - Archive's reward is always Scrap (simplest to resolve without
    ///   chaining into a separate Shop/Furnace visit mid-Test).
    /// - Shop AI: buys every affordable die offer, then Max HP once if
    ///   still affordable, then leaves - no Reroll spending.
    /// - Furnace AI: fuses the first same-Type pair found (same Grade
    ///   preferred, to upgrade rather than just consolidate Effects), if
    ///   affordable.
    /// - Reroll AI (Combat): marks every non-Flow die below x1.2 net
    ///   multiplier for reroll, spends all available rerolls, then stops.
    /// - Echo AI: always targets the single highest-value Power die.
    /// </summary>
    public static class RunSimulator
    {
        public static RunSimStats RunCampaigns(RunSimConfig config, int campaignCount)
        {
            var stats = new RunSimStats();
            stats.TotalCampaigns = campaignCount;

            for (int i = 0; i < campaignCount; i++)
            {
                SimulateCampaign(config, stats);
            }

            return stats;
        }

        private static void SimulateCampaign(RunSimConfig config, RunSimStats stats)
        {
            var state = new GameState();
            state.ResetForNewRun(config.startingCoreDie, config.startingHp);

            if (config.startingPool != null)
            {
                foreach (var entry in config.startingPool)
                {
                    if (entry?.data == null) continue;
                    state.AddDieToPool(new DieInstance(entry.data, entry.type), fromRunUnlock: false);
                }
            }

            int runsThisCampaign = 0;

            while (true)
            {
                runsThisCampaign++;
                if (runsThisCampaign > config.maxRunsPerCampaign)
                {
                    stats.AbandonedCampaigns++;
                    return;
                }

                bool victory = SimulateOneRun(config, state, stats);

                stats.ScrapAtRunEnd.Add(state.scrap);

                if (victory)
                {
                    stats.Victories++;
                    stats.RunsPerCampaign.Add(runsThisCampaign);
                    stats.RecordCoreGradeAtCampaignEnd(state.coreDie != null ? state.coreDie.displayName : "?");
                    return;
                }

                // Defeat -> Meta (up to 3 random unlocked dice, pick 1 at random) -> Fragmentation -> continue
                DieInstance chosen = null;
                if (state.unlockedThisRun.Count > 0)
                {
                    var candidates = state.unlockedThisRun.OrderBy(_ => Random.value).Take(3).ToList();
                    chosen = candidates[Random.Range(0, candidates.Count)];
                    stats.RecordMetaUnlock($"{chosen.type} D{(chosen.data != null ? chosen.data.faces : 0)}");
                }

                state.ApplyFragmentation(config.startingHp);
                if (chosen != null) state.AddDieToPool(chosen, fromRunUnlock: false);
            }
        }

        /// <summary>One run: from Grade I Page 1 until either Defeat (returns false) or the Sovereign is defeated (returns true).</summary>
        private static bool SimulateOneRun(RunSimConfig config, GameState state, RunSimStats stats)
        {
            int nodesResolved = 0;

            while (state.currentEchelon <= 5)
            {
                var page = MapGenerator.GeneratePage(state.currentPage, state.currentEchelon);
                MapNode currentNode = page.EntryColumn[0];

                for (int col = 1; col < page.columns.Count; col++)
                {
                    var options = currentNode.connectionsToNextColumn;
                    int nextRow = options.Count > 0 ? options[Random.Range(0, options.Count)] : 0;
                    currentNode = page.columns[col][nextRow];

                    NodeType resolvedType = currentNode.type == NodeType.Glitch ? RollGlitchOutcome() : currentNode.type;

                    // Singolarita' (design doc Section 7, Grade V): 33% ambush before any non-Conflict/Terminal node.
                    if (state.currentEchelon == 5 && resolvedType != NodeType.Conflict && resolvedType != NodeType.Terminal && Random.value < 0.33f)
                    {
                        var ambushTier = Random.value < 0.5f ? EnemyTier.Base : EnemyTier.Elite;
                        var ambushEnemy = config.enemyRegistry != null ? config.enemyRegistry.GetRandom(state.currentEchelon, ambushTier) : null;
                        if (ambushEnemy != null && !ResolveCombatNode(config, state, stats, ambushEnemy))
                        {
                            stats.NodesResolvedBeforeDeath.Add(nodesResolved);
                            return false;
                        }
                    }

                    switch (resolvedType)
                    {
                        case NodeType.Conflict:
                        case NodeType.Overload:
                        case NodeType.Terminal:
                        {
                            var tier = resolvedType == NodeType.Terminal ? EnemyTier.Guardian
                                     : resolvedType == NodeType.Overload ? EnemyTier.Elite
                                     : EnemyTier.Base;
                            var enemy = config.enemyRegistry != null ? config.enemyRegistry.GetRandom(state.currentEchelon, tier) : null;
                            if (enemy == null) break; // registry not configured for this Grade/Tier - skip silently

                            if (!ResolveCombatNode(config, state, stats, enemy))
                            {
                                stats.NodesResolvedBeforeDeath.Add(nodesResolved);
                                return false;
                            }

                            if (tier == EnemyTier.Guardian) ApplyGuardianVictory(config, state, enemy);
                            break;
                        }

                        case NodeType.Merchant:
                            SimulateShopVisit(config, state, stats);
                            break;

                        case NodeType.Furnace:
                            SimulateFurnaceVisit(config, state, stats);
                            break;

                        case NodeType.Archive:
                            SimulateArchiveVisit(config, state, stats);
                            if (state.currentHp <= 0)
                            {
                                stats.DeathsFromAmbizione++;
                                stats.RecordDeathByGrade(state.currentEchelon);
                                stats.NodesResolvedBeforeDeath.Add(nodesResolved);
                                return false;
                            }
                            break;

                        case NodeType.Bonfire:
                            int missing = state.maxHp - state.currentHp;
                            state.currentHp = Mathf.Min(state.maxHp, state.currentHp + Mathf.CeilToInt(missing / 2f));
                            break;
                    }

                    nodesResolved++;
                }

                // Terminal (Guardian) already advanced Grade/Page (or set the victory sentinel) inside
                // ApplyGuardianVictory above - only advance the Page ourselves for a normal P1/P2 exit.
                if (currentNode.type != NodeType.Terminal && state.currentPage < 3)
                {
                    state.currentPage++;
                }
            }

            return true; // currentEchelon > 5 - Sovereign defeated
        }

        private static NodeType RollGlitchOutcome()
        {
            NodeType[] outcomes = { NodeType.Conflict, NodeType.Merchant, NodeType.Archive, NodeType.Overload, NodeType.Bonfire, NodeType.Furnace };
            return outcomes[Random.Range(0, outcomes.Length)];
        }

        /// <summary>Mirrors RunManager.ApplyGuardianVictory exactly (Core evolution, Tassa di Sfarzo, Grade advance / victory sentinel).</summary>
        private static readonly float[] AscensionTaxByGrade = { 0f, 0.10f, 0.15f, 0.20f, 0.25f };

        private static void ApplyGuardianVictory(RunSimConfig config, GameState state, EnemyData guardian)
        {
            if (guardian.coreEvolutionOnDefeat != null) state.coreDie = guardian.coreEvolutionOnDefeat;

            int currentGrade = Mathf.Clamp(state.currentEchelon, 1, 5);
            if (currentGrade < 5)
            {
                int tax = Mathf.RoundToInt(state.scrap * AscensionTaxByGrade[currentGrade]);
                state.scrap = Mathf.Max(0, state.scrap - tax);
                state.currentEchelon = currentGrade + 1;
                // currentPage deliberately NOT touched here - matches RunManager's own comment: the
                // outer loop advances it explicitly for non-Terminal exits, and a fresh Grade always
                // starts back at Page 1 via the generic reset below.
                state.currentPage = 1;
            }
            else
            {
                state.currentEchelon = 6; // sentinel: run complete
            }
        }

        /// <summary>Runs one full fight to conclusion. Returns true if the player won, false if the player died.</summary>
        private static bool ResolveCombatNode(RunSimConfig config, GameState state, RunSimStats stats, EnemyData enemy)
        {
            state.ResetForNewFight();
            var turn = new TurnController();

            int enemyHp = enemy.maxHp;
            int pendingAttackReduction = 0;
            int turnCount = 0;

            while (true)
            {
                turnCount++;
                if (turnCount > config.maxTurnsPerFight)
                {
                    return false; // stalemate - treat as a loss (a balance red flag worth noticing in the results)
                }

                int inhibitedValue = enemy.inhibitorDie != null ? Random.Range(1, enemy.inhibitorDie.faces + 1) : 0;
                bool inhibitorBoostsAttack = inhibitedValue % 2 != 0;

                var ctx = turn.RollAll(state, config.handSize, inhibitedValue);

                // Reroll AI: mark every non-Flow die under a modest Vibrazione multiplier, spend all rerolls available.
                while (turn.RerollsRemaining > 0)
                {
                    bool markedAny = false;
                    foreach (var d in ctx.dice)
                    {
                        if (d.instance == null || d.instance.type == DieType.Flow) continue;
                        if (d.markedForReroll) continue;
                        if (ctx.GetNetMultiplier(d) < 1.2f)
                        {
                            turn.ToggleRerollMark(d);
                            markedAny = true;
                        }
                    }
                    if (!markedAny) break;
                    var rerollResult = turn.Reroll();
                    if (rerollResult.scrapGained > 0) state.scrap += rerollResult.scrapGained;
                }

                // Echo AI: always boost the single highest-value Power die.
                var powerDice = ctx.GetOfType(DieType.Power);
                if (powerDice.Count > 0)
                {
                    var bestPower = powerDice.OrderByDescending(d => ctx.GetEffectiveValue(d)).First();
                    foreach (var echoDie in ctx.GetOfType(DieType.Echo))
                    {
                        turn.AssignEchoTarget(echoDie, bestPower);
                    }
                }

                stats.TotalTurnsAllFights++;
                if (ctx.dice.Exists(d => ctx.GetNetMultiplier(d) > 1f)) stats.TurnsWithVibrationBonusAllFights++;

                int thresholdThisTurn = enemy.baseThreshold + (!inhibitorBoostsAttack ? 2 : 0); // Parity (design doc Section 4) - unique Abilities not simulated, see class summary
                int attackThisTurn = Mathf.Max(0, enemy.baseAttack + (inhibitorBoostsAttack ? 2 : 0) - pendingAttackReduction);
                pendingAttackReduction = 0;

                var result = turn.ResolveCheck(thresholdThisTurn, attackThisTurn);

                if (result.attackSucceeded)
                {
                    enemyHp = Mathf.Max(0, enemyHp - result.excess);
                    state.scrap += result.excess; // design doc Section 6, base Scrap-from-combat rule
                }
                if (result.bonusDamageToEnemy > 0) enemyHp = Mathf.Max(0, enemyHp - result.bonusDamageToEnemy);
                if (!result.defenseHeld) state.currentHp = Mathf.Max(0, state.currentHp - result.damageTaken);
                if (result.scrapGained > 0) state.scrap += result.scrapGained;
                if (result.hpHealed > 0) state.currentHp = Mathf.Min(state.maxHp, state.currentHp + result.hpHealed);
                if (result.enemyAttackReductionNextTurn > 0) pendingAttackReduction += result.enemyAttackReductionNextTurn;

                turn.StoreReverbPending(state);

                if (enemyHp <= 0)
                {
                    stats.TotalTurnsInWonFights += turnCount;
                    stats.WonFightsCount++;
                    return true;
                }
                if (state.currentHp <= 0)
                {
                    stats.DeathsFromCombat++;
                    stats.RecordDeathByGrade(state.currentEchelon);
                    stats.RecordDeathByEnemy(enemy.displayName);
                    return false;
                }
            }
        }

        /// <summary>AI: buy every affordable die offer, then Max HP once if still affordable, then leave.</summary>
        private static void SimulateShopVisit(RunSimConfig config, GameState state, RunSimStats stats)
        {
            if (config.shopCostTable == null) return;
            int gradeIndex = Mathf.Clamp(state.currentEchelon - 1, 0, 4);
            int pageIndex = Mathf.Clamp(state.currentPage - 1, 0, 2);
            var sizeOptions = config.dieSizeByGrade != null && gradeIndex < config.dieSizeByGrade.Length ? config.dieSizeByGrade[gradeIndex]?.options : null;
            if (sizeOptions == null || sizeOptions.Length == 0) return;

            int[,] maxEffectsByGradeAndPage =
            {
                { 0, 0, 1 }, { 1, 1, 2 }, { 2, 2, 2 }, { 2, 2, 3 }, { 3, 4, 4 }
            };
            int maxEffects = maxEffectsByGradeAndPage[gradeIndex, pageIndex];

            int dieCost = config.shopCostTable.GetNewDieCost(state.currentEchelon);
            for (int i = 0; i < config.shopOfferCount; i++)
            {
                if (state.scrap < dieCost) break;

                DieData size = sizeOptions[Random.Range(0, sizeOptions.Length)];
                var types = new[] { DieType.Power, DieType.Stability, DieType.Flow, DieType.Echo };
                DieType type = types[Random.Range(0, types.Length)];
                var offer = new DieInstance(size, type);

                int actualEffectCount = Random.Range(0, maxEffects + 1); // same rule as ShopController: base (0) is always a possible roll
                if (actualEffectCount > 0 && config.effectRegistry != null)
                {
                    foreach (var e in config.effectRegistry.GetRandomUnlocked(type, state.currentEchelon, actualEffectCount)) offer.AddEffect(e);
                }

                state.scrap -= dieCost;
                state.AddDieToPool(offer, fromRunUnlock: true);
                stats.RecordDicePurchase($"{type} D{size.faces}");
            }

            int hpCost = config.shopCostTable.GetIncreaseMaxHpCost(state.currentEchelon);
            if (state.scrap >= hpCost)
            {
                state.scrap -= hpCost;
                float ratio = state.maxHp > 0 ? (float)state.currentHp / state.maxHp : 1f;
                state.maxHp += 1;
                state.currentHp = Mathf.FloorToInt(ratio * state.maxHp);
                stats.ShopMaxHpPurchases++;
            }
        }

        /// <summary>AI: fuse the first same-Type pair found (same Grade preferred), if affordable.</summary>
        private static void SimulateFurnaceVisit(RunSimConfig config, GameState state, RunSimStats stats)
        {
            int gradeIndex = Mathf.Clamp(state.currentEchelon - 1, 0, config.fusionCostByGrade.Length - 1);
            int cost = config.fusionCostByGrade[gradeIndex];
            if (state.scrap < cost) return;

            DieInstance a = null, b = null;
            var byType = state.dicePool.GroupBy(d => d.type);
            foreach (var group in byType)
            {
                var list = group.ToList();
                if (list.Count < 2) continue;
                var sameGradePair = list
                    .GroupBy(d => d.data != null ? d.data.grade : 0)
                    .FirstOrDefault(g => g.Count() >= 2);
                if (sameGradePair != null)
                {
                    a = sameGradePair.ElementAt(0);
                    b = sameGradePair.ElementAt(1);
                }
                else
                {
                    a = list[0];
                    b = list[1];
                }
                break;
            }
            if (a == null || b == null) return;

            state.scrap -= cost;

            bool sameGrade = a.data != null && b.data != null && a.data.grade == b.data.grade;
            DieData resultData = sameGrade ? AdvanceOneGrade(a.data) : (a.data.grade <= b.data.grade ? a.data : b.data);

            var fused = new DieInstance(resultData, a.type);
            foreach (var e in a.effects) fused.AddEffect(e);
            foreach (var e in b.effects) fused.AddEffect(e);
            if (fused.effects.Count > 4) fused.effects.RemoveRange(4, fused.effects.Count - 4);

            state.RemoveDiePermanently(a);
            state.RemoveDiePermanently(b);
            state.AddDieToPool(fused, fromRunUnlock: true);
            stats.FurnaceFusions++;
        }

        private static DieData AdvanceOneGrade(DieData from)
        {
            if (from == null) return null;
            int targetGrade = from.grade + 1;
            DieData current = from;
            int safety = 0;
            while (current.nextTier != null && current.grade < targetGrade && safety < 10)
            {
                current = current.nextTier;
                safety++;
            }
            return current;
        }

        /// <summary>AI: always takes the Scrap reward (simplification, see class summary).</summary>
        private static void SimulateArchiveVisit(RunSimConfig config, GameState state, RunSimStats stats)
        {
            if (config.archiveTestTable == null) return;
            int grade = state.currentEchelon;
            int testRoll = Random.Range(0, 3);
            bool success;

            switch (testRoll)
            {
                case 0: // Resonance
                {
                    int roll = state.coreDie != null ? Random.Range(1, state.coreDie.faces + 1) : 0;
                    success = roll >= config.archiveTestTable.GetResonanceThreshold(grade);
                    stats.ArchiveResonanceTotal++;
                    if (success) stats.ArchiveResonanceWins++;
                    else state.scrap = Mathf.Max(0, state.scrap - config.archiveTestTable.GetResonancePenalty(grade));
                    break;
                }
                case 1: // Tribute
                {
                    int sum = state.dicePool.Sum(d => d.data != null ? Random.Range(1, d.data.faces + 1) : 0);
                    success = sum >= config.archiveTestTable.GetTributeThreshold(grade);
                    stats.ArchiveTributeTotal++;
                    if (success) stats.ArchiveTributeWins++;
                    else if (state.CanRemoveDie() && state.dicePool.Count > 0)
                    {
                        state.RemoveDiePermanently(state.dicePool[Random.Range(0, state.dicePool.Count)]);
                    }
                    break;
                }
                default: // Ambition
                {
                    int coreRoll = state.coreDie != null ? Random.Range(1, state.coreDie.faces + 1) : 0;
                    int bestPoolRoll = 0;
                    foreach (var d in state.dicePool)
                    {
                        if (d.data == null) continue;
                        int r = Random.Range(1, d.data.faces + 1);
                        if (r > bestPoolRoll) bestPoolRoll = r;
                    }
                    success = (coreRoll + bestPoolRoll) >= config.archiveTestTable.GetAmbitionThreshold(grade);
                    stats.ArchiveAmbitionTotal++;
                    if (success) stats.ArchiveAmbitionWins++;
                    else state.currentHp = Mathf.Max(0, state.currentHp - Mathf.CeilToInt(state.maxHp * 0.2f));
                    break;
                }
            }

            if (success) state.scrap += config.archiveTestTable.GetRewardScrap(grade);
        }
    }
}

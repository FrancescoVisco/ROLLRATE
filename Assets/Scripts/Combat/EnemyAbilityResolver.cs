using System.Linq;
using UnityEngine;
using Rollrate.Data;
using Rollrate.Core;

namespace Rollrate.Combat
{
    /// <summary>
    /// All 15 enemy Abilities (design doc Section 8) in one place, keyed
    /// by EnemyAbilityId. Deliberately centralized in a single resolver
    /// (not 15 separate classes) so the whole set can be reviewed and
    /// verified together instead of chasing bugs across scattered files.
    ///
    /// Called from three points in the turn (see RollKeepUIController):
    /// OnTurnStart (right after the Inhibitor rolls, before the player
    /// rolls), OnPlayerReroll (every time Reroll() actually rerolls
    /// something), and ApplyPreCheckModifiers + OnTurnEnd (around
    /// ResolveCheck).
    ///
    /// SIMPLIFICATION NOTE: Feedback/Refraction/Glitch are written to
    /// affect Power (Attack) and Stability (Defense) dice specifically -
    /// their interaction with Flow's reroll-granting or Echo's transfer
    /// role (if the affected Type happens to be Flow/Echo) is not
    /// specifically modeled; Refraction targeting Flow/Echo currently has
    /// no mechanical effect. Flagged here rather than silently ignored.
    /// </summary>
    public static class EnemyAbilityResolver
    {
        /// <summary>Called once per turn, right after the Inhibitor rolls, before the player rolls.</summary>
        public static void OnTurnStart(EnemyController enemy)
        {
            switch (enemy.AbilityId)
            {
                case EnemyAbilityId.Clockwork: // Gatekeeper - permanent, forever
                    enemy.AddPermanentThresholdBonus(2);
                    break;

                case EnemyAbilityId.Sentence: // Judge - chosen once, kept all fight
                    if (!enemy.PermanentExtraInhibitedValue.HasValue)
                    {
                        int faces = enemy.InhibitorDieType != null ? enemy.InhibitorDieType.faces : 8;
                        enemy.SetPermanentExtraInhibitedValueOnce(Random.Range(1, faces + 1));
                    }
                    break;

                case EnemyAbilityId.Refraction: // Prism - re-rolled every turn
                    enemy.RollRefractionTargetType();
                    break;
            }
        }

        /// <summary>Called every time TurnController.Reroll() actually rerolls at least one die.</summary>
        public static void OnPlayerReroll(EnemyController enemy, GameState playerState)
        {
            switch (enemy.AbilityId)
            {
                case EnemyAbilityId.Backlash: // Eraser - this turn only
                    enemy.AddRerollThresholdBonusThisTurn(2);
                    break;

                case EnemyAbilityId.Stasis: // Warden - direct HP damage to the player
                    if (playerState != null) playerState.currentHp = Mathf.Max(0, playerState.currentHp - 1);
                    break;
            }
        }

        /// <summary>
        /// Called once per turn, right before ResolveCheck. Applies
        /// Static/Lockdown/Jammer/Tax's Threshold/Inhibition changes
        /// directly onto EnemyController/TurnContext, and returns
        /// (attackAdjustment, defenseAdjustment, extraThreshold) for the
        /// rest (Feedback/Refraction/Glitch/Void), which the caller adds
        /// to the base Attack/Defense/Threshold before calling ResolveCheck.
        /// </summary>
        public static (float attackAdjustment, float defenseAdjustment, int extraThreshold) ApplyPreCheckModifiers(
            EnemyController enemy, TurnContext ctx, GameState playerState)
        {
            float attackAdj = 0f;
            float defenseAdj = 0f;
            int extraThreshold = 0;

            switch (enemy.AbilityId)
            {
                case EnemyAbilityId.Static: // Fragment
                    if (enemy.LastInhibitedValue == 1 || enemy.LastInhibitedValue == 2) extraThreshold += 2;
                    break;

                case EnemyAbilityId.Lockdown: // Compiler
                    extraThreshold += ctx.GetOfType(DieType.Flow).Count;
                    break;

                case EnemyAbilityId.Jammer: // Sentinel
                    if (ctx.coreIsEven && !ctx.extraInhibitedValues.Contains(1)) ctx.extraInhibitedValues.Add(1);
                    break;

                case EnemyAbilityId.Tax: // Inquisitor - reads the WHOLE pool, not just the Hand
                    if (playerState != null)
                    {
                        int belowD12 = playerState.dicePool.Count(d => d.data != null && d.data.faces < 12);
                        extraThreshold += belowD12 * 3;
                    }
                    break;

                case EnemyAbilityId.Feedback: // Architect - -2 per Power die held
                    attackAdj -= 2f * ctx.GetOfType(DieType.Power).Count;
                    break;

                case EnemyAbilityId.Refraction: // Prism - halves the chosen Type's contribution (Power/Stability only, see class summary)
                    var targetType = enemy.RefractionTargetTypeThisTurn;
                    if (targetType == DieType.Power)
                        attackAdj -= ctx.GetOfType(DieType.Power).Sum(d => ctx.GetEffectiveValue(d)) * 0.5f;
                    else if (targetType == DieType.Stability)
                        defenseAdj -= ctx.GetOfType(DieType.Stability).Sum(d => ctx.GetEffectiveValue(d)) * 0.5f;
                    break;

                case EnemyAbilityId.Glitch: // Null-Pointer - max-value dice regress to 1 (Power/Stability only, see class summary)
                    foreach (var d in ctx.GetOfType(DieType.Power))
                    {
                        if (d.instance?.data == null || d.rolledValue < d.instance.data.faces) continue;
                        float normal = ctx.GetEffectiveValue(d);
                        float regressed = 1f * ctx.GetNetMultiplier(d);
                        attackAdj -= (normal - regressed);
                    }
                    foreach (var d in ctx.GetOfType(DieType.Stability))
                    {
                        if (d.instance?.data == null || d.rolledValue < d.instance.data.faces) continue;
                        float normal = ctx.GetEffectiveValue(d);
                        float regressed = 1f * ctx.GetNetMultiplier(d);
                        defenseAdj -= (normal - regressed);
                    }
                    break;

                case EnemyAbilityId.Void: // Avatar - redirects Echo dice to the enemy's Threshold instead of their assigned target
                    foreach (var d in ctx.GetOfType(DieType.Echo))
                    {
                        if (d.echoTarget == null && !d.echoTargetType.HasValue) continue;
                        float echoValue = d.rolledValue * ctx.GetNetMultiplier(d);
                        if (ctx.HasEffect(d, EffectId.Amplify)) echoValue *= 2f;
                        extraThreshold += Mathf.RoundToInt(echoValue / 2f);
                        d.echoTarget = null;
                        d.echoTargetType = null;
                    }
                    break;
            }

            return (attackAdj, defenseAdj, extraThreshold);
        }

        /// <summary>Called once per turn, right after ResolveCheck resolves. Queues next-turn bonuses (Pressure/Discord) and disables dice for the fight (Delete).</summary>
        public static void OnTurnEnd(EnemyController enemy, TurnContext ctx, GameState playerState, bool attackSucceeded)
        {
            switch (enemy.AbilityId)
            {
                case EnemyAbilityId.Pressure: // Tracer
                    if (!attackSucceeded) enemy.QueueNextTurnAttackBonus(3);
                    break;

                case EnemyAbilityId.Discord: // Cantor
                    int qualifying = ctx.dice.Count(d => ctx.GetNetMultiplier(d) >= 2f);
                    if (qualifying > 0) enemy.QueueNextTurnThresholdBonus(qualifying * 2);
                    break;

                case EnemyAbilityId.Delete: // Sovereign
                    if (playerState != null)
                    {
                        var toDisable = ctx.dice.Where(d => d.rolledValue == ctx.coreValue && d.instance != null).ToList();
                        foreach (var d in toDisable)
                        {
                            playerState.disabledThisFight.Add(d.instance);
                            Debug.Log($"[EnemyAbilityResolver] Delete: disabled a die showing {ctx.coreValue} for the rest of the fight.");
                        }
                    }
                    break;
            }
        }
    }
}

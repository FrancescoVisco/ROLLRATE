using UnityEngine;

namespace Rollrate.Combat.Enemies
{
    /// <summary>
    /// Judge (Grade IV Guardian) - Sentence: at the start of every turn, a
    /// new numeric value becomes permanently inhibited for the rest of the
    /// fight, in addition to the normal Inhibitor roll.
    /// </summary>
    public class JudgeAbility : EnemyAbilityBase
    {
        public override void OnTurnStart(EnemyController enemy)
        {
            int faces = enemy.InhibitorDieType != null ? enemy.InhibitorDieType.faces : 8;
            var alreadyInhibited = enemy.GetPermanentlyInhibitedValues();

            int candidate;
            int attempts = 0;
            do
            {
                candidate = Random.Range(1, faces + 1);
                attempts++;
            } while (alreadyInhibited.Contains(candidate) && attempts < 20);

            enemy.AddPermanentInhibitedValue(candidate);
            Debug.Log($"[JudgeAbility] Sentence: value {candidate} is now permanently inhibited for the rest of the fight.");
        }

        public override int ApplyThresholdModifier(int baseThreshold, EnemyAbilityContext ctx) => baseThreshold;
    }
}

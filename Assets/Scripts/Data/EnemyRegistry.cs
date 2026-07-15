using System.Collections.Generic;
using UnityEngine;

namespace Rollrate.Data
{
    /// <summary>
    /// Flat list of every enemy in the game. The Map picks a random match
    /// by Grade + Tier at runtime (Conflict node -> Base, Overload node ->
    /// Elite, Terminal node -> Guardian) - no per-Grade authoring needed
    /// beyond setting each EnemyData's own Grade/Tier fields.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyRegistry", menuName = "Rollrate/Enemy Registry")]
    public class EnemyRegistry : ScriptableObject
    {
        public EnemyData[] allEnemies;

        public EnemyData GetRandom(int grade, EnemyTier tier)
        {
            var matches = new List<EnemyData>();
            foreach (EnemyData e in allEnemies)
            {
                if (e != null && e.grade == grade && e.tier == tier) matches.Add(e);
            }

            if (matches.Count == 0)
            {
                Debug.LogWarning($"[EnemyRegistry] No enemy found for Grade {grade}, Tier {tier}.");
                return null;
            }

            return matches[Random.Range(0, matches.Count)];
        }
    }
}

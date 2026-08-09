using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rollrate.Data
{
    /// <summary>
    /// Flat list of all 16 Effects. The Shop picks random UNLOCKED ones
    /// (grade <= currentEchelon, design doc Section 5 "Grado degli
    /// Effetti" - cumulative, everything at or below the current Grade
    /// stays available forever) matching a die's Type, at Purchase time.
    /// </summary>
    [CreateAssetMenu(fileName = "EffectRegistry", menuName = "Rollrate/Effect Registry")]
    public class EffectRegistry : ScriptableObject
    {
        public EffectData[] allEffects;

        /// <summary>
        /// Picks up to `count` DISTINCT random unlocked Effects matching
        /// the given Type - fewer than `count` if not enough are unlocked
        /// yet. Never returns duplicates on the same die.
        /// </summary>
        public List<EffectData> GetRandomUnlocked(DieType type, int currentEchelon, int count)
        {
            var eligible = allEffects
                .Where(e => e != null && e.dieType == type && e.grade <= currentEchelon)
                .OrderBy(_ => Random.value)
                .Take(count)
                .ToList();
            return eligible;
        }
    }
}

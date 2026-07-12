using System.Collections.Generic;
using System.Linq;
using Rollrate.Data;

namespace Rollrate.Combat
{
    /// <summary>
    /// Detects the two Resonance states described in the design:
    ///
    /// - Legame: 2+ placed dice share the same value. Modules involved
    ///   ignore enemy Inhibition (handled per-slot, doesn't require all 4
    ///   slots to be filled).
    ///
    /// - Totale: all 4 slots are filled AND the 4 values match one of five
    ///   patterns (Poker, Doppia Coppia, Tris+Core, Catena, Scala). Triggers
    ///   Automatic Critical Success, forces every module's Static AND
    ///   Frequency effect regardless of Core condition, and doubles Scrap.
    /// </summary>
    public static class ResonanceDetector
    {
        /// <summary>
        /// Returns the set of slots whose die value is shared with at least
        /// one other placed die (Legame). Slots with no die placed are
        /// never included.
        /// </summary>
        public static HashSet<SlotType> DetectLegameSlots(Dictionary<SlotType, int> placedValues)
        {
            var result = new HashSet<SlotType>();

            foreach (var kvp in placedValues)
            {
                bool sharesValue = placedValues.Any(other => other.Key != kvp.Key && other.Value == kvp.Value);
                if (sharesValue) result.Add(kvp.Key);
            }

            return result;
        }

        /// <summary>
        /// Checks whether the 4 placed dice form a Full Resonance pattern.
        /// Requires exactly 4 values (one per slot) to be present.
        /// </summary>
        public static bool DetectFullResonance(Dictionary<SlotType, int> placedValues, int coreValue)
        {
            if (placedValues.Count < 4) return false;

            var values = placedValues.Values.OrderBy(v => v).ToList();

            return IsPoker(values)
                || IsDoublePair(values)
                || IsTripleAndCore(values, coreValue)
                || IsChainAndCore(values, coreValue)
                || IsStraight(values);
        }

        /// <summary>Poker: all 4 values identical.</summary>
        private static bool IsPoker(List<int> sorted)
        {
            return sorted[0] == sorted[1] && sorted[1] == sorted[2] && sorted[2] == sorted[3];
        }

        /// <summary>Doppia Coppia: two distinct pairs (e.g. [4,4,12,12]).</summary>
        private static bool IsDoublePair(List<int> sorted)
        {
            return sorted[0] == sorted[1] && sorted[2] == sorted[3] && sorted[0] != sorted[2];
        }

        /// <summary>Tris e Core: three equal values + the fourth equals the Core Die.</summary>
        private static bool IsTripleAndCore(List<int> sorted, int coreValue)
        {
            // Try every position as the "odd one out" that should match Core.
            if (sorted[0] == sorted[1] && sorted[1] == sorted[2] && sorted[3] == coreValue) return true;
            if (sorted[1] == sorted[2] && sorted[2] == sorted[3] && sorted[0] == coreValue) return true;
            return false;
        }

        /// <summary>Catena: one pair + the other two dice both equal the Core Die.</summary>
        private static bool IsChainAndCore(List<int> sorted, int coreValue)
        {
            // Count how many placed dice equal the Core value.
            int coreMatches = sorted.Count(v => v == coreValue);
            if (coreMatches < 2) return false;

            // Remove two Core-matching values, check if the remaining two form a pair.
            var remaining = new List<int>(sorted);
            remaining.Remove(coreValue);
            remaining.Remove(coreValue);

            return remaining.Count == 2 && remaining[0] == remaining[1];
        }

        /// <summary>Scala: the 4 values form a continuous numeric sequence, all distinct.</summary>
        private static bool IsStraight(List<int> sorted)
        {
            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i] != sorted[i - 1] + 1) return false;
            }
            return true;
        }
    }
}

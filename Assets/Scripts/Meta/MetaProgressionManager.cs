using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rollrate.Meta
{
    /// <summary>
    /// Persistent meta-progression, saved via PlayerPrefs so it survives
    /// across runs AND app restarts (unlike GameState, which resets on
    /// Fragmentation/new run per design). Holds Frammenti Residui and the
    /// set of modules/dice permanently unlocked to appear at an earlier
    /// Grade than their natural one (design doc: "Meta-progressione").
    /// </summary>
    public static class MetaProgressionManager
    {
        private const string FragmentsKey = "Rollrate_ResidualFragments";
        private const string UnlocksKey = "Rollrate_EarlyUnlocks"; // serialized "name:grade,name:grade,..."

        private const int UnlockCostPerGrade = 20;

        /// <summary>Reward for dying at Grade 1-5 (index 0-4). Design doc table.</summary>
        private static readonly int[] RewardByGradeReached = { 5, 12, 22, 35, 50 };
        private const int RunCompleteReward = 75;

        public static int ResidualFragments
        {
            get => PlayerPrefs.GetInt(FragmentsKey, 0);
            private set
            {
                PlayerPrefs.SetInt(FragmentsKey, Mathf.Max(0, value));
                PlayerPrefs.Save();
            }
        }

        /// <summary>DEBUG: wipes Frammenti Residui and all early unlocks back to zero/none. Doesn't touch any other PlayerPrefs key.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(FragmentsKey);
            PlayerPrefs.DeleteKey(UnlocksKey);
            PlayerPrefs.Save();
            Debug.Log("[MetaProgressionManager] Reset: Frammenti Residui e sblocchi anticipati azzerati.");
        }

        /// <summary>Call from RunManager.HandleDefeat(), BEFORE resetting state, with the Grade the player had reached.</summary>
        public static void AwardForDefeat(int gradeReached)
        {
            int idx = Mathf.Clamp(gradeReached - 1, 0, RewardByGradeReached.Length - 1);
            int reward = RewardByGradeReached[idx];
            ResidualFragments += reward;
            Debug.Log($"[MetaProgressionManager] Defeat at Grade {gradeReached}: +{reward} Frammenti Residui (totale: {ResidualFragments}).");
        }

        /// <summary>Call when the Grade V Terminal is defeated (full run completion).</summary>
        public static void AwardForRunComplete()
        {
            ResidualFragments += RunCompleteReward;
            Debug.Log($"[MetaProgressionManager] Run completata: +{RunCompleteReward} Frammenti Residui (totale: {ResidualFragments}).");
        }

        private static Dictionary<string, int> LoadUnlocks()
        {
            var result = new Dictionary<string, int>();
            string raw = PlayerPrefs.GetString(UnlocksKey, "");
            if (string.IsNullOrEmpty(raw)) return result;

            foreach (string entry in raw.Split(','))
            {
                string[] parts = entry.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out int grade))
                {
                    result[parts[0]] = grade;
                }
            }
            return result;
        }

        private static void SaveUnlocks(Dictionary<string, int> unlocks)
        {
            string serialized = string.Join(",", unlocks.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
            PlayerPrefs.SetString(UnlocksKey, serialized);
            PlayerPrefs.Save();
        }

        /// <summary>The Grade this item effectively appears from - its natural Grade, or earlier if unlocked.</summary>
        public static int GetEffectiveGrade(string itemName, int naturalGrade)
        {
            var unlocks = LoadUnlocks();
            return unlocks.TryGetValue(itemName, out int unlockedGrade) ? Mathf.Min(unlockedGrade, naturalGrade) : naturalGrade;
        }

        /// <summary>Flat cost for the next single-Grade-earlier unlock step (20, per design doc).</summary>
        public static int GetNextUnlockCost() => UnlockCostPerGrade;

        /// <summary>
        /// Pushes this item's effective Grade back by exactly one, for a
        /// flat cost of 20 Frammenti Residui. Fails if already at Grade I
        /// or if funds are insufficient.
        /// </summary>
        public static bool TryUnlockOneGradeEarlier(string itemName, int naturalGrade)
        {
            int current = GetEffectiveGrade(itemName, naturalGrade);
            if (current <= 1) return false;

            int cost = UnlockCostPerGrade;
            if (ResidualFragments < cost) return false;

            ResidualFragments -= cost;
            var unlocks = LoadUnlocks();
            unlocks[itemName] = current - 1;
            SaveUnlocks(unlocks);
            return true;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Rollrate.Simulation
{
    /// <summary>
    /// In-memory stand-in for MetaProgressionManager, used ONLY by the Run
    /// Simulator - mirrors its exact formulas (reward table, 20-per-Grade
    /// unlock cost) but never touches real PlayerPrefs, so simulating
    /// thousands of campaigns never pollutes the actual player's save data.
    /// </summary>
    public class SimMetaState
    {
        public int ResidualFragments;
        public Dictionary<string, int> Unlocks = new Dictionary<string, int>();

        private static readonly int[] RewardByGradeReached = { 5, 12, 22, 35, 50 };
        private const int RunCompleteReward = 75;
        private const int UnlockCostPerGrade = 20;

        public int AwardForDefeat(int gradeReached)
        {
            int idx = Mathf.Clamp(gradeReached - 1, 0, RewardByGradeReached.Length - 1);
            int reward = RewardByGradeReached[idx];
            ResidualFragments += reward;
            return reward;
        }

        public void AwardForRunComplete()
        {
            ResidualFragments += RunCompleteReward;
        }

        public int GetEffectiveGrade(string itemName, int naturalGrade)
        {
            return Unlocks.TryGetValue(itemName, out int unlockedGrade) ? Mathf.Min(unlockedGrade, naturalGrade) : naturalGrade;
        }

        public bool TryUnlockOneGradeEarlier(string itemName, int naturalGrade)
        {
            int current = GetEffectiveGrade(itemName, naturalGrade);
            if (current <= 1) return false;
            if (ResidualFragments < UnlockCostPerGrade) return false;

            ResidualFragments -= UnlockCostPerGrade;
            Unlocks[itemName] = current - 1;
            return true;
        }
    }
}

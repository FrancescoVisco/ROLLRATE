using System.Collections.Generic;
using UnityEngine;
using Rollrate.Core;
using Rollrate.Data;

namespace Rollrate.Dismantle
{
    /// <summary>
    /// Logic for the Dismantle Node: lets the player destroy an owned die
    /// or module in exchange for Scrap, based on the item's own Grade (see
    /// DismantleRewardTable) - not a percentage of purchase cost, and not
    /// available anywhere else (see design doc Section 6/7).
    ///
    /// Safety rules (enforced via GameState.CanDismantleDie/CanDismantleModule):
    /// - Dice: at least 4 must remain in the pool after removal.
    /// - Modules: at least 1 copy must remain for that slot after removal
    ///   (dismantling the last copy of a slot's only module is blocked).
    /// </summary>
    public class DismantleController : MonoBehaviour
    {
        [SerializeField] private DismantleRewardTable rewardTable;

        public System.Action OnInventoryChanged;

        public List<DieData> GetDistinctOwnedDice()
        {
            var state = RunManager.Instance.State;
            var seen = new List<DieData>();
            foreach (DieData d in state.dicePool)
            {
                if (d != null && !seen.Contains(d)) seen.Add(d);
            }
            return seen;
        }

        public int GetOwnedDieCount(DieData die)
        {
            var state = RunManager.Instance.State;
            int count = 0;
            foreach (DieData d in state.dicePool) if (d == die) count++;
            return count;
        }

        public List<ModuleData> GetDistinctOwnedModules()
        {
            var state = RunManager.Instance.State;
            var result = new List<ModuleData>();
            foreach (SlotType slot in System.Enum.GetValues(typeof(SlotType)))
            {
                if (!state.ownedModules.TryGetValue(slot, out var owned)) continue;
                foreach (ModuleData m in owned)
                {
                    if (m != null && !result.Contains(m)) result.Add(m);
                }
            }
            return result;
        }

        public int GetOwnedModuleCount(ModuleData module) => RunManager.Instance.State.GetOwnedModuleCount(module);

        public int GetDieReward(DieData die) => rewardTable.GetDieScrap(die.grade);
        public int GetModuleReward(ModuleData module) => rewardTable.GetModuleScrap(module.grade);

        public bool CanDismantleDie() => RunManager.Instance.State.CanDismantleDie();
        public bool CanDismantleModule(ModuleData module) => RunManager.Instance.State.CanDismantleModule(module);

        public bool TryDismantleDie(DieData die)
        {
            var state = RunManager.Instance.State;
            if (!state.CanDismantleDie()) return false;

            state.RemoveDiePermanently(die);
            state.scrap += GetDieReward(die);
            OnInventoryChanged?.Invoke();
            return true;
        }

        public bool TryDismantleModule(ModuleData module)
        {
            var state = RunManager.Instance.State;
            if (!state.CanDismantleModule(module)) return false;

            if (state.RemoveOwnedModule(module))
            {
                state.scrap += GetModuleReward(module);
                OnInventoryChanged?.Invoke();
                return true;
            }
            return false;
        }
    }
}

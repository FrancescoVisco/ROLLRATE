using UnityEngine;
using Rollrate.Data;

namespace Rollrate.Combat.Enemies
{
    /// <summary>
    /// Architect (Grade III Elite, D14) - Feedback: subtracts -2 from the
    /// value of every die placed in the Power slot.
    /// </summary>
    public class ArchitectAbility : EnemyAbilityBase
    {
        public override int ApplyThresholdModifier(int baseThreshold, EnemyAbilityContext ctx) => baseThreshold;

        public override int ModifyPlacedDieValue(SlotType slot, int rawValue, DieData dieType, EnemyAbilityContext ctx)
        {
            return slot == SlotType.Power ? Mathf.Max(0, rawValue - 2) : rawValue;
        }
    }
}

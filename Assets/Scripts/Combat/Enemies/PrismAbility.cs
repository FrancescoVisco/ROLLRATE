using UnityEngine;
using Rollrate.Data;

namespace Rollrate.Combat.Enemies
{
    /// <summary>
    /// Prism (Grade III Guardian) - Refraction: at the start of the turn,
    /// declares a random slot; every die placed there has its value halved
    /// (rounded down) for this turn.
    /// </summary>
    public class PrismAbility : EnemyAbilityBase
    {
        private static readonly SlotType[] AllSlots = { SlotType.Power, SlotType.Stability, SlotType.Flow, SlotType.Echo };

        public override void OnTurnStart(EnemyController enemy)
        {
            SlotType target = AllSlots[Random.Range(0, AllSlots.Length)];
            enemy.SetPrismTargetSlot(target);
            Debug.Log($"[PrismAbility] Refraction targets the {target} slot this turn.");
        }

        public override int ApplyThresholdModifier(int baseThreshold, EnemyAbilityContext ctx) => baseThreshold;

        public override int ModifyPlacedDieValue(SlotType slot, int rawValue, DieData dieType, EnemyAbilityContext ctx)
        {
            if (ctx.Enemy != null && ctx.Enemy.GetPrismTargetSlot() == slot)
            {
                return Mathf.FloorToInt(rawValue / 2f);
            }
            return rawValue;
        }
    }
}

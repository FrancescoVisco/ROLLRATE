using UnityEngine;
using Rollrate.Data;

namespace Rollrate.Combat.Enemies
{
    /// <summary>
    /// Avatar (Grade V Base, D12) - Void: the die placed in the ECHO slot
    /// adds half its value (rounded down) to the enemy Threshold.
    /// </summary>
    public class AvatarAbility : EnemyAbilityBase
    {
        public override int ApplyThresholdModifier(int baseThreshold, EnemyAbilityContext ctx)
        {
            if (ctx.PlacedValues != null && ctx.PlacedValues.TryGetValue(SlotType.Echo, out int echoValue))
            {
                return baseThreshold + Mathf.FloorToInt(echoValue / 2f);
            }
            return baseThreshold;
        }
    }
}

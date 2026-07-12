using Rollrate.Data;

namespace Rollrate.Combat.Enemies
{
    /// <summary>
    /// Null-Pointer (Grade V Elite, D12) - Glitch: dice showing their die's
    /// maximum face value "regress" and count as 1 instead.
    /// </summary>
    public class NullPointerAbility : EnemyAbilityBase
    {
        public override int ApplyThresholdModifier(int baseThreshold, EnemyAbilityContext ctx) => baseThreshold;

        public override int ModifyPlacedDieValue(SlotType slot, int rawValue, DieData dieType, EnemyAbilityContext ctx)
        {
            if (dieType != null && rawValue == dieType.faces)
            {
                return 1;
            }
            return rawValue;
        }
    }
}

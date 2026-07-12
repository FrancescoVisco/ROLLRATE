using Rollrate.Data;

namespace Rollrate.Combat
{
    /// <summary>
    /// Base class for an enemy's ability logic. One subclass per EnemyAbilityId.
    /// Only ApplyThresholdModifier is required; everything else has a no-op
    /// default, so simple abilities only need to override what they use.
    /// </summary>
    public abstract class EnemyAbilityBase
    {
        /// <summary>Returns the Threshold to use this turn.</summary>
        public abstract int ApplyThresholdModifier(int baseThreshold, EnemyAbilityContext ctx);

        /// <summary>
        /// Called once per turn, at the moment the enemy rolls its Inhibitor
        /// Die (before the player places anything). Use this for abilities
        /// that need to update persistent state every turn (e.g. Gatekeeper's
        /// growing Threshold, Prism's random target slot, Judge's growing
        /// inhibited set).
        /// </summary>
        public virtual void OnTurnStart(EnemyController enemy) { }

        /// <summary>
        /// Called once per turn, after the turn has been fully resolved.
        /// Use this for abilities that react to what just happened (e.g.
        /// Sovereign locking in this turn's Core value for next turn).
        /// </summary>
        public virtual void OnTurnEnd(EnemyController enemy, EnemyAbilityContext ctx) { }

        /// <summary>Extra flat damage to the player on a failed turn (e.g. Tracer).</summary>
        public virtual int GetBonusDamageOnFailure(EnemyAbilityContext ctx) => 0;

        /// <summary>A second value that's inhibited this turn, in addition to the normal roll (e.g. Sentinel). -1 = none.</summary>
        public virtual int GetExtraInhibitedValue(EnemyAbilityContext ctx) => -1;

        /// <summary>
        /// Lets an ability modify a placed die's effective value before it's
        /// resolved (e.g. Architect's -2 to Power, Prism's halving, Null-Pointer's
        /// max-face regression). Return the value unchanged if this ability
        /// doesn't affect that slot/die.
        /// </summary>
        public virtual int ModifyPlacedDieValue(SlotType slot, int rawValue, DieData dieType, EnemyAbilityContext ctx) => rawValue;
    }
}

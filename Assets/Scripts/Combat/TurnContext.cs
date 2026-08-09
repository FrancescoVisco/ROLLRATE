using System.Collections.Generic;
using System.Linq;
using Rollrate.Data;

namespace Rollrate.Combat
{
    /// <summary>
    /// The state of a single die during the current turn's Roll & Keep
    /// (design doc Section 4). One of these exists per die drawn into
    /// this turn's Hand (GameState.DrawHand), for the whole turn.
    ///
    /// INTERACTION MODEL: every die in the Hand ALWAYS counts toward
    /// Attacco/Difesa/rilanci-disponibili, whether or not the player has
    /// clicked it - a click only toggles markedForReroll (which dice the
    /// next Reroll will affect) or, for Echo dice, assigns echoTarget /
    /// echoTargetType.
    /// </summary>
    public class HeldDieState
    {
        public DieInstance instance;
        public int rolledValue;
        public bool markedForReroll;
        public bool isInhibited;
        [System.NonSerialized] public HeldDieState echoTarget;      // set only for Echo dice - a SINGLE other die in the Hand receiving its value
        [System.NonSerialized] public DieType? echoTargetType;      // set only for Echo dice carrying Chain - ALL non-Echo dice of this Type receive its value instead of a single die

        public HeldDieState(DieInstance instance)
        {
            this.instance = instance;
        }
    }

    /// <summary>
    /// Everything needed to resolve one turn: the Core's rolled value
    /// (fixed for the whole turn), the Inhibitor's corrupted value, and
    /// the roll state of every die drawn into this turn's Hand.
    ///
    /// EFFECTS COVERED HERE (design doc Section 5): Bulwark, Overclock,
    /// Amplify, Cleanse, Chain (single-turn, no cross-turn state needed).
    /// Overkill/Breach/Backlash/Cushion/Overflow/Drain are evaluated in
    /// TurnController.ResolveCheck instead, since they need the final
    /// summed Attack/Defense and the enemy's numbers. Suppress/Reverb
    /// (cross-turn state) and Reflect/SafetyNet/Cascade (reroll-triggered)
    /// are handled in TurnController, around RollAll/Reroll.
    /// </summary>
    public class TurnContext
    {
        public int coreValue;
        public bool coreIsEven;
        public ValueRange coreRange;
        public DieData coreDie;

        public int inhibitedValue;
        public bool hasInhibitedValue;
        /// <summary>Additional inhibited values on top of the Inhibitor's own roll - Sentinel adds "1" conditionally, Judge adds one permanently for the whole fight (design doc Section 8).</summary>
        public List<int> extraInhibitedValues = new List<int>();

        public List<HeldDieState> dice = new List<HeldDieState>();

        /// <summary>Cascade's accumulated bonus this turn (design doc Section 5) - added directly into ComputeAttack. Set by TurnController.Reroll.</summary>
        public float cascadeBonusThisTurn;

        /// <summary>True if this die carries the given Effect.</summary>
        /// <summary>
        /// True if this die carries the given Effect. Returns false
        /// unconditionally if the die is currently Inhibited - an
        /// inhibited die's value still counts fully toward Attacco/
        /// Difesa (no change there), but its Effects never activate and
        /// it can't vibrate (design doc Section 5, Inibizione: "diventa
        /// un numero e basta"). Centralized here so every Effect check
        /// in the game is automatically covered, without needing to gate
        /// each one individually.
        /// </summary>
        public bool HasEffect(HeldDieState d, EffectId id)
        {
            if (d == null) return false;
            if (IsInhibited(d)) return false;
            return d.instance?.effects != null && d.instance.effects.Any(e => e != null && e.id == id);
        }

        /// <summary>
        /// Whether this die is inhibited for calculation purposes: its
        /// raw value matches the Inhibitor's corrupted value, UNLESS an
        /// Echo die with Cleanse is currently targeting it (design doc
        /// Section 5, Cleanse) - Cleanse overrides the corruption entirely.
        /// </summary>
        public bool IsInhibited(HeldDieState d)
        {
            bool rawInhibited = (hasInhibitedValue && d.rolledValue == inhibitedValue) || extraInhibitedValues.Contains(d.rolledValue);
            if (!rawInhibited) return false;

            bool cleansed = dice.Any(other =>
                other.instance?.type == DieType.Echo &&
                IsTargeting(other, d) &&
                HasEffect(other, EffectId.Cleanse));

            return !cleansed;
        }

        /// <summary>True if this Echo die's assignment (single-die OR Chain type-wide) covers the given die.</summary>
        private bool IsTargeting(HeldDieState echoDie, HeldDieState candidate)
        {
            if (echoDie.echoTarget == candidate) return true;
            if (echoDie.echoTargetType.HasValue && candidate.instance?.type == echoDie.echoTargetType.Value) return true;
            return false;
        }

        /// <summary>Vibrazione net multiplier for a single die (axes shared + Max/Min bonus) - x1 flat if inhibited (see design doc Section 5, Inibizione).</summary>
        public float GetNetMultiplier(HeldDieState d)
        {
            if (d.instance?.data == null) return 1f;
            if (IsInhibited(d)) return 1f;
            return ResonanceDetector.GetNetMultiplier(d.rolledValue, d.instance.data, coreValue, coreIsEven, coreRange);
        }

        /// <summary>
        /// The die's value after its Vibrazione multiplier, PLUS the
        /// effective value of any Echo die targeting it - either
        /// directly (echoTarget) or via Chain (echoTargetType matching
        /// this die's Type) - doubled per source if that Echo die
        /// carries Amplify. If this die is inhibited and a targeting
        /// Echo die carries Cleanse, no value is added for THAT source
        /// (spent on curing the Inhibition instead - see IsInhibited).
        /// </summary>
        public float GetEffectiveValue(HeldDieState d)
        {
            float baseValue = d.rolledValue * GetNetMultiplier(d);

            float echoBonus = 0f;
            foreach (var other in dice)
            {
                if (other.instance?.type != DieType.Echo || !IsTargeting(other, d)) continue;

                bool cleansedThisTransfer = HasEffect(other, EffectId.Cleanse) &&
                    hasInhibitedValue && d.rolledValue == inhibitedValue;
                if (cleansedThisTransfer) continue; // spent on Cleanse, not on value

                float otherValue = other.rolledValue * GetNetMultiplier(other);
                if (HasEffect(other, EffectId.Amplify)) otherValue *= 2f;
                echoBonus += otherValue;
            }

            return baseValue + echoBonus;
        }

        /// <summary>Every die of a given Type in the Hand - ALL of them count, regardless of markedForReroll.</summary>
        public List<HeldDieState> GetOfType(DieType type)
        {
            return dice.Where(d => d.instance != null && d.instance.type == type).ToList();
        }

        /// <summary>
        /// Rilanci disponibili questo turno (design doc Section 5, Tipo
        /// del Dado - Flow, and Overclock): per ogni Dado Flow nella
        /// Mano, il suo moltiplicatore di Vibrazione arrotondato
        /// all'intero più vicino, +1 se il suo valore grezzo è Alto,
        /// +Grado se porta Overclock.
        /// </summary>
        public int ComputeAvailableRerolls()
        {
            int total = 0;
            foreach (var d in GetOfType(DieType.Flow))
            {
                if (IsInhibited(d)) continue; // "diventa un numero e basta" - Flow's whole role (granting rerolls) IS its Vibrazione, so inhibited Flow grants 0, not round(x1)=1

                float mult = GetNetMultiplier(d);
                int rounded = UnityEngine.Mathf.RoundToInt(mult);
                bool isHigh = d.instance.data != null && d.instance.data.GetRange(d.rolledValue) == ValueRange.High;
                total += rounded + (isHigh ? 1 : 0);

                if (HasEffect(d, EffectId.Overclock))
                {
                    total += UnityEngine.Mathf.Clamp(d.instance.data.grade, 1, 5);
                }
            }
            return total;
        }

        /// <summary>Attacco: sum of effective values of every Power die, plus Cascade's accumulated bonus this turn.</summary>
        public float ComputeAttack()
        {
            float sum = cascadeBonusThisTurn;
            foreach (var d in GetOfType(DieType.Power)) sum += GetEffectiveValue(d);
            return sum;
        }

        /// <summary>Difesa: sum of effective values of every Stability die, doubled per-die for any that carry Bulwark (design doc Section 5).</summary>
        public float ComputeDefense()
        {
            float sum = 0f;
            foreach (var d in GetOfType(DieType.Stability))
            {
                float value = GetEffectiveValue(d);
                if (HasEffect(d, EffectId.Bulwark)) value *= 2f;
                sum += value;
            }
            return sum;
        }
    }
}

using System.Collections.Generic;

namespace Rollrate.Combat
{
    /// <summary>
    /// Snapshot of the current turn's state, passed to modules so they can
    /// compute their Static Effect and Frequency Effect. Lives only for the
    /// duration of a single turn.
    /// </summary>
    public class CombatContext
    {
        public int CoreValue;
        public Data.ValueRange CoreRange;
        public bool CoreIsEven;

        public int InhibitedValue; // X rolled by the enemy's Inhibitor Die

        public int PlayerHp;
        public int PlayerHpMax;
        public int HpLostThisTurn;

        public int EnemyThreshold;

        [System.NonSerialized]
        public int PointsExceedingThreshold; // set by CombatController in a second pass, after Power total is known

        // How many slots were activated/empty this turn (for Affinity/Solitary)
        public int SlotsActivatedThisTurn;
        public int EmptySlotsThisTurn;

        // Raw rolled values placed per slot
        public Dictionary<Data.SlotType, List<int>> DiceBySlot = new();

        public bool FullResonanceActive;

        public int ScrapGainedThisTurn;
    }

    /// <summary>
    /// Result of a module's calculation: how much it contributes, plus any
    /// side effects (healing, scrap, deferred buffs) to apply to GameState.
    ///
    /// Some fields represent effects that reach beyond this single slot's
    /// immediate value (e.g. board-wide Inhibition immunity, next-turn
    /// Threshold reduction). Wiring these into GameState/CombatController
    /// fully is a later step - for now ModuleResolver reports them here so
    /// nothing about the module's intended behavior gets lost.
    /// </summary>
    public struct ModuleResult
    {
        public int ValueBonus;       // e.g. bonus added to the Power total
        public int HpRecovered;
        public int ScrapGained;
        public bool IgnoresInhibition;          // this slot ignores Inhibition
        public bool IgnoresInhibitionBoardWide; // Shield's Frequency: whole board ignores Inhibition

        public int DamageReduction;             // Covering: flat reduction to incoming damage
        public float NextThresholdReductionPercent; // Aiming: % drop applied to the next enemy Threshold
        public int ChargesGenerated;            // Changeover: 10 Charges = +1 Die
        public bool ReturnDieToPoolNextTurn;     // Mirror (Even): keep the die instead of discarding it
        public int DiceToReroll;                 // Second Chance: how many dice may be rerolled
        public bool RerollKeepsBetterResult;      // Second Chance (High DCD): keep better of old/new
        public int TempDiceToAddNextTurn;         // Reinforcements: how many temporary dice to add
        public bool AddedDiceCopyCoreType;        // Reinforcements (Even): copies Core Die type instead of D4
        public float NextShopDiscountPercent;      // Scrap (Odd): discount on next purchase
        public int NextTurnPowerBonus;             // Overload: flat bonus to next turn's first Power die

        public string DebugLog;
    }
}

using UnityEngine;

namespace Rollrate.Data
{
    /// <summary>Which tier of enemy this is, within its Grade - used by the Map to pick the right pool for Conflict (Base) vs Overload (Elite) nodes.</summary>
    public enum EnemyTier
    {
        Base,
        Elite,
        Guardian
    }

    [CreateAssetMenu(fileName = "SO_Enemy_", menuName = "Rollrate/Enemy")]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName;
        [TextArea] public string flavorText;

        [Header("Grade & Tier")]
        [Tooltip("1-5, matching the Echelon Grade this enemy belongs to.")]
        public int grade = 1;
        [Tooltip("Base = Nodo Conflitto pool, Elite = Nodo Sovraccarico pool, Guardian = Terminal node only.")]
        public EnemyTier tier = EnemyTier.Base;

        [Header("Combat Stats")]
        public int maxHp;
        public int baseThreshold;
        [Tooltip("What the player's Defense must equal or exceed each turn (design doc Section 4, Attacco nemico vs Difesa).")]
        public int baseAttack;

        [Header("Inhibitor Die")]
        [Tooltip("The die this enemy rolls each turn to determine the Inhibited value.")]
        public DieData inhibitorDie;

        [Header("Ability")]
        public EnemyAbilityId abilityId;
        [TextArea] public string abilityDescription;

        [Header("Guardian Only - Data Extraction (design doc Section 8)")]
        [Tooltip("Only meaningful for tier=Guardian: the Core Die this Guardian's defeat evolves the player's Core into (e.g. Gatekeeper -> D8). Leave empty for Base/Elite enemies.")]
        public DieData coreEvolutionOnDefeat;

        [Header("Visual")]
        public Sprite icon;
    }
}


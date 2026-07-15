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

        [Header("Inhibitor Die")]
        [Tooltip("The die this enemy rolls each turn to determine the Inhibited value.")]
        public DieData inhibitorDie;

        [Header("Ability")]
        public EnemyAbilityId abilityId;
        [TextArea] public string abilityDescription;

        [Header("Visual")]
        public Sprite icon;
    }
}


using UnityEngine;

namespace Rollrate.Data
{
    [CreateAssetMenu(fileName = "SO_Enemy_", menuName = "Rollrate/Enemy")]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName;
        [TextArea] public string flavorText;

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

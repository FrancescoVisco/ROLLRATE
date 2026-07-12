using UnityEngine;

namespace Rollrate.Data
{
    [CreateAssetMenu(fileName = "SO_Module_", menuName = "Rollrate/Module")]
    public class ModuleData : ScriptableObject
    {
        [Header("Identity")]
        public ModuleId id;
        public SlotType slot;
        public string displayName;
        [TextArea] public string flavorText;

        [Header("Descriptions (for UI/tooltips)")]
        [TextArea] public string staticEffectDescription;
        [TextArea] public string frequencyEffectDescription;

        [Header("Cost per Grade (I -> V)")]
        public int[] costPerGrade = new int[5];

        [Header("Visual")]
        public Sprite icon;
    }
}

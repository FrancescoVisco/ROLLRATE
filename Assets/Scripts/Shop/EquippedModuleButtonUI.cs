using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Rollrate.Data;
using Rollrate.UI;

namespace Rollrate.Shop
{
    /// <summary>
    /// A single row representing one OWNED module for a given slot. Clicking
    /// it equips that module (replacing whatever was equipped for that slot
    /// before). Visually marks whichever one is currently equipped. On
    /// hover, shows the same Static/Frequency tooltip used by SlotDropZone
    /// in combat - works here too since this scene is loaded additively on
    /// top of Combat, where TooltipUI actually lives.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class EquippedModuleButtonUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Color equippedColor = new Color(0.2f, 0.8f, 0.3f);
        [SerializeField] private Color notEquippedColor = Color.white;

        private ModuleData _module;
        private EquipmentPanelUI _panel;

        public void Setup(ModuleData module, bool isEquipped, int ownedCount, EquipmentPanelUI panel)
        {
            _module = module;
            _panel = panel;

            if (nameText != null)
            {
                string countTag = ownedCount > 1 ? $" x{ownedCount}" : "";
                string equippedTag = isEquipped ? " (Equipped)" : "";
                nameText.text = $"{module.displayName}{countTag}{equippedTag}";
                nameText.color = isEquipped ? equippedColor : notEquippedColor;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _panel.EquipModule(_module);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_module == null || TooltipUI.Instance == null) return;

            string tooltipText = $"<b>{_module.displayName}</b>\n\n" +
                                  $"<b>Static:</b>\n{_module.staticEffectDescription}\n\n" +
                                  $"<b>Frequency:</b>\n{_module.frequencyEffectDescription}";
            TooltipUI.Instance.Show(tooltipText);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipUI.Instance?.Hide();
        }
    }
}


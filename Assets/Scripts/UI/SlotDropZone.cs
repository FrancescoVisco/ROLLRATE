using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Rollrate.Data;

namespace Rollrate.UI
{
    /// <summary>
    /// Attach this to each of the 4 slot UI containers (Power, Stability,
    /// Flow, Echo). When a DraggableDie is dropped on it, it snaps the die
    /// into place and remembers which die is currently placed here.
    ///
    /// Requires: an Image component (even a transparent one) so raycasts
    /// can hit this object - IDropHandler needs something to detect the drop.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class SlotDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Slot Identity")]
        public SlotType slotType;

        [Header("Currently Placed Die (read-only at runtime)")]
        public DraggableDie placedDie;

        [Header("Module Name Label (always visible)")]
        [Tooltip("Shows the installed module's name, e.g. 'Charge'.")]
        [SerializeField] private TextMeshProUGUI moduleNameLabel;

        private ModuleData _installedModule;

        /// <summary>
        /// Updates this slot's name label and remembers the installed
        /// module, so its effect description can be shown in a tooltip
        /// on hover. Pass null to show an empty slot.
        /// </summary>
        public void SetModuleInfo(ModuleData module)
        {
            _installedModule = module;

            if (moduleNameLabel != null)
            {
                moduleNameLabel.text = module != null ? module.displayName : "(empty)";
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_installedModule == null || TooltipUI.Instance == null) return;

            string tooltipText = $"<b>{_installedModule.displayName}</b>\n\n" +
                                  $"<b>Static:</b>\n{_installedModule.staticEffectDescription}\n\n" +
                                  $"<b>Frequency:</b>\n{_installedModule.frequencyEffectDescription}";
            TooltipUI.Instance.Show(tooltipText);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipUI.Instance?.Hide();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            CombatController.Instance?.TryOnSlotClicked(slotType);
        }

        public void OnDrop(PointerEventData eventData)
        {
            var droppedObject = eventData.pointerDrag;
            if (droppedObject == null) return;

            var die = droppedObject.GetComponent<DraggableDie>();
            if (die == null) return;
            if (die.isLocked) return; // Locked dice (Core, enemy Inhibitor) can never be placed in a slot

            // If this slot already has a die, send the old one back to the pool area.
            if (placedDie != null)
            {
                placedDie.ReturnToStart();
            }

            placedDie = die;

            var dieRect = die.GetComponent<RectTransform>();
            dieRect.SetParent(transform, true);
            dieRect.anchoredPosition = Vector2.zero;

            Debug.Log($"[SlotDropZone] {slotType} slot now holds a {die.dieType?.displayName} showing {die.rolledValue}");
        }

        /// <summary>
        /// Clears this slot's reference without touching the die's position.
        /// If this slot was Flow's currently chosen target, notifies
        /// CombatController so the target selection resets (the die's value
        /// is restored to its original roll by DraggableDie itself).
        /// </summary>
        public void Clear()
        {
            placedDie = null;
            CombatController.Instance?.NotifySlotCleared(slotType);
        }

        /// <summary>
        /// Sends the currently placed die back to where it came from (the
        /// hand) and clears this slot. Safe to call even if the slot is empty.
        /// </summary>
        public void ReturnPlacedDie()
        {
            if (placedDie != null)
            {
                placedDie.ReturnToStart();
                Clear();
            }
        }
    }
}

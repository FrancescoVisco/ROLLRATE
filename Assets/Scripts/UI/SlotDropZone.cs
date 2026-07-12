using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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
    public class SlotDropZone : MonoBehaviour, IDropHandler
    {
        [Header("Slot Identity")]
        public SlotType slotType;

        [Header("Currently Placed Die (read-only at runtime)")]
        public DraggableDie placedDie;

        public void OnDrop(PointerEventData eventData)
        {
            var droppedObject = eventData.pointerDrag;
            if (droppedObject == null) return;

            var die = droppedObject.GetComponent<DraggableDie>();
            if (die == null) return;
            if (die.isCoreDie) return; // Core Die can never be placed in a slot

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

        /// <summary>Clears this slot's reference without touching the die's position.</summary>
        public void Clear()
        {
            placedDie = null;
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
                placedDie = null;
            }
        }
    }
}

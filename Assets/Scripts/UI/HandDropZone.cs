using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rollrate.UI
{
    /// <summary>
    /// Attach this to the HandContainer panel. When a die is dropped here,
    /// it snaps back into the hand - this is how the player removes a die
    /// from a slot without needing a "Reset" button: just drag it back out.
    ///
    /// Requires: an Image component so raycasts can hit this object.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class HandDropZone : MonoBehaviour, IDropHandler
    {
        public void OnDrop(PointerEventData eventData)
        {
            var droppedObject = eventData.pointerDrag;
            if (droppedObject == null) return;

            var die = droppedObject.GetComponent<DraggableDie>();
            if (die == null || die.isCoreDie) return;

            var dieRect = die.GetComponent<RectTransform>();
            dieRect.SetParent(transform, true);

            // Let the die settle wherever it was dropped within the hand -
            // simplest UX, no need to force a fixed slot position back.
        }
    }
}

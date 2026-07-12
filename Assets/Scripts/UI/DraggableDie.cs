using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using Rollrate.Data;

namespace Rollrate.UI
{
    /// <summary>
    /// Attach this to a UI Image/GameObject representing a single rolled die
    /// in the player's hand. Lets the player drag it with the mouse and drop
    /// it onto a SlotDropZone.
    ///
    /// Requires: a CanvasGroup component on the same GameObject (used to
    /// disable raycasts while dragging, so the drop zone underneath can
    /// detect the drop).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    public class DraggableDie : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Die Data")]
        public DieData dieType;   // which die this is (D4, D6, ...)
        public int rolledValue;   // the value rolled this turn

        [Header("Core Die Flag")]
        [Tooltip("True if this is the player's Core Die. Core dice are never placed into slots - they only determine Frequency conditions (Even/Odd/Low/High) for all slots this turn.")]
        public bool isCoreDie;

        [Header("Optional Visuals")]
        [Tooltip("A child TextMeshProUGUI element used to display the rolled value. Optional.")]
        public TextMeshProUGUI valueLabel;

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Canvas _rootCanvas;
        private Vector2 _startPosition;
        private Transform _startParent;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            _rootCanvas = GetComponentInParent<Canvas>();

            // Force a centered anchor/pivot regardless of whatever parent
            // this ends up under (HandContainer, a Slot, the root Canvas).
            // This makes anchoredPosition behave consistently everywhere -
            // without this, stretched-anchor parents can make the same
            // anchoredPosition value land in very different visual spots.
            _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>
        /// Sets the die's type, rolled value, and whether it's the Core Die,
        /// then refreshes the on-screen label.
        /// </summary>
        public void Setup(DieData type, int value, bool isCore = false)
        {
            dieType = type;
            rolledValue = value;
            isCoreDie = isCore;
            if (valueLabel != null)
            {
                valueLabel.text = value.ToString();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (isCoreDie)
            {
                // The Core Die never gets placed into a slot - it stays
                // fixed in its display area and only drives Frequency conditions.
                eventData.pointerDrag = null;
                return;
            }

            _startPosition = _rectTransform.anchoredPosition;
            _startParent = transform.parent;

            // If this die is currently sitting in a slot, free that slot
            // now - regardless of where the die ends up (a new slot, or
            // back in the hand), it should no longer count as placed there.
            var previousSlot = _startParent.GetComponent<SlotDropZone>();
            if (previousSlot != null && previousSlot.placedDie == this)
            {
                previousSlot.Clear();
            }

            // Move to the root canvas while dragging so it renders on top
            // of everything else, then ignore raycasts so drop zones underneath
            // can detect the pointer.
            transform.SetParent(_rootCanvas.transform, true);
            _canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isCoreDie) return;
            _rectTransform.anchoredPosition += eventData.delta / _rootCanvas.scaleFactor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (isCoreDie) return;

            _canvasGroup.blocksRaycasts = true;

            // If nothing accepted the drop (SlotDropZone would have already
            // re-parented this object), snap back to where it started.
            if (transform.parent == _rootCanvas.transform)
            {
                ReturnToStart();
            }
        }

        public void ReturnToStart()
        {
            transform.SetParent(_startParent, true);
            _rectTransform.anchoredPosition = _startPosition;
        }
    }
}

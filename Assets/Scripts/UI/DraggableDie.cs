using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using Rollrate.Data;

namespace Rollrate.UI
{
    /// <summary>
    /// Attach this to a UI Image/GameObject representing a single rolled die.
    /// Normally lets the player drag it with the mouse and drop it onto a
    /// SlotDropZone. When isLocked is true, the die is display-only and
    /// cannot be dragged at all - used for the player's Core Die and the
    /// enemy's Inhibitor Die display, both shown purely for information.
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
        public int rolledValue;   // the CURRENT value (may have been changed by Mirror/Shift/Reverse or an enemy ability)

        [Header("Locked Flag")]
        [Tooltip("True for dice that are never placed into slots and never draggable: the player's Core Die and the enemy's Inhibitor Die display.")]
        public bool isLocked;

        [Header("Optional Visuals")]
        [Tooltip("A child TextMeshProUGUI element used to display the rolled value. Optional.")]
        public TextMeshProUGUI valueLabel;
        [Tooltip("A child GameObject (e.g. a red border/overlay) toggled on when this die's value matches the enemy's Inhibited value. Optional.")]
        [SerializeField] private GameObject inhibitedIndicator;

        // The value/inhibited-state this die had at the moment it was rolled
        // this turn, before any Mirror/Shift/Reverse/enemy-ability mutation.
        // Restored automatically if the die is returned to the hand.
        private int _originalRolledValue;
        private bool _originalInhibited;

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
        /// Sets the die's type, rolled value, locked state, and whether it
        /// starts inhibited, then refreshes the on-screen label/indicator.
        /// This value/inhibited-state is remembered as the "original" this
        /// turn, restored automatically if the die returns to the hand.
        /// </summary>
        public void Setup(DieData type, int value, bool locked = false, bool inhibited = false)
        {
            dieType = type;
            rolledValue = value;
            isLocked = locked;
            _originalRolledValue = value;
            _originalInhibited = inhibited;

            if (valueLabel != null)
            {
                valueLabel.text = value.ToString();
            }
            SetInhibited(inhibited);
        }

        /// <summary>
        /// Overrides the rolled value, its label, and its Inhibited
        /// indicator, without touching lock state, position, or the
        /// remembered "original" value - used when a Flow module
        /// (Mirror/Shift/Reverse) or an enemy ability changes this die's
        /// effective value mid-resolution, so the player sees the new
        /// number (and whether it's now inhibited) reflected immediately.
        /// </summary>
        public void OverrideValue(int newValue, bool? inhibited = null)
        {
            rolledValue = newValue;
            if (valueLabel != null)
            {
                valueLabel.text = newValue.ToString();
            }
            if (inhibited.HasValue)
            {
                SetInhibited(inhibited.Value);
            }
        }

        /// <summary>Shows or hides the "this value is inhibited" visual indicator.</summary>
        public void SetInhibited(bool inhibited)
        {
            if (inhibitedIndicator != null)
            {
                inhibitedIndicator.SetActive(inhibited);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (isLocked)
            {
                // Locked dice (Core Die, enemy Inhibitor Die) are display-only.
                eventData.pointerDrag = null;
                return;
            }

            _startPosition = _rectTransform.anchoredPosition;
            _startParent = transform.parent;

            // If this die is currently sitting in a slot, free that slot
            // now - regardless of where the die ends up (a new slot, or
            // back in the hand), it should no longer count as placed there.
            // Any Mirror/Shift/Reverse/enemy-ability mutation only applies
            // while the die stays ON that slot: leaving it, for any reason,
            // immediately reverts it to what was actually rolled this turn.
            var previousSlot = _startParent.GetComponent<SlotDropZone>();
            if (previousSlot != null && previousSlot.placedDie == this)
            {
                RevertToOriginalIfChanged();
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
            if (isLocked) return;
            _rectTransform.anchoredPosition += eventData.delta / _rootCanvas.scaleFactor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (isLocked) return;

            _canvasGroup.blocksRaycasts = true;

            // If nothing accepted the drop (SlotDropZone would have already
            // re-parented this object), snap back to where it started.
            if (transform.parent == _rootCanvas.transform)
            {
                ReturnToStart();
            }
        }

        /// <summary>
        /// Reverts this die to the value/inhibited-state it had when rolled
        /// this turn, if it currently differs (i.e. a Flow module or enemy
        /// ability had changed it). Safe to call even if nothing changed.
        /// </summary>
        public void RevertToOriginalIfChanged()
        {
            if (rolledValue != _originalRolledValue)
            {
                OverrideValue(_originalRolledValue, _originalInhibited);
            }
        }

        public void ReturnToStart()
        {
            transform.SetParent(_startParent, true);
            _rectTransform.anchoredPosition = _startPosition;

            // Safety net: the value should already have been reverted in
            // OnBeginDrag the moment this die left its slot, but this stays
            // as a fallback in case ReturnToStart is ever called directly.
            RevertToOriginalIfChanged();
        }
    }
}

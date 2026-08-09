using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Rollrate.Data;
using Rollrate.Combat;

namespace Rollrate.UI
{
    /// <summary>
    /// One rolled die, shown in the hand for the whole turn (design doc
    /// Section 4, Roll & Keep). Replaces the old DraggableDie entirely -
    /// there is no more dragging or Slot placement at all: a die always
    /// sits in the hand, and a single click toggles whether it's MARKED
    /// FOR REROLL (see TurnController.ToggleRerollMark) - every die in
    /// the hand always counts toward Attacco/Difesa/rilanci regardless of
    /// this mark; marking only decides what the next Reroll will affect.
    /// The Core Die and the enemy's Inhibitor Die display use this same
    /// component with isLocked=true (display-only, not clickable).
    ///
    /// Visuals: the die's main Image is always tinted by its DieType
    /// (rosso/blu/viola/verde - DieTypeColors), since that's a permanent
    /// property of the die. Held state, Inhibited state, and the
    /// Vibrazione multiplier are shown through separate elements instead
    /// of retinting the main Image (which must stay the Type color).
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class RollableDie : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Die State")]
        public HeldDieState state; // null for the Core Die / enemy Inhibitor display
        [Tooltip("True for the Core Die and the enemy's Inhibitor Die display - never clickable, never held.")]
        public bool isLocked;

        [Header("Optional Visuals")]
        [Tooltip("The Image that shows the die's Type color (red/blue/purple/green). If left empty, falls back to the Image component on this SAME GameObject (required by [RequireComponent]) - assign this explicitly if your die's visible artwork is actually on a CHILD object instead, otherwise the wrong (often invisible) Image gets tinted and colors will look like they're 'not working'.")]
        [SerializeField] private Image mainImage;
        [Tooltip("Shows the rolled value.")]
        public TextMeshProUGUI valueLabel;
        [Tooltip("Shows the net Vibrazione multiplier (e.g. 'x2.5'), hidden at x1. Color scales toward gold (bonus) or burnt orange (malus) - see SetVibrationMultiplier.")]
        public TextMeshProUGUI vibrationMultiplierLabel;
        [Tooltip("A child GameObject (e.g. a border/glow) toggled on while this die is SELECTED - marked for reroll, or (for Echo dice) awaiting a transfer target. Its color is forced to yellow by code (SelectedColor below) if it has its own Image component, so it can never end up the wrong color regardless of how it was colored in the prefab - yellow is reserved for 'selected', grey is reserved for inhibitedIndicator only.")]
        [SerializeField] private GameObject heldIndicator;
        [Tooltip("A child GameObject (e.g. a border/icon) toggled on while this die's value matches the enemy's Inhibited value. Its color is forced to grey by code (InhibitedColor below) if it has its own Image component - grey reads as 'disabled/switched off', distinct from Power's red and from SelectedColor's yellow.")]
        [SerializeField] private GameObject inhibitedIndicator;

        private Image _image;
        private Image _heldIndicatorImage; // cached from heldIndicator, if it has one - forced yellow, never left to chance
        private Image _inhibitedIndicatorImage; // cached from inhibitedIndicator, if it has one - forced grey, never left to chance

        private static readonly Color VibrationColorNeutral = Color.white;
        private static readonly Color VibrationColorBonus = new Color(1f, 0.85f, 0.3f);   // gold
        private static readonly Color VibrationColorMalus = new Color(0.8f, 0.35f, 0.15f); // burnt orange
        private static readonly Color SelectedColor = new Color(0.95f, 0.85f, 0.15f);      // yellow - "selected" (marked for reroll, or Echo pending a target), NEVER grey (grey = inhibited only)
        private static readonly Color InhibitedColor = new Color(0.45f, 0.45f, 0.45f);      // grey - reads as "disabled/switched off", distinct from Power's red and SelectedColor's yellow

        private void Awake()
        {
            _image = mainImage != null ? mainImage : GetComponent<Image>();
            if (heldIndicator != null) _heldIndicatorImage = heldIndicator.GetComponent<Image>();
            if (inhibitedIndicator != null) _inhibitedIndicatorImage = inhibitedIndicator.GetComponent<Image>();

            // Force a centered anchor/pivot regardless of whatever parent
            // this ends up under (HandContainer with a Layout Group,
            // CoreDieContainer/InhibitorDieContainer without one) - without
            // this, a container with no Layout Group can leave the die
            // wherever its anchor/pivot happened to be set in the prefab,
            // instead of centered.
            var rect = GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
            }
        }

        /// <summary>Sets up this die for a fresh roll. Pass state=null for the Core Die / Inhibitor display (isLocked should be true in that case).</summary>
        public void Setup(HeldDieState state, bool isLocked)
        {
            this.state = state;
            this.isLocked = isLocked;

            if (_image == null) _image = mainImage != null ? mainImage : GetComponent<Image>();
            if (!isLocked && state?.instance != null)
            {
                var typeColor = DieTypeColors.For(state.instance.type);
                _image.color = typeColor;
                Debug.Log($"[RollableDie] '{name}' set to {state.instance.type} color {typeColor} on {(mainImage != null ? "assigned Main Image" : "own GetComponent<Image>")}.");
            }

            RefreshValue();
            RefreshHeldIndicator();
        }

        /// <summary>
        /// Sets up this die as a LOCKED display-only die showing a value
        /// directly, with no DieInstance/DieType behind it - used for the
        /// Core Die and the enemy's Inhibitor Die, neither of which has a
        /// Type (design doc Section 5, Dado Core) or ever gets held/rerolled.
        /// </summary>
        public void SetupLockedDisplay(int value, Color tint)
        {
            state = null;
            isLocked = true;

            if (_image == null) _image = mainImage != null ? mainImage : GetComponent<Image>();
            _image.color = tint;

            if (valueLabel != null) valueLabel.text = value.ToString();
            if (vibrationMultiplierLabel != null) vibrationMultiplierLabel.text = string.Empty;
            RefreshHeldIndicator();
        }

        /// <summary>
        /// Re-asserts the Type color unconditionally - called from EVERY
        /// visual refresh touchpoint (RefreshValue, RefreshInhibited,
        /// RefreshHeldIndicator, SetVibrationMultiplier), not just Setup,
        /// as a safety net: the Type color must never silently drift away
        /// from DieTypeColors.For(type) no matter what else touches this
        /// die's visuals over the course of a turn.
        /// </summary>
        private void ReapplyTypeColor()
        {
            if (isLocked || state?.instance == null || _image == null) return;
            _image.color = DieTypeColors.For(state.instance.type);
        }

        /// <summary>Refreshes the shown value - call any time state.rolledValue changes (e.g. after a Reroll).</summary>
        public void RefreshValue()
        {
            if (state == null) return;
            if (valueLabel != null) valueLabel.text = state.rolledValue.ToString();
            ReapplyTypeColor();
        }

        /// <summary>Shows/hides the "inhibited" indicator - call any time inhibition state might have changed (e.g. after a Reroll).</summary>
        public void RefreshInhibited(bool isInhibited)
        {
            if (inhibitedIndicator != null) inhibitedIndicator.SetActive(isInhibited);

            // Same defensive pattern as SelectedColor: only force the color while
            // actually shown, and never touch it if inhibitedIndicator turns out to
            // share the die's OWN Image component (would overwrite the Type color).
            if (isInhibited && _inhibitedIndicatorImage != null && _inhibitedIndicatorImage != _image)
            {
                _inhibitedIndicatorImage.color = InhibitedColor;
            }
            else if (_inhibitedIndicatorImage == _image && _inhibitedIndicatorImage != null)
            {
                Debug.LogWarning($"[RollableDie] '{name}': Inhibited Indicator is wired to the die's OWN Image - it must be a SEPARATE child GameObject, otherwise it overwrites the Type color. Fix this in the prefab.", this);
            }

            ReapplyTypeColor();
        }

        /// <summary>
        /// Shows the die's current net Vibrazione multiplier (Vibrazione
        /// axes + Max/Min bonus, already combined - design doc Section 5).
        /// Hidden entirely at exactly x1 (nothing to report).
        /// </summary>
        public void SetVibrationMultiplier(float netMultiplier)
        {
            ReapplyTypeColor();
            if (vibrationMultiplierLabel == null) return;

            if (Mathf.Approximately(netMultiplier, 1f))
            {
                vibrationMultiplierLabel.text = string.Empty;
                return;
            }

            vibrationMultiplierLabel.text = $"x{netMultiplier:0.#}";
            vibrationMultiplierLabel.color = netMultiplier > 1f
                ? Color.Lerp(VibrationColorNeutral, VibrationColorBonus, Mathf.Clamp01((netMultiplier - 1f) / 2f))
                : Color.Lerp(VibrationColorNeutral, VibrationColorMalus, Mathf.Clamp01((1f - netMultiplier) / 2f));
        }

        private bool _pendingEchoSourceFlag; // Echo die awaiting a target
        private bool _echoLinkedFlag;        // this die is CURRENTLY the target of an assigned Echo transfer

        private void RefreshHeldIndicator()
        {
            bool shouldShow = (state != null && state.markedForReroll) || _pendingEchoSourceFlag || _echoLinkedFlag;
            if (heldIndicator != null) heldIndicator.SetActive(shouldShow);
            ReapplyTypeColor();

            // Defensive: only force yellow while actually shown, and never touch it if
            // heldIndicator turns out to share the die's OWN Image component (a likely
            // scene-wiring mistake - "Held Indicator" should be a SEPARATE child, not
            // the die's main Image) - that mistake was overwriting the Type color on
            // every single interaction, since this used to run unconditionally.
            if (shouldShow && _heldIndicatorImage != null && _heldIndicatorImage != _image)
            {
                _heldIndicatorImage.color = SelectedColor;
            }
            else if (_heldIndicatorImage == _image && _heldIndicatorImage != null)
            {
                Debug.LogWarning($"[RollableDie] '{name}': Held Indicator is wired to the die's OWN Image - it must be a SEPARATE child GameObject, otherwise it overwrites the Type color. Fix this in the prefab.", this);
            }
        }

        /// <summary>
        /// Shows/hides the yellow "selected" highlight for an Echo die
        /// currently awaiting a target (design doc Section 4, ECHO
        /// SYSTEM) - there was previously NO visual feedback at all for
        /// this state, only the status text.
        /// </summary>
        public void SetPendingEchoSource(bool isPending)
        {
            _pendingEchoSourceFlag = isPending;
            RefreshHeldIndicator();
        }

        /// <summary>
        /// Shows/hides the SAME yellow highlight, but for the RECEIVING
        /// side of an Echo transfer once it's been assigned (design doc
        /// Section 4) - addresses "non si capisce che lo stai
        /// selezionando" (the target die previously showed nothing at
        /// all once chosen). Stays on for as long as the assignment
        /// lasts (until Resolve, reassignment, or cancellation).
        /// </summary>
        public void SetEchoLinked(bool isLinked)
        {
            _echoLinkedFlag = isLinked;
            RefreshHeldIndicator();
        }

        /// <summary>Single click toggles HELD (design doc: "Click singolo") - no-op for locked display dice (Core/Inhibitor).</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (isLocked || state == null) return;
            RollKeepUIController.Instance?.OnDieClicked(this);
        }

        /// <summary>Called by RollKeepUIController right after it toggles state.markedForReroll, so the visual catches up.</summary>
        public void RefreshHeld()
        {
            RefreshHeldIndicator();
        }

        /// <summary>What a die of this Type does with no Effects attached (design doc Section 5, Tipo del Dado) - the first line of every tooltip.</summary>
        private static string BaseEffectDescription(DieType type)
        {
            switch (type)
            {
                case DieType.Power: return "Its effective value contributes to Attack.";
                case DieType.Stability: return "Its effective value contributes to Defense.";
                case DieType.Flow: return "Grants rerolls based on its own Vibration multiplier.";
                case DieType.Echo: return "Transfers its effective value to another chosen die.";
                default: return string.Empty;
            }
        }

        /// <summary>Shows the die's Type, its base effect, and every attached Effect with its full description (design doc Section 5). No-op for locked display dice (Core/Inhibitor have neither).</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isLocked || state?.instance == null || TooltipUI.Instance == null) return;

            var type = state.instance.type;
            string text = $"{type} — {BaseEffectDescription(type)}";

            if (state.instance.effects != null && state.instance.effects.Count > 0)
            {
                foreach (var effect in state.instance.effects)
                {
                    if (effect == null) continue;
                    text += $"\n\n{effect.displayName}: {effect.effectDescription}";
                }
            }

            TooltipUI.Instance.Show(text);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipUI.Instance?.Hide();
        }
    }
}

using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Rollrate.Data;

namespace Rollrate.Meta
{
    /// <summary>
    /// One selectable die candidate on the Meta screen (design doc Section
    /// 7). Shows the die's Type color, size (faces), and attached Effects;
    /// click to select it (MetaController handles the "only one selected
    /// at a time" logic via SetSelected, and clears the selection entirely
    /// if the player clicks anywhere outside a candidate).
    ///
    /// Selection uses the SAME yellow used everywhere else in the game for
    /// "this is selected" (RollableDie's SelectedColor) - tinted directly
    /// onto this card's own root Image, no separate glow/border object
    /// needed.
    /// </summary>
    public class MetaDieCandidateUI : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("The card's own background Image (also used for click detection) - tinted yellow when selected, neutral otherwise.")]
        [SerializeField] private Image cardBackground;
        [SerializeField] private Image typeColorImage;
        [SerializeField] private TextMeshProUGUI dieLabel; // e.g. "Power D8"
        [SerializeField] private TextMeshProUGUI effectsLabel; // e.g. "Bulwark, Overclock" - empty if none

        private static readonly Color SelectedColor = new Color(0.95f, 0.85f, 0.15f); // same yellow as RollableDie's "selected" highlight
        private static readonly Color NeutralColor = Color.white;

        public DieInstance Die { get; private set; }
        private Action<DieInstance> _onClicked;

        public void Setup(DieInstance die, Action<DieInstance> onClicked)
        {
            Die = die;
            _onClicked = onClicked;

            if (typeColorImage != null) typeColorImage.color = DieTypeColors.For(die.type);
            if (dieLabel != null) dieLabel.text = $"{die.type} D{die.data?.faces}";

            if (effectsLabel != null)
            {
                effectsLabel.text = die.effects != null && die.effects.Count > 0
                    ? string.Join(", ", die.effects.Where(e => e != null).Select(e => e.displayName))
                    : string.Empty;
            }

            SetSelected(false);
        }

        public void SetSelected(bool isSelected)
        {
            if (cardBackground != null) cardBackground.color = isSelected ? SelectedColor : NeutralColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _onClicked?.Invoke(Die);
        }
    }
}

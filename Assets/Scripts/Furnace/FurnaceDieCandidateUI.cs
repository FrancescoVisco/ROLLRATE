using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Rollrate.Data;

namespace Rollrate.Furnace
{
    /// <summary>
    /// One owned die shown on the Furnace shelf (design doc Section 7,
    /// Nodo Furnace) - click to select it as one of the 2 dice to fuse.
    /// Same pattern as MetaDieCandidateUI/ShopOfferUI: click DIRECTLY on
    /// the icon (IPointerClickHandler), not a generic Button, and
    /// "selected" uses the same yellow used everywhere else in the game.
    /// </summary>
    public class FurnaceDieCandidateUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image dieImage; // shows the Type color normally; temporarily yellow while selected (also the click target)
        [SerializeField] private TextMeshProUGUI dieLabel; // e.g. "Power D8"
        [SerializeField] private TextMeshProUGUI effectsLabel; // e.g. "Bulwark, Overclock" - empty if none

        private static readonly Color SelectedColor = new Color(0.95f, 0.85f, 0.15f); // same yellow as everywhere else in the game
        private Color _typeColor; // remembered so it can be restored once deselected

        public DieInstance Die { get; private set; }
        private Action<DieInstance> _onClicked;

        public void Setup(DieInstance die, Action<DieInstance> onClicked)
        {
            Die = die;
            _onClicked = onClicked;

            _typeColor = DieTypeColors.For(die.type);
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
            if (dieImage != null) dieImage.color = isSelected ? SelectedColor : _typeColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _onClicked?.Invoke(Die);
        }
    }
}

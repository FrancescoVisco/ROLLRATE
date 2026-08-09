using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Rollrate.Data;

namespace Rollrate.Shop
{
    /// <summary>
    /// The clickable die offer itself (design doc Section 6, Nodo Dice
    /// Dealer) - click DIRECTLY on the die's own icon to buy it, no
    /// separate "Buy" button. Uses IPointerClickHandler (same pattern as
    /// RollableDie/MetaDieCandidateUI) instead of UnityEngine.UI.Button on
    /// purpose: a Button's own hover/press color transitions would fight
    /// with this icon's Type-color tint on the SAME Image, the exact kind
    /// of color conflict already found and fixed in Combat.
    /// </summary>
    public class ShopOfferUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image typeColorImage;
        [SerializeField] private TextMeshProUGUI dieLabel; // e.g. "Power D8"
        [SerializeField] private TextMeshProUGUI effectsLabel; // e.g. "Bulwark, Overclock" - empty if none
        [SerializeField] private TextMeshProUGUI costText; // e.g. "15 Scrap"

        private Action _onClicked;

        public void Setup(DieInstance offer, string costLabel, Action onClicked)
        {
            _onClicked = onClicked;

            if (typeColorImage != null) typeColorImage.color = DieTypeColors.For(offer.type);
            if (dieLabel != null) dieLabel.text = $"{offer.type} D{offer.data?.faces}";
            if (costText != null) costText.text = costLabel;

            if (effectsLabel != null)
            {
                effectsLabel.text = offer.effects != null && offer.effects.Count > 0
                    ? string.Join(", ", offer.effects.Where(e => e != null).Select(e => e.displayName))
                    : string.Empty;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _onClicked?.Invoke();
        }
    }
}

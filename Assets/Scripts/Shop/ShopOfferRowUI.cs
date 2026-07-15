using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Rollrate.Core;
using Rollrate.UI;

namespace Rollrate.Shop
{
    /// <summary>
    /// A single clickable offer row, instantiated dynamically by ShopUI for
    /// each item in ShopController.CurrentOffers. The whole row is the
    /// clickable area (via IPointerClickHandler on this Image) - no separate
    /// Buy button needed. Buying a Module you already own is allowed on
    /// purpose (see ShopController) - this row just shows how many you own,
    /// it never blocks the purchase. On hover, shows a tooltip: Static/
    /// Frequency effect for modules, faces/Low-High ranges for dice.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class ShopOfferRowUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color ownedColor = new Color(0.6f, 0.6f, 0.9f, 1f);

        private ShopOffer _offer;
        private ShopController _controller;

        /// <summary>Populates this row and wires it up to buy the given offer on click.</summary>
        public void Setup(ShopOffer offer, ShopController controller)
        {
            _offer = offer;
            _controller = controller;

            int ownedCount = controller.GetOfferOwnedCount(offer);
            bool owned = ownedCount > 0;

            if (nameText != null)
            {
                string ownedTag = owned ? $" (Owned x{ownedCount})" : "";
                nameText.text = offer.IsModule ? $"{offer.module.displayName} ({offer.module.slot}){ownedTag}" : offer.die.displayName;
                nameText.color = owned ? ownedColor : normalColor;
            }

            if (costText != null)
            {
                costText.text = $"{controller.GetOfferCost(offer)} Scrap";
                costText.color = owned ? ownedColor : normalColor;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _controller.TryBuyOffer(_offer);
            // ShopController.OnOffersChanged fires from TryBuyOffer on success,
            // which ShopUI listens to in order to fully redraw the board.
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_offer == null || TooltipUI.Instance == null) return;

            string tooltipText;
            if (_offer.IsModule)
            {
                var m = _offer.module;
                tooltipText = $"<b>{m.displayName}</b> ({m.slot})\n\n" +
                              $"<b>Static:</b>\n{m.staticEffectDescription}\n\n" +
                              $"<b>Frequency:</b>\n{m.frequencyEffectDescription}";
            }
            else
            {
                var d = _offer.die;
                tooltipText = $"<b>{d.displayName}</b>\n\n" +
                              $"Faces: {d.faces}\n" +
                              $"Low: 1-{d.lowMax}\n" +
                              $"High: {d.highMin}-{d.faces}";
            }
            TooltipUI.Instance.Show(tooltipText);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipUI.Instance?.Hide();
        }
    }
}


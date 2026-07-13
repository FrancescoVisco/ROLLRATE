using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Rollrate.Core;

namespace Rollrate.Shop
{
    /// <summary>
    /// A single clickable offer row, instantiated dynamically by ShopUI for
    /// each item in ShopController.CurrentOffers. The whole row is the
    /// clickable area (via IPointerClickHandler on this Image) - no separate
    /// Buy button needed.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class ShopOfferRowUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color alreadyOwnedColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        private ShopOffer _offer;
        private ShopController _controller;
        private bool _purchasable = true;

        /// <summary>Populates this row and wires it up to buy the given offer on click.</summary>
        public void Setup(ShopOffer offer, ShopController controller)
        {
            _offer = offer;
            _controller = controller;

            // Safety net: GenerateOffers already excludes owned modules, so
            // this should be rare, but if it ever happens, block the
            // purchase and say so clearly instead of silently failing.
            bool alreadyOwned = offer.IsModule && RunManager.Instance.State.OwnsModule(offer.module);
            _purchasable = !alreadyOwned;

            if (nameText != null)
            {
                nameText.text = offer.IsModule ? $"{offer.module.displayName} ({offer.module.slot})" : offer.die.displayName;
                nameText.color = alreadyOwned ? alreadyOwnedColor : normalColor;
            }

            if (costText != null)
            {
                costText.text = alreadyOwned ? "Already Owned" : $"{controller.GetOfferCost(offer)} Scrap";
                costText.color = alreadyOwned ? alreadyOwnedColor : normalColor;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_purchasable) return;
            _controller.TryBuyOffer(_offer);
            // ShopController.OnOffersChanged fires from TryBuyOffer on success,
            // which ShopUI listens to in order to fully redraw the board.
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Rollrate.Archive
{
    /// <summary>
    /// One selectable row for choice panels (e.g. "which die do you want to
    /// evolve/lose"). Generic: just a name and a click callback - unlike
    /// DismantleRowUI, doesn't show a Scrap reward since these choices
    /// aren't about Scrap.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class ArchiveChoiceRowUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TextMeshProUGUI nameText;
        private System.Action _onClick;

        public void Setup(string label, System.Action onClick)
        {
            if (nameText != null) nameText.text = label;
            _onClick = onClick;
        }

        public void OnPointerClick(PointerEventData eventData) => _onClick?.Invoke();
    }
}

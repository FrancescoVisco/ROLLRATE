using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Rollrate.Meta
{
    /// <summary>
    /// One row in the Meta screen: a distinct module or die, its natural
    /// Grade, its current effective Grade (natural, or earlier if
    /// unlocked), and its cost to push one Grade earlier. The whole row
    /// is the clickable area (via IPointerClickHandler on this Image),
    /// same pattern as ShopOfferRowUI/DismantleRowUI/EquippedModuleButtonUI
    /// - no separate Button component needed.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class MetaRowUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color blockedColor = new Color(0.5f, 0.5f, 0.5f);

        private string _keyName;      // stable ID (asset name) used for PlayerPrefs storage
        private string _displayName;  // what's shown to the player
        private int _naturalGrade;
        private MetaUI _panel;
        private bool _canUnlock;

        public void Setup(string keyName, string displayName, int naturalGrade, MetaUI panel)
        {
            _keyName = keyName;
            _displayName = displayName;
            _naturalGrade = naturalGrade;
            _panel = panel;

            Refresh();
        }

        public void Refresh()
        {
            int effectiveGrade = MetaProgressionManager.GetEffectiveGrade(_keyName, _naturalGrade);
            _canUnlock = effectiveGrade > 1;

            if (nameText != null)
            {
                nameText.text = _displayName;
                nameText.color = _canUnlock ? normalColor : blockedColor;
            }

            if (statusText != null)
            {
                statusText.text = effectiveGrade < _naturalGrade
                    ? $"Grado naturale {_naturalGrade} - sbloccato a Grado {effectiveGrade}"
                    : $"Grado naturale {_naturalGrade}";
                statusText.color = _canUnlock ? normalColor : blockedColor;
            }

            if (costText != null)
            {
                costText.text = _canUnlock ? $"{MetaProgressionManager.GetNextUnlockCost()} Frammenti Residui" : "Già al Grado I";
                costText.color = _canUnlock ? normalColor : blockedColor;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_canUnlock) return;

            if (MetaProgressionManager.TryUnlockOneGradeEarlier(_keyName, _naturalGrade))
            {
                _panel.RefreshAll();
            }
        }
    }
}

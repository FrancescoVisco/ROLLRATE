using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Rollrate.UI;

namespace Rollrate.Dismantle
{
    /// <summary>
    /// One row in the Dismantle Node's list: shows a distinct owned die or
    /// module, how many copies you own, and the Scrap reward for
    /// destroying ONE copy. Clicking it dismantles one copy immediately
    /// (no confirmation dialog) if allowed - grayed out and unclickable if
    /// the safety minimum (GameState.CanDismantleDie/CanDismantleModule)
    /// would be violated. On hover, shows the same Static/Frequency (for
    /// modules) tooltip used elsewhere.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class DismantleRowUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI rewardText;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color blockedColor = new Color(0.5f, 0.5f, 0.5f);

        private DismantleItem _item;
        private DismantleController _controller;
        private DismantleUI _panel;
        private bool _canDismantle;

        public void Setup(DismantleItem item, DismantleController controller, DismantleUI panel)
        {
            _item = item;
            _controller = controller;
            _panel = panel;

            int count;
            int reward;
            string name;

            if (item.IsModule)
            {
                count = controller.GetOwnedModuleCount(item.module);
                reward = controller.GetModuleReward(item.module);
                name = $"{item.module.displayName} ({item.module.slot}) x{count}";
                _canDismantle = controller.CanDismantleModule(item.module);
            }
            else
            {
                count = controller.GetOwnedDieCount(item.die);
                reward = controller.GetDieReward(item.die);
                name = $"{item.die.displayName} x{count}";
                _canDismantle = controller.CanDismantleDie();
            }

            if (nameText != null)
            {
                nameText.text = name;
                nameText.color = _canDismantle ? normalColor : blockedColor;
            }

            if (rewardText != null)
            {
                rewardText.text = _canDismantle ? $"+{reward} Scrap" : "Cannot dismantle (minimum reached)";
                rewardText.color = _canDismantle ? normalColor : blockedColor;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_canDismantle) return;

            bool success = _item.IsModule
                ? _controller.TryDismantleModule(_item.module)
                : _controller.TryDismantleDie(_item.die);

            if (success) _panel.Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_item == null || TooltipUI.Instance == null) return;

            string tooltipText;
            if (_item.IsModule)
            {
                var m = _item.module;
                tooltipText = $"<b>{m.displayName}</b> ({m.slot})\n\n<b>Static:</b>\n{m.staticEffectDescription}\n\n<b>Frequency:</b>\n{m.frequencyEffectDescription}";
            }
            else
            {
                var d = _item.die;
                tooltipText = $"<b>{d.displayName}</b>\n\nFaces: {d.faces}\nLow: 1-{d.lowMax}\nHigh: {d.highMin}-{d.faces}";
            }
            TooltipUI.Instance.Show(tooltipText);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipUI.Instance?.Hide();
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Rollrate.Map
{
    /// <summary>
    /// One clickable node button on the Map. Shows the node's type, is
    /// grayed out/unclickable unless it's currently reachable from the
    /// player's position, and turns a "visited" color once entered.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class MapNodeButtonUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Color reachableColor = Color.white;
        [SerializeField] private Color lockedColor = new Color(0.4f, 0.4f, 0.4f);
        [SerializeField] private Color visitedColor = new Color(0.3f, 0.6f, 0.3f);
        [SerializeField] private Color currentColor = new Color(0.9f, 0.8f, 0.2f);

        private MapNode _node;
        private MapController _controller;
        private Image _image;

        private void Awake()
        {
            // Force a centered anchor/pivot regardless of the prefab's own
            // setup - without this, anchoredPosition can land in very
            // different visual spots depending on inherited anchors.
            var rect = GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        public void Setup(MapNode node, MapController controller)
        {
            _node = node;
            _controller = controller;
            _image = GetComponent<Image>();

            if (label != null) label.text = node.type.ToString();
            Refresh();
        }

        public void Refresh()
        {
            if (_node.isCurrent) _image.color = currentColor;
            else if (_node.visited) _image.color = visitedColor;
            else if (_controller.IsReachable(_node)) _image.color = reachableColor;
            else _image.color = lockedColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _controller.TryEnterNode(_node);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace Rollrate.Map
{
    /// <summary>
    /// Marker component for the connection-line prefab. Forces a centered
    /// anchor/pivot on Awake, same reasoning as MapNodeButtonUI - so
    /// MapController's midpoint/rotation math behaves consistently
    /// regardless of how the prefab's RectTransform was set up in the Editor.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class MapConnectionLineUI : MonoBehaviour
    {
        private void Awake()
        {
            var rect = GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}

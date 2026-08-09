using UnityEngine;
using TMPro;

namespace Rollrate.UI
{
    /// <summary>
    /// Single shared tooltip panel. Any UI element can call
    /// TooltipUI.Instance.Show("some text") on hover and Hide() on exit.
    /// The panel follows the mouse while visible, and resizes itself to fit
    /// the text every time Show() is called - sizing is computed directly
    /// in code (via TMP's preferredHeight) rather than relying on Content
    /// Size Fitter/Layout Group, which can be unreliable right after
    /// activating a previously-inactive GameObject.
    ///
    /// SETUP REQUIREMENTS (Inspector):
    /// - panelRect and textRect must both have pivot = (0,0) and
    ///   anchorMin = anchorMax = (0,0) (a single point, not stretched).
    /// - textRect must be a child of panelRect.
    /// - Remove any Content Size Fitter / Vertical Layout Group components -
    ///   this script fully replaces their job.
    /// </summary>
    public class TooltipUI : MonoBehaviour
    {
        public static TooltipUI Instance { get; private set; }

        [Header("References")]
        [SerializeField] private GameObject panel;          // the background box, toggled active/inactive
        [SerializeField] private TextMeshProUGUI contentText;
        [SerializeField] private RectTransform panelRect;   // the background box's own RectTransform
        [SerializeField] private RectTransform textRect;    // contentText's own RectTransform (child of panelRect)
        [SerializeField] private Canvas rootCanvas;

        [Header("Layout")]
        [SerializeField] private float wrapWidth = 280f;
        [SerializeField] private float padding = 16f;
        [SerializeField] private Vector2 mouseOffset = new Vector2(12f, 12f);

        private void Awake()
        {
            Instance = this;
            if (panel != null) panel.SetActive(false);
        }

        private void Update()
        {
            if (panel == null || !panel.activeSelf || rootCanvas == null || panelRect == null) return;

            var canvasRect = rootCanvas.transform as RectTransform;

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                Input.mousePosition,
                rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera,
                out localPoint);

            Vector2 size = panelRect.sizeDelta;
            Rect bounds = canvasRect.rect;

            // Default: grow up-and-right from the cursor (pivot is (0,0), so
            // this anchored position IS the panel's bottom-left corner).
            float x = localPoint.x + mouseOffset.x;
            float y = localPoint.y + mouseOffset.y;

            // Flip to the opposite side whenever the default placement would
            // push the panel past that edge of the canvas - grows left/down
            // from the cursor instead, so the tooltip never spills off-screen
            // near the borders.
            if (x + size.x > bounds.xMax) x = localPoint.x - mouseOffset.x - size.x;
            if (y + size.y > bounds.yMax) y = localPoint.y - mouseOffset.y - size.y;

            // Final safety clamp: even after flipping, a corner case (cursor
            // right in a screen corner) could still overflow the opposite
            // edge - pin it fully inside the canvas as a last resort.
            x = Mathf.Clamp(x, bounds.xMin, bounds.xMax - size.x);
            y = Mathf.Clamp(y, bounds.yMin, bounds.yMax - size.y);

            panelRect.anchoredPosition = new Vector2(x, y);
        }

        /// <summary>
        /// Shows the tooltip with the given text, resizing the panel to fit
        /// it (fixed wrap width, height computed from the actual content).
        /// </summary>
        public void Show(string text)
        {
            if (panel == null || contentText == null || panelRect == null || textRect == null) return;

            panel.SetActive(true);
            contentText.text = text;

            // Fix the text's wrap width, then measure how tall it needs to be.
            Vector2 textSize = textRect.sizeDelta;
            textSize.x = wrapWidth;
            textRect.sizeDelta = textSize;

            contentText.ForceMeshUpdate();
            float preferredHeight = contentText.preferredHeight;

            textSize.y = preferredHeight;
            textRect.sizeDelta = textSize;
            textRect.anchoredPosition = new Vector2(padding, padding);

            panelRect.sizeDelta = new Vector2(wrapWidth + padding * 2f, preferredHeight + padding * 2f);
        }

        /// <summary>Hides the tooltip.</summary>
        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }
    }
}

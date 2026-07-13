using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Rollrate.Combat;

namespace Rollrate.UI
{
    /// <summary>
    /// Attach this to a UI Image container that wraps the enemy's visual
    /// representation (e.g. a background panel around the enemy name/HP/
    /// portrait) - NOT directly on a Text element, whose RectTransform is
    /// usually tightly cropped around the glyphs and would only trigger
    /// hover near the actual characters instead of the whole visual area.
    /// On hover, shows a tooltip with the enemy's name, flavor text, and
    /// ability description - same pattern as SlotDropZone's module tooltip.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class EnemyInfoDisplay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private EnemyController enemyController;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (enemyController == null || enemyController.Data == null || TooltipUI.Instance == null) return;

            var data = enemyController.Data;
            string tooltipText = $"<b>{data.displayName}</b>\n\n" +
                                  $"{data.flavorText}\n\n" +
                                  $"<b>Ability:</b>\n{data.abilityDescription}";
            TooltipUI.Instance.Show(tooltipText);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipUI.Instance?.Hide();
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using Rollrate.Combat;

namespace Rollrate.UI
{
    /// <summary>
    /// Attach this to whatever UI element represents the enemy (e.g. the
    /// enemy name/HP label, or a portrait icon). On hover, shows a tooltip
    /// with the enemy's name, flavor text, and ability description - same
    /// pattern as SlotDropZone's module tooltip.
    /// </summary>
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

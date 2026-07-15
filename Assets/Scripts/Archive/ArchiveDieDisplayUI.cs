using UnityEngine;
using TMPro;

namespace Rollrate.Archive
{
    /// <summary>
    /// A static, non-interactive die visual - same look as the combat dice
    /// (DraggableDie), minus any drag/drop behavior. Used by ArchiveUI to
    /// show the actual rolled value(s) behind each Test's result, instead
    /// of just a text number.
    /// </summary>
    public class ArchiveDieDisplayUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI valueLabel;

        public void Setup(int value)
        {
            if (valueLabel != null) valueLabel.text = value.ToString();
        }
    }
}

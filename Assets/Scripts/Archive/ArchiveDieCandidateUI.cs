using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Rollrate.Data;

namespace Rollrate.Archive
{
    /// <summary>
    /// One owned die shown when the player must choose which one to lose
    /// (Test di Tributo failure, design doc Section 7). Same pattern as
    /// FurnaceDieCandidateUI: single Image doubles as Type-color display
    /// and click target, no separate glow object.
    /// </summary>
    public class ArchiveDieCandidateUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image dieImage;
        [SerializeField] private TextMeshProUGUI dieLabel; // e.g. "Power D8"
        [SerializeField] private TextMeshProUGUI effectsLabel; // e.g. "Bulwark, Overclock" - empty if none

        private DieInstance _die;
        private Action<DieInstance> _onClicked;

        public void Setup(DieInstance die, Action<DieInstance> onClicked)
        {
            _die = die;
            _onClicked = onClicked;

            if (dieImage != null) dieImage.color = DieTypeColors.For(die.type);
            if (dieLabel != null) dieLabel.text = $"{die.type} D{die.data?.faces}";

            if (effectsLabel != null)
            {
                effectsLabel.text = die.effects != null && die.effects.Count > 0
                    ? string.Join(", ", die.effects.Where(e => e != null).Select(e => e.displayName))
                    : string.Empty;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _onClicked?.Invoke(_die);
        }
    }
}

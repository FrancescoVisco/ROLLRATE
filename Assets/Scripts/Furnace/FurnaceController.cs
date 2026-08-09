using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rollrate.Core;
using Rollrate.Data;

namespace Rollrate.Furnace
{
    /// <summary>
    /// Nodo Furnace (design doc Section 7): select exactly 2 OWNED dice
    /// of the SAME Type to fuse. Same Grade -> result advances one full
    /// Grade up (walking DieData.nextTier until the Grade number
    /// increases, robust to Grades spanning 1 or 2 die sizes). Different
    /// Grade -> result keeps the LOWER of the two Grades. Either way, the
    /// result's Effects are the union of both sources' Effects
    /// (deduplicated), capped at 4 - the absolute maximum, per design
    /// doc's Grado degli Effetti section. The 2 source dice are consumed.
    /// </summary>
    public class FurnaceController : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Cost to fuse, per current Grade (I-V) - waived entirely if the Archive's 'Fusione Gratuita' reward is pending.")]
        [SerializeField] private int[] fusionCostByGrade = { 20, 35, 55, 80, 100 };

        [Header("UI - Shared")]
        [SerializeField] private TextMeshProUGUI scrapText;
        [SerializeField] private TextMeshProUGUI hpText;

        [Header("UI")]
        [SerializeField] private FurnaceDieCandidateUI candidatePrefab;
        [SerializeField] private Transform candidatesContainer;
        [SerializeField] private Button fuseButton;
        [SerializeField] private TextMeshProUGUI fuseCostText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button leaveButton;
        [Tooltip("Must match the actual name of this Furnace scene (renamed from DismantleScene) - used to return to the Map via NodeSceneLoader.ExitNode.")]
        [SerializeField] private string furnaceSceneName = "FurnaceScene";

        private const int MaxEffectsOnFusedDie = 4; // absolute hard cap (design doc Section 5)

        private readonly List<FurnaceDieCandidateUI> _spawnedCandidates = new List<FurnaceDieCandidateUI>();
        private readonly List<DieInstance> _selected = new List<DieInstance>(); // up to 2

        private void Start()
        {
            if (fuseButton != null)
            {
                fuseButton.onClick.AddListener(OnFuseClicked);
                fuseButton.interactable = false;
            }
            if (leaveButton != null) leaveButton.onClick.AddListener(() => NodeSceneLoader.ExitNode(furnaceSceneName));

            SpawnShelf();
            RefreshStatus();
            RefreshSharedUI();
        }

        private int GetFusionCost()
        {
            var state = RunManager.Instance?.State;
            if (state == null || fusionCostByGrade.Length == 0) return 0;
            int index = Mathf.Clamp(state.currentEchelon - 1, 0, fusionCostByGrade.Length - 1);
            return fusionCostByGrade[index];
        }

        /// <summary>Scrap/HP are always shown, same convention as Shop/Archive.</summary>
        private void RefreshSharedUI()
        {
            var state = RunManager.Instance?.State;
            if (state == null) return;

            if (scrapText != null) scrapText.text = $"Scrap: {state.scrap}";
            if (hpText != null) hpText.text = $"HP: {state.currentHp}/{state.maxHp}";

            if (fuseCostText != null)
            {
                fuseCostText.text = ArchiveRewardContext.FreeFusionPending ? "Gratis" : $"{GetFusionCost()} Scrap";
            }
        }

        /// <summary>Shows every die the player currently owns as a selectable candidate.</summary>
        private void SpawnShelf()
        {
            foreach (var view in _spawnedCandidates)
            {
                if (view != null) Destroy(view.gameObject);
            }
            _spawnedCandidates.Clear();
            _selected.Clear();

            var state = RunManager.Instance?.State;
            if (state == null || candidatePrefab == null || candidatesContainer == null) return;

            foreach (var die in state.dicePool)
            {
                var view = Instantiate(candidatePrefab, candidatesContainer);
                view.Setup(die, OnCandidateClicked);
                _spawnedCandidates.Add(view);
            }
        }

        /// <summary>Click toggles selection (up to 2 at once) - reclicking a selected die deselects it, same pattern as everywhere else.</summary>
        private void OnCandidateClicked(DieInstance die)
        {
            if (_selected.Contains(die))
            {
                _selected.Remove(die);
            }
            else
            {
                if (_selected.Count >= 2)
                {
                    // Already have 2 selected - clicking a third replaces the OLDEST selection, not a silent no-op.
                    _selected.RemoveAt(0);
                }
                _selected.Add(die);
            }

            foreach (var view in _spawnedCandidates) view.SetSelected(_selected.Contains(view.Die));

            bool validPair = _selected.Count == 2 && _selected[0].type == _selected[1].type;
            bool canAfford = ArchiveRewardContext.FreeFusionPending || (RunManager.Instance?.State.scrap ?? 0) >= GetFusionCost();
            if (fuseButton != null) fuseButton.interactable = validPair && canAfford;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (statusText == null) return;

            if (_selected.Count == 0)
            {
                statusText.text = "Seleziona 2 dadi dello stesso Tipo da fondere.";
            }
            else if (_selected.Count == 1)
            {
                statusText.text = $"Selezionato: {_selected[0].type} D{_selected[0].data?.faces}. Scegline un altro dello stesso Tipo.";
            }
            else if (_selected[0].type != _selected[1].type)
            {
                statusText.text = $"I due dadi devono avere lo stesso Tipo ({_selected[0].type} + {_selected[1].type} non va bene).";
            }
            else
            {
                bool sameGrade = _selected[0].data?.grade == _selected[1].data?.grade;
                statusText.text = sameGrade
                    ? $"Pronti alla Fusione: {_selected[0].type} di Grado superiore, Effetti combinati."
                    : $"Pronti alla Fusione: risultato di Grado {Mathf.Min(_selected[0].data?.grade ?? 1, _selected[1].data?.grade ?? 1)}, Effetti combinati.";
            }
        }

        private void OnFuseClicked()
        {
            var state = RunManager.Instance?.State;
            if (state == null || _selected.Count != 2 || _selected[0].type != _selected[1].type) return;

            bool isFree = ArchiveRewardContext.FreeFusionPending; // design doc Section 7, Archive's "Fusione Gratuita"
            int cost = isFree ? 0 : GetFusionCost();
            if (!isFree && state.scrap < cost) return; // can't afford it - click silently does nothing

            state.scrap -= cost;
            if (isFree) ArchiveRewardContext.FreeFusionPending = false; // spent - only the NEXT fusion after the reward is free

            DieInstance a = _selected[0];
            DieInstance b = _selected[1];

            bool sameGrade = a.data != null && b.data != null && a.data.grade == b.data.grade;
            DieData resultData = sameGrade ? AdvanceOneGrade(a.data) : LowerGradeOf(a.data, b.data);

            var fused = new DieInstance(resultData, a.type);
            foreach (var e in a.effects) fused.AddEffect(e);
            foreach (var e in b.effects) fused.AddEffect(e);
            if (fused.effects.Count > MaxEffectsOnFusedDie)
            {
                fused.effects.RemoveRange(MaxEffectsOnFusedDie, fused.effects.Count - MaxEffectsOnFusedDie);
            }

            state.RemoveDiePermanently(a);
            state.RemoveDiePermanently(b);
            state.AddDieToPool(fused, fromRunUnlock: true); // the fused result is a NEW die acquired this run - eligible for the Meta screen on defeat

            if (statusText != null) statusText.text = $"Fuso: {fused.type} D{fused.data?.faces}, {fused.effects.Count} Effetti.";
            SpawnShelf();
            if (fuseButton != null) fuseButton.interactable = false;
            RefreshSharedUI();
        }

        /// <summary>Walks DieData.nextTier until the Grade number actually increases - robust to a Grade spanning 1 or 2 die sizes (e.g. D4->D6 is still Grade I, needs one more step to reach Grade II's D8).</summary>
        private static DieData AdvanceOneGrade(DieData from)
        {
            if (from == null) return null;
            int targetGrade = from.grade + 1;
            DieData current = from;
            int safety = 0; // guards against a misconfigured nextTier chain looping forever
            while (current.nextTier != null && current.grade < targetGrade && safety < 10)
            {
                current = current.nextTier;
                safety++;
            }
            return current;
        }

        private static DieData LowerGradeOf(DieData a, DieData b)
        {
            if (a == null) return b;
            if (b == null) return a;
            return a.grade <= b.grade ? a : b;
        }
    }
}

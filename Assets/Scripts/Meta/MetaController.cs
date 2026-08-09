using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Rollrate.Core;
using Rollrate.Data;

namespace Rollrate.Meta
{
    /// <summary>
    /// The Meta screen (design doc Section 7, Meta-progressione), reached
    /// only after Defeat. Shows a short run summary, then up to 3 randomly
    /// chosen dice from GameState.unlockedThisRun for the player to pick
    /// ONE to keep permanently - the rest are lost. Applies Fragmentation
    /// only AFTER the choice is made (or immediately if there was nothing
    /// to choose from), then continues directly to the Map - NOT back to
    /// the Main Menu (that's reserved for Final Victory's full reset, see
    /// MapController.ApplyRecalibration).
    /// </summary>
    public class MetaController : MonoBehaviour
    {
        [Header("Run Summary")]
        [SerializeField] private TextMeshProUGUI summaryText;

        [Header("Dice Choice")]
        [Tooltip("Prefab with a MetaDieCandidateUI component - one instantiated per candidate die (up to 3).")]
        [SerializeField] private MetaDieCandidateUI candidatePrefab;
        [SerializeField] private Transform candidatesContainer;
        [Tooltip("Shown instead of the candidates when there was nothing unlocked this run (0 candidates) - no choice to make.")]
        [SerializeField] private GameObject noCandidatesMessage;
        [SerializeField] private Button continueButton;

        [Header("Scene")]
        [SerializeField] private string mapSceneName = "MapScene";

        private readonly List<MetaDieCandidateUI> _spawnedCandidates = new List<MetaDieCandidateUI>();
        private DieInstance _selectedDie;

        private void Start()
        {
            var state = RunManager.Instance?.State;
            if (state == null)
            {
                Debug.LogError("[MetaController] RunManager.Instance is null - can't show the Meta screen.");
                return;
            }

            if (summaryText != null)
            {
                summaryText.text = $"Grado raggiunto: {state.currentEchelon}\nNemici sconfitti: {state.enemiesDefeatedThisRun}\nScrap accumulati: {state.scrap}";
            }

            SpawnCandidates(state);

            if (continueButton != null)
            {
                continueButton.onClick.AddListener(OnContinueClicked);
                continueButton.interactable = _spawnedCandidates.Count == 0; // must pick one first, if there's anything to pick
            }
        }

        /// <summary>Picks up to 3 random dice from unlockedThisRun and instantiates a candidate button per die.</summary>
        private void SpawnCandidates(GameState state)
        {
            var pool = new List<DieInstance>(state.unlockedThisRun);
            ShuffleList(pool);
            var chosen = pool.Take(3).ToList();

            bool hasCandidates = chosen.Count > 0;
            if (noCandidatesMessage != null) noCandidatesMessage.SetActive(!hasCandidates);

            if (!hasCandidates || candidatePrefab == null || candidatesContainer == null) return;

            foreach (var die in chosen)
            {
                var view = Instantiate(candidatePrefab, candidatesContainer);
                view.Setup(die, OnCandidateClicked);
                _spawnedCandidates.Add(view);
            }
        }

        private void OnCandidateClicked(DieInstance die)
        {
            _selectedDie = _selectedDie == die ? null : die; // reclicking the same one deselects it - same pattern as everywhere else (RollableDie's reroll-mark, Echo pending source)
            foreach (var view in _spawnedCandidates) view.SetSelected(view.Die == _selectedDie);
            if (continueButton != null) continueButton.interactable = _selectedDie != null || _spawnedCandidates.Count == 0;
        }

        private void OnContinueClicked()
        {
            RunManager.Instance?.FinalizeFragmentationAndContinue(_selectedDie);
            SceneManager.LoadScene(mapSceneName);
        }

        private static void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}

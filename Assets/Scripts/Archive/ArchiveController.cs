using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rollrate.Core;
using Rollrate.Data;

namespace Rollrate.Archive
{
    /// <summary>
    /// Nodo Archive (design doc Section 7): ONE of the 3 Tests is chosen
    /// at RANDOM on entry (not picked by the player). Press Roll to
    /// attempt it - the roll itself shows in Roll Text, the outcome
    /// (success/failure + consequence) in Result Text. Success shows the
    /// 3-way reward choice; failure applies that Test's own penalty
    /// directly - if it involves losing a die (Test di Tributo), the die
    /// is picked RANDOMLY, no player choice involved.
    /// </summary>
    public class ArchiveController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private ArchiveTestTable testTable;

        [Header("UI - Shared")]
        [SerializeField] private TextMeshProUGUI scrapText;
        [SerializeField] private TextMeshProUGUI hpText;

        [Header("UI - The randomly chosen Test")]
        [Tooltip("Describes whichever Test was randomly chosen on entry - not a menu of 3 to pick from.")]
        [SerializeField] private TextMeshProUGUI testInfoText;
        [SerializeField] private Button rollButton;
        [Tooltip("Shows just the roll itself (e.g. 'Core Die: 6').")]
        [SerializeField] private TextMeshProUGUI rollText;
        [Tooltip("Shows the outcome: success/failure and its consequence.")]
        [SerializeField] private TextMeshProUGUI resultText;

        [Header("UI - Reward Choice (Test success)")]
        [SerializeField] private GameObject rewardContainer;
        [SerializeField] private Button rewardScrapButton;
        [SerializeField] private TextMeshProUGUI rewardScrapText;
        [SerializeField] private Button rewardFreePurchaseButton;
        [SerializeField] private Button rewardFreeFusionButton;

        [Header("Scene")]
        [SerializeField] private string archiveSceneName = "ArchiveScene";
        [SerializeField] private string shopSceneName = "ShopScene";
        [SerializeField] private string furnaceSceneName = "FurnaceScene";
        [SerializeField] private Button leaveButton;

        private enum TestKind { Resonance, Tribute, Ambition }
        private TestKind _chosenTest; // rolled once, at Start - the player never picks this

        private void Start()
        {
            if (rollButton != null) rollButton.onClick.AddListener(AttemptTest);
            if (leaveButton != null) leaveButton.onClick.AddListener(() => NodeSceneLoader.ExitNode(archiveSceneName));

            if (rewardScrapButton != null) rewardScrapButton.onClick.AddListener(OnRewardScrapClicked);
            if (rewardFreePurchaseButton != null) rewardFreePurchaseButton.onClick.AddListener(OnRewardFreePurchaseClicked);
            if (rewardFreeFusionButton != null) rewardFreeFusionButton.onClick.AddListener(OnRewardFreeFusionClicked);

            if (rewardContainer != null) rewardContainer.SetActive(false);

            _chosenTest = (TestKind)Random.Range(0, 3);
            RefreshTestInfo();
            RefreshSharedUI();
        }

        /// <summary>Scrap/HP are always shown, same convention as Shop/Furnace - refreshed after every action that can change them.</summary>
        private void RefreshSharedUI()
        {
            var state = RunManager.Instance?.State;
            if (state == null) return;

            if (scrapText != null) scrapText.text = $"Scrap: {state.scrap}";
            if (hpText != null) hpText.text = $"HP: {state.currentHp}/{state.maxHp}";
        }

        private void RefreshTestInfo()
        {
            var state = RunManager.Instance?.State;
            if (state == null || testTable == null || testInfoText == null) return;

            int grade = state.currentEchelon;
            testInfoText.text = _chosenTest switch
            {
                TestKind.Resonance => $"Test di Risonanza: Core Die >= {testTable.GetResonanceThreshold(grade)} (fallimento: -{testTable.GetResonancePenalty(grade)} Scrap)",
                TestKind.Tribute => $"Test di Tributo: Somma Pool >= {testTable.GetTributeThreshold(grade)} (fallimento: perdi un dado a caso)",
                TestKind.Ambition => $"Test di Ambizione: Core + miglior dado Pool >= {testTable.GetAmbitionThreshold(grade)} (fallimento: -20% PV Massimi)",
                _ => string.Empty
            };
        }

        private void AttemptTest()
        {
            var state = RunManager.Instance?.State;
            if (state == null || testTable == null) return;

            if (rollButton != null) rollButton.interactable = false; // one attempt only

            int grade = state.currentEchelon;
            bool success;

            switch (_chosenTest)
            {
                case TestKind.Resonance:
                {
                    int roll = state.coreDie != null ? Random.Range(1, state.coreDie.faces + 1) : 0;
                    int threshold = testTable.GetResonanceThreshold(grade);
                    success = roll >= threshold;
                    if (rollText != null) rollText.text = $"Dado Core: {roll}";
                    if (resultText != null) resultText.text = success ? "SUCCESSO" : "Fallito";
                    if (!success)
                    {
                        int penalty = testTable.GetResonancePenalty(grade);
                        state.scrap = Mathf.Max(0, state.scrap - penalty);
                        if (resultText != null) resultText.text += $" (-{penalty} Scrap)";
                    }
                    break;
                }

                case TestKind.Tribute:
                {
                    int sum = state.dicePool.Sum(d => d.data != null ? Random.Range(1, d.data.faces + 1) : 0);
                    int threshold = testTable.GetTributeThreshold(grade);
                    success = sum >= threshold;
                    if (rollText != null) rollText.text = $"Somma Pool: {sum}";
                    if (resultText != null) resultText.text = success ? "SUCCESSO" : "Fallito";
                    if (!success)
                    {
                        // Design (aggiornato): il dado perso viene estratto A CASO, non scelto dal giocatore.
                        if (state.CanRemoveDie() && state.dicePool.Count > 0)
                        {
                            var lost = state.dicePool[Random.Range(0, state.dicePool.Count)];
                            state.RemoveDiePermanently(lost);
                            if (resultText != null) resultText.text += $" (perso: {lost.type} D{lost.data?.faces})";
                        }
                        else if (resultText != null)
                        {
                            resultText.text += " (pool troppo piccolo - nessun dado perso)";
                        }
                    }
                    break;
                }

                case TestKind.Ambition:
                {
                    int coreRoll = state.coreDie != null ? Random.Range(1, state.coreDie.faces + 1) : 0;
                    int bestPoolRoll = 0;
                    foreach (var d in state.dicePool)
                    {
                        if (d.data == null) continue;
                        int r = Random.Range(1, d.data.faces + 1);
                        if (r > bestPoolRoll) bestPoolRoll = r;
                    }
                    int total = coreRoll + bestPoolRoll;
                    int threshold = testTable.GetAmbitionThreshold(grade);
                    success = total >= threshold;
                    if (rollText != null) rollText.text = $"Core {coreRoll} + miglior dado {bestPoolRoll} = {total}";
                    if (resultText != null) resultText.text = success ? "SUCCESSO" : "Fallito";
                    if (!success)
                    {
                        int hpLoss = Mathf.CeilToInt(state.maxHp * 0.2f);
                        state.currentHp = Mathf.Max(0, state.currentHp - hpLoss);
                        if (resultText != null) resultText.text += $" (-{hpLoss} PV, 20% dei Massimi)";
                    }
                    break;
                }

                default:
                    return;
            }

            if (success) ShowRewardChoice();
            RefreshSharedUI();
        }

        /// <summary>Test success: 1 of 3 identical rewards (design doc Section 7, Scelta dell'Archive).</summary>
        private void ShowRewardChoice()
        {
            var state = RunManager.Instance?.State;
            if (state == null || testTable == null || rewardContainer == null) return;

            rewardContainer.SetActive(true);
            if (rewardScrapText != null) rewardScrapText.text = $"{testTable.GetRewardScrap(state.currentEchelon)} Scrap";
        }

        private void OnRewardScrapClicked()
        {
            var state = RunManager.Instance?.State;
            if (state == null || testTable == null) return;

            state.scrap += testTable.GetRewardScrap(state.currentEchelon);
            if (resultText != null) resultText.text += $" | Ricompensa: +{testTable.GetRewardScrap(state.currentEchelon)} Scrap.";
            if (rewardContainer != null) rewardContainer.SetActive(false);
            RefreshSharedUI();
        }

        private void OnRewardFreePurchaseClicked()
        {
            ArchiveRewardContext.FreeDiePurchasePending = true;
            NodeSceneLoader.ExitNode(archiveSceneName);
            NodeSceneLoader.EnterNode(shopSceneName);
        }

        private void OnRewardFreeFusionClicked()
        {
            ArchiveRewardContext.FreeFusionPending = true;
            NodeSceneLoader.ExitNode(archiveSceneName);
            NodeSceneLoader.EnterNode(furnaceSceneName);
        }
    }
}

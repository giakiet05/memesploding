using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Network.API.Models;
using Network.API.Services;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UIManager
{
    public class MatchHistoryManager : MonoBehaviour
    {
        private enum MatchHistoryFilter
        {
            All,
            Wins,
            Losses
        }

        [Header("List")]
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private MatchHistoryCardView cardTemplate;

        [Header("Analytics")]
        [SerializeField] private TextMeshProUGUI winsValueText;
        [SerializeField] private TextMeshProUGUI totalMatchesText;
        [SerializeField] private TextMeshProUGUI lossesValueText;
        [SerializeField] private TextMeshProUGUI totalMatchesSecondaryText;
        [SerializeField] private TextMeshProUGUI winRateValueText;
        [SerializeField] private TextMeshProUGUI winRateTotalText;

        [Header("Filters")]
        [SerializeField] private Button allFilterButton;
        [SerializeField] private Button winsFilterButton;
        [SerializeField] private Button lossesFilterButton;
        [SerializeField] private Image allFilterBackground;
        [SerializeField] private Image winsFilterBackground;
        [SerializeField] private Image lossesFilterBackground;

        [Header("State")]
        [SerializeField] private Color activeFilterColor = new(0.92f, 0.42f, 0.19f, 1f);
        [SerializeField] private Color inactiveFilterColor = new(1f, 1f, 1f, 0.14f);
        [SerializeField] private int pageSize = 50;

        private readonly List<MatchSummaryDto> _allMatches = new();
        private readonly List<MatchHistoryCardView> _spawnedCards = new();
        private MatchHistoryFilter _activeFilter = MatchHistoryFilter.All;
        private TextMeshProUGUI _emptyStateLabel;
        private bool _isLoading;

        private void Awake()
        {
            AutoBind();
            BindFilterButtons();
            PrepareTemplate();
        }

        private void OnEnable()
        {
            _ = LoadAsync();
        }

        private void OnDestroy()
        {
            UnbindFilterButtons();
        }

        private async Task LoadAsync()
        {
            if (_isLoading)
                return;

            var gameManager = GameManager.EnsureInstance();
            if (!gameManager.IsAuthenticated)
            {
                UniversalPopup.ShowError("Please log in to view match history.");
                return;
            }

            _isLoading = true;

            try
            {
                var response = await MatchService.Instance.GetMyMatchHistoryAsync(
                    new PaginationQueryDto { Page = 1, PageSize = Mathf.Max(1, pageSize) },
                    gameManager.AccessToken
                );

                if (response == null || !response.success)
                {
                    UniversalPopup.ShowError(string.IsNullOrWhiteSpace(response?.message)
                        ? "Unable to load match history."
                        : response.message);
                    ApplyMatches(Array.Empty<MatchSummaryDto>());
                    return;
                }

                ApplyMatches(response.data?.Items ?? new List<MatchSummaryDto>());
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MatchHistory] Load failed: {ex.Message}");
                UniversalPopup.ShowError("Unable to load match history.");
                ApplyMatches(Array.Empty<MatchSummaryDto>());
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void ApplyMatches(IEnumerable<MatchSummaryDto> matches)
        {
            _allMatches.Clear();
            _allMatches.AddRange(matches
                .Where(match => match != null)
                .OrderByDescending(match => match.StartedAt));

            UpdateAnalytics();
            RenderCurrentFilter();
        }

        private void UpdateAnalytics()
        {
            var totalMatches = _allMatches.Count;
            var wins = _allMatches.Count(IsWin);
            var losses = _allMatches.Count(IsLoss);
            var winRate = totalMatches <= 0 ? 0f : (wins / (float)totalMatches) * 100f;

            if (winsValueText != null)
                winsValueText.text = wins.ToString();

            if (totalMatchesText != null)
                totalMatchesText.text = $"/ {totalMatches}";

            if (lossesValueText != null)
                lossesValueText.text = losses.ToString();

            if (totalMatchesSecondaryText != null)
                totalMatchesSecondaryText.text = $"/ {totalMatches}";

            if (winRateValueText != null)
                winRateValueText.text = $"{Mathf.RoundToInt(winRate)}%";

            if (winRateTotalText != null)
                winRateTotalText.text = $"{wins}/{Mathf.Max(1, totalMatches)} wins";
        }

        private void RenderCurrentFilter()
        {
            SetFilterVisuals();
            ClearSpawnedCards();

            var filteredMatches = GetFilteredMatches().ToList();
            if (filteredMatches.Count == 0)
            {
                ShowEmptyState();
                return;
            }

            HideEmptyState();

            for (int index = 0; index < filteredMatches.Count; index++)
            {
                var view = index == 0 ? cardTemplate : Instantiate(cardTemplate, contentRoot);
                view.gameObject.SetActive(true);
                view.Bind(filteredMatches[index], GameManager.EnsureInstance().Player?.ID);

                if (index > 0)
                    _spawnedCards.Add(view);
            }
        }

        private IEnumerable<MatchSummaryDto> GetFilteredMatches()
        {
            return _activeFilter switch
            {
                MatchHistoryFilter.Wins => _allMatches.Where(IsWin),
                MatchHistoryFilter.Losses => _allMatches.Where(IsLoss),
                _ => _allMatches
            };
        }

        private void SetActiveFilter(MatchHistoryFilter filter)
        {
            if (_activeFilter == filter)
                return;

            _activeFilter = filter;
            RenderCurrentFilter();
        }

        private void ClearSpawnedCards()
        {
            foreach (var card in _spawnedCards)
            {
                if (card != null)
                    Destroy(card.gameObject);
            }

            _spawnedCards.Clear();

            if (cardTemplate != null)
                cardTemplate.gameObject.SetActive(false);
        }

        private void ShowEmptyState()
        {
            if (_emptyStateLabel == null)
                _emptyStateLabel = CreateEmptyStateLabel();

            if (_emptyStateLabel != null)
            {
                _emptyStateLabel.text = _activeFilter switch
                {
                    MatchHistoryFilter.Wins => "No wins yet.",
                    MatchHistoryFilter.Losses => "No losses found.",
                    _ => "No match history yet."
                };
                _emptyStateLabel.gameObject.SetActive(true);
            }
        }

        private void HideEmptyState()
        {
            if (_emptyStateLabel != null)
                _emptyStateLabel.gameObject.SetActive(false);
        }

        private TextMeshProUGUI CreateEmptyStateLabel()
        {
            if (contentRoot == null)
                return null;

            var go = new GameObject("EmptyState", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(contentRoot, false);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = 28f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.text = "No match history yet.";
            return text;
        }

        private void SetFilterVisuals()
        {
            SetFilterColor(allFilterBackground, _activeFilter == MatchHistoryFilter.All);
            SetFilterColor(winsFilterBackground, _activeFilter == MatchHistoryFilter.Wins);
            SetFilterColor(lossesFilterBackground, _activeFilter == MatchHistoryFilter.Losses);
        }

        private void SetFilterColor(Image target, bool active)
        {
            if (target != null)
                target.color = active ? activeFilterColor : inactiveFilterColor;
        }

        private void PrepareTemplate()
        {
            if (cardTemplate != null)
                cardTemplate.gameObject.SetActive(false);
        }

        private void BindFilterButtons()
        {
            if (allFilterButton != null)
                allFilterButton.onClick.AddListener(HandleAllFilterClicked);

            if (winsFilterButton != null)
                winsFilterButton.onClick.AddListener(HandleWinsFilterClicked);

            if (lossesFilterButton != null)
                lossesFilterButton.onClick.AddListener(HandleLossesFilterClicked);
        }

        private void UnbindFilterButtons()
        {
            if (allFilterButton != null)
                allFilterButton.onClick.RemoveListener(HandleAllFilterClicked);

            if (winsFilterButton != null)
                winsFilterButton.onClick.RemoveListener(HandleWinsFilterClicked);

            if (lossesFilterButton != null)
                lossesFilterButton.onClick.RemoveListener(HandleLossesFilterClicked);
        }

        private void HandleAllFilterClicked() => SetActiveFilter(MatchHistoryFilter.All);
        private void HandleWinsFilterClicked() => SetActiveFilter(MatchHistoryFilter.Wins);
        private void HandleLossesFilterClicked() => SetActiveFilter(MatchHistoryFilter.Losses);

        private void AutoBind()
        {
            contentRoot ??= transform.Find("Scroll View/Viewport/Content") as RectTransform;
            cardTemplate ??= contentRoot != null ? contentRoot.GetComponentInChildren<MatchHistoryCardView>(true) : null;

            winsValueText ??= transform.Find("AnalyticPanel/WinTotal Box/WinTotal")?.GetComponent<TextMeshProUGUI>();
            totalMatchesText ??= transform.Find("AnalyticPanel/WinTotal Box/Total")?.GetComponent<TextMeshProUGUI>();
            lossesValueText ??= transform.Find("AnalyticPanel/LoosesTotal Box/LoosesTotal")?.GetComponent<TextMeshProUGUI>();
            totalMatchesSecondaryText ??= transform.Find("AnalyticPanel/LoosesTotal Box/Total")?.GetComponent<TextMeshProUGUI>();
            winRateValueText ??= transform.Find("AnalyticPanel/WinrateBox/WinTotal")?.GetComponent<TextMeshProUGUI>();
            winRateTotalText ??= transform.Find("AnalyticPanel/WinrateBox/Total")?.GetComponent<TextMeshProUGUI>();

            allFilterButton ??= transform.Find("FilterPanel/BG/AllFilter")?.GetComponent<Button>();
            winsFilterButton ??= transform.Find("FilterPanel/BG/WinFilter")?.GetComponent<Button>();
            lossesFilterButton ??= transform.Find("FilterPanel/BG/LossesFilter")?.GetComponent<Button>();

            allFilterBackground ??= allFilterButton != null ? allFilterButton.GetComponent<Image>() : null;
            winsFilterBackground ??= winsFilterButton != null ? winsFilterButton.GetComponent<Image>() : null;
            lossesFilterBackground ??= lossesFilterButton != null ? lossesFilterButton.GetComponent<Image>() : null;
        }

        private static bool IsWin(MatchSummaryDto match)
        {
            return match?.FinalRank == 1;
        }

        private static bool IsLoss(MatchSummaryDto match)
        {
            return match?.FinalRank.HasValue == true && match.FinalRank.Value > 1;
        }
    }
}

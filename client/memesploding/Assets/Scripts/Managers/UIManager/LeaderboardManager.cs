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
    public class LeaderboardManager : MonoBehaviour
    {
        private enum LeaderboardFilter
        {
            Global,
            Friends
        }

        [Header("List")]
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private RankingCardView cardTemplate;
        [SerializeField] private RankingCardView selfCard;

        [Header("Filters")]
        [SerializeField] private Button globalFilterButton;
        [SerializeField] private Button friendsFilterButton;
        [SerializeField] private Image globalFilterBackground;
        [SerializeField] private Image friendsFilterBackground;

        [Header("State")]
        [SerializeField] private Color activeFilterColor = new(0.92f, 0.42f, 0.19f, 1f);
        [SerializeField] private Color inactiveFilterColor = new(1f, 1f, 1f, 0.14f);
        [SerializeField] private int pageSize = 50;

        private readonly List<UserProfileDto> _allUsers = new();
        private readonly List<RankingCardView> _spawnedCards = new();
        private LeaderboardFilter _activeFilter = LeaderboardFilter.Global;
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
                UniversalPopup.ShowError("Please log in to view the leaderboard.");
                return;
            }

            _isLoading = true;

            try
            {
                var leaderboardResponse = await UserService.Instance.GetLeaderboardAsync(
                    new PaginationQueryDto { Page = 1, PageSize = Mathf.Max(1, pageSize) },
                    gameManager.AccessToken
                );

                if (leaderboardResponse == null || !leaderboardResponse.success)
                {
                    UniversalPopup.ShowError(string.IsNullOrWhiteSpace(leaderboardResponse?.message)
                        ? "Unable to load leaderboard."
                        : leaderboardResponse.message);
                    ApplyUsers(Array.Empty<UserProfileDto>());
                    return;
                }

                var statsResponse = await UserService.Instance.GetMyStatsAsync(gameManager.AccessToken);
                ApplyUsers(leaderboardResponse.data?.Items ?? new List<UserProfileDto>());
                ApplySelf(statsResponse?.data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Leaderboard] Load failed: {ex.Message}");
                UniversalPopup.ShowError("Unable to load leaderboard.");
                ApplyUsers(Array.Empty<UserProfileDto>());
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void ApplyUsers(IEnumerable<UserProfileDto> users)
        {
            _allUsers.Clear();
            _allUsers.AddRange(users.Where(user => user != null));
            RenderCurrentFilter();
        }

        private void ApplySelf(UserStatsDto stats)
        {
            if (selfCard == null)
                return;

            var player = GameManager.EnsureInstance().Player;
            var rank = stats?.GlobalRank ?? 0;
            selfCard.gameObject.SetActive(true);
            selfCard.BindSelf(player, rank);
        }

        private void RenderCurrentFilter()
        {
            SetFilterVisuals();
            ClearSpawnedCards();

            var filteredUsers = GetFilteredUsers().ToList();
            if (filteredUsers.Count == 0)
            {
                ShowEmptyState();
                return;
            }

            HideEmptyState();

            if (cardTemplate == null || contentRoot == null)
                return;

            foreach (var user in filteredUsers)
            {
                var view = Instantiate(cardTemplate, contentRoot);
                view.gameObject.SetActive(true);
                view.Bind(user, GetGlobalRank(user));
                _spawnedCards.Add(view);
            }
        }

        private IEnumerable<UserProfileDto> GetFilteredUsers()
        {
            return _activeFilter switch
            {
                LeaderboardFilter.Friends => _allUsers.Where(IsFriend),
                _ => _allUsers
            };
        }

        private static bool IsFriend(UserProfileDto user)
        {
            return user != null &&
                   string.Equals(user.Relationship, "Accepted", StringComparison.OrdinalIgnoreCase);
        }

        private int GetGlobalRank(UserProfileDto user)
        {
            if (user == null)
                return 0;

            var index = _allUsers.IndexOf(user);
            return index >= 0 ? index + 1 : 0;
        }

        private void SetActiveFilter(LeaderboardFilter filter)
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
        }

        private void ShowEmptyState()
        {
            if (_emptyStateLabel == null)
                _emptyStateLabel = CreateEmptyStateLabel();

            if (_emptyStateLabel != null)
            {
                _emptyStateLabel.text = _activeFilter == LeaderboardFilter.Friends
                    ? "No friends on the leaderboard yet."
                    : "No leaderboard data yet.";
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
            text.text = "No leaderboard data yet.";
            return text;
        }

        private void SetFilterVisuals()
        {
            SetFilterColor(globalFilterBackground, _activeFilter == LeaderboardFilter.Global);
            SetFilterColor(friendsFilterBackground, _activeFilter == LeaderboardFilter.Friends);
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
            if (globalFilterButton != null)
                globalFilterButton.onClick.AddListener(HandleGlobalFilterClicked);

            if (friendsFilterButton != null)
                friendsFilterButton.onClick.AddListener(HandleFriendsFilterClicked);
        }

        private void UnbindFilterButtons()
        {
            if (globalFilterButton != null)
                globalFilterButton.onClick.RemoveListener(HandleGlobalFilterClicked);

            if (friendsFilterButton != null)
                friendsFilterButton.onClick.RemoveListener(HandleFriendsFilterClicked);
        }

        private void HandleGlobalFilterClicked() => SetActiveFilter(LeaderboardFilter.Global);
        private void HandleFriendsFilterClicked() => SetActiveFilter(LeaderboardFilter.Friends);

        private Transform GetLeaderboardRoot()
        {
            if (transform.Find("Global") != null)
                return transform;

            var popup = GameObject.Find("Leaderboard Popup");
            if (popup != null)
                return popup.transform;

            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                var t = canvas.transform.Find("Leaderboard Popup");
                if (t != null)
                    return t;
            }

            return transform;
        }

        private void AutoBind()
        {
            var root = GetLeaderboardRoot();

            contentRoot ??= root.Find("Global/RankingBoard/Panel/Scroll View/Viewport/Content") as RectTransform;

            if (cardTemplate == null)
            {
                var templateTransform = root.Find("Global/RankingBoard/Panel/Content/RankingCard");
                if (templateTransform != null)
                    cardTemplate = templateTransform.GetComponent<RankingCardView>() ??
                                   templateTransform.gameObject.AddComponent<RankingCardView>();
            }

            if (selfCard == null)
            {
                var selfTransform = root.Find("Global/RankingCard(You)");
                if (selfTransform != null)
                    selfCard = selfTransform.GetComponent<RankingCardView>() ??
                               selfTransform.gameObject.AddComponent<RankingCardView>();
            }

            globalFilterButton ??= root.Find("Global/Header/Button")?.GetComponent<Button>();
            friendsFilterButton ??= root.Find("Global/Header/Button (1)")?.GetComponent<Button>();

            globalFilterBackground ??= globalFilterButton != null ? globalFilterButton.GetComponent<Image>() : null;
            friendsFilterBackground ??= friendsFilterButton != null ? friendsFilterButton.GetComponent<Image>() : null;
        }
    }
}

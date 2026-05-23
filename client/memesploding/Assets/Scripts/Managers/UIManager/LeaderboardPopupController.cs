using System;
using System.Collections.Generic;
using System.Linq;
using Managers;
using Network.API.Models;
using Network.API.Services;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UIManager
{
    public class LeaderboardPopupController : MonoBehaviour
    {
        private enum LeaderboardFilter
        {
            Global,
            Friends
        }

        [Header("Scene References (optional; auto-bound if empty)")]
        [SerializeField] private Popup popupRoot;
        [SerializeField] private Button globalFilterButton;
        [SerializeField] private Button friendsFilterButton;
        [SerializeField] private Image globalFilterBackground;
        [SerializeField] private Image friendsFilterBackground;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private LeaderboardCurrentUserView currentUserView;

        [Header("Prefabs")]
        [SerializeField] private LeaderboardEntryView rowPrefab;

        [Header("Visuals")]
        [SerializeField] private Color activeFilterColor = new(0.92f, 0.42f, 0.19f, 1f);
        [SerializeField] private Color inactiveFilterColor = new(1f, 1f, 1f, 0.14f);
        [SerializeField] private int pageSize = 50;

        private readonly List<LeaderboardEntryView> _spawnedRows = new();
        private readonly List<UserProfileDto> _globalEntries = new();
        private LeaderboardFilter _activeFilter = LeaderboardFilter.Global;
        private bool _isLoading;

        private void Awake()
        {
            AutoBind();
            BindButtons();
        }

        private void OnDestroy()
        {
            if (globalFilterButton != null)
                globalFilterButton.onClick.RemoveListener(ShowGlobal);

            if (friendsFilterButton != null)
                friendsFilterButton.onClick.RemoveListener(ShowFriends);
        }

        public async void Open()
        {
            AutoBind();

            if (popupRoot == null)
            {
                UniversalPopup.ShowError("Leaderboard popup is missing.");
                return;
            }

            if (_isLoading)
                return;

            _activeFilter = LeaderboardFilter.Global;
            UpdateFilterVisuals();
            await LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            var gameManager = GameManager.EnsureInstance();
            if (!gameManager.IsAuthenticated)
            {
                UniversalPopup.ShowError("Please log in to view the leaderboard.");
                return;
            }

            _isLoading = true;

            try
            {
                var response = await UserService.Instance.GetLeaderboardAsync(
                    new PaginationQueryDto { Page = 1, PageSize = Mathf.Max(1, pageSize) },
                    gameManager.AccessToken
                );

                if (response?.success != true || response.data?.Items == null)
                {
                    UniversalPopup.ShowError(string.IsNullOrWhiteSpace(response?.message)
                        ? "Unable to load leaderboard."
                        : response.message);
                    ApplyEntries(Array.Empty<UserProfileDto>());
                    return;
                }

                ApplyEntries(response.data.Items);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LeaderboardPopupController] Load failed: {ex.Message}", this);
                UniversalPopup.ShowError("Unable to load leaderboard.");
                ApplyEntries(Array.Empty<UserProfileDto>());
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void ApplyEntries(IEnumerable<UserProfileDto> entries)
        {
            _globalEntries.Clear();
            _globalEntries.AddRange(entries
                .Where(entry => entry != null)
                .OrderByDescending(entry => entry.Score)
                .ThenByDescending(entry => entry.Level)
                .ThenBy(entry => entry.Username));

            Render();
        }

        private void Render()
        {
            UpdateFilterVisuals();
            ClearRows();

            var gameManager = GameManager.EnsureInstance();
            var currentUserId = gameManager.Player?.ID;
            var filtered = GetFilteredEntries().ToList();

            var selfEntry = !string.IsNullOrWhiteSpace(currentUserId)
                ? _globalEntries.Find(entry => string.Equals(entry.Id, currentUserId, StringComparison.OrdinalIgnoreCase))
                : null;
            var selfRank = selfEntry == null ? 0 : _globalEntries.FindIndex(entry => string.Equals(entry.Id, selfEntry.Id, StringComparison.OrdinalIgnoreCase)) + 1;

            if (currentUserView != null)
                currentUserView.Bind(selfEntry, selfRank);

            for (int i = 0; i < filtered.Count; i++)
            {
                if (rowPrefab == null || contentRoot == null)
                    break;

                var row = Instantiate(rowPrefab, contentRoot);
                row.gameObject.SetActive(true);
                row.Bind(filtered[i], ResolveRank(filtered[i]));
                _spawnedRows.Add(row);
            }
        }

        private IEnumerable<UserProfileDto> GetFilteredEntries()
        {
            if (_activeFilter == LeaderboardFilter.Friends)
            {
                return _globalEntries.Where(entry =>
                    string.Equals(entry.Relationship, "Friend", StringComparison.OrdinalIgnoreCase));
            }

            return _globalEntries;
        }

        private int ResolveRank(UserProfileDto entry)
        {
            if (entry == null)
                return 0;

            return _globalEntries.FindIndex(candidate => string.Equals(candidate.Id, entry.Id, StringComparison.OrdinalIgnoreCase)) + 1;
        }

        private void ShowGlobal()
        {
            _activeFilter = LeaderboardFilter.Global;
            Render();
        }

        private void ShowFriends()
        {
            _activeFilter = LeaderboardFilter.Friends;
            Render();
        }

        private void ClearRows()
        {
            foreach (var row in _spawnedRows)
            {
                if (row != null)
                    Destroy(row.gameObject);
            }

            _spawnedRows.Clear();
        }

        private void BindButtons()
        {
            if (globalFilterButton != null)
            {
                globalFilterButton.onClick.RemoveListener(ShowGlobal);
                globalFilterButton.onClick.AddListener(ShowGlobal);
            }

            if (friendsFilterButton != null)
            {
                friendsFilterButton.onClick.RemoveListener(ShowFriends);
                friendsFilterButton.onClick.AddListener(ShowFriends);
            }
        }

        private void UpdateFilterVisuals()
        {
            if (globalFilterBackground != null)
                globalFilterBackground.color = _activeFilter == LeaderboardFilter.Global ? activeFilterColor : inactiveFilterColor;

            if (friendsFilterBackground != null)
                friendsFilterBackground.color = _activeFilter == LeaderboardFilter.Friends ? activeFilterColor : inactiveFilterColor;
        }

        private void AutoBind()
        {
            var canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            var root = canvas != null ? canvas.transform : null;
            if (root == null)
                return;

            popupRoot ??= root.Find("Leaderboard Popup")?.GetComponent<Popup>();
            globalFilterButton ??= root.Find("Leaderboard Popup/Global/Header/Button")?.GetComponent<Button>();
            friendsFilterButton ??= root.Find("Leaderboard Popup/Global/Header/Button (1)")?.GetComponent<Button>();
            globalFilterBackground ??= globalFilterButton != null ? globalFilterButton.GetComponent<Image>() : null;
            friendsFilterBackground ??= friendsFilterButton != null ? friendsFilterButton.GetComponent<Image>() : null;
            contentRoot ??= root.Find("Leaderboard Popup/Global/RankingBoard/Panel/Content") as RectTransform;
            currentUserView ??= root.Find("Leaderboard Popup/Global/RankingCard(You)")?.GetComponent<LeaderboardCurrentUserView>();
        }
    }
}

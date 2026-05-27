using System;
using System.Collections.Generic;
using Network.API.Models;
using Network.API.Services;
using UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Managers.UIManager
{
    public class MainMenuManager : MonoBehaviour
    {
        public static MainMenuManager Instance;
        [Header("Popups")]
        [SerializeField] private Popup quickMatchBackdrop;
        [SerializeField] private Popup settingPopupBackdrop;
        [SerializeField] private Popup playNowBackdrop;
        [SerializeField] private Popup roomInvitationBackdrop;
        [SerializeField] private Popup leaderboardPopup;

        [Header("Panels")]
        [SerializeField] private Popup friendsPannel;
        [SerializeField] private Popup notificationPannel;

        [Header("Buttons")]
        [SerializeField] private Button playTestButton;

        private readonly List<Popup[]> _popupHistory = new();
        private bool _isStartingPlayTest;

        private void Awake()
        {
            AutoBind();

            if (Instance != null && Instance != this)
                Destroy(gameObject);
            else
                Instance = this;
        }

        private void OnEnable()
        {
            playTestButton?.onClick.AddListener(HandlePlayTestClicked);
        }

        private void OnDisable()
        {
            playTestButton?.onClick.RemoveListener(HandlePlayTestClicked);
        }

        public void HideAllPopups()
        {
            Hide(quickMatchBackdrop);
            Hide(settingPopupBackdrop);
            Hide(playNowBackdrop);
            Hide(roomInvitationBackdrop);
            Hide(leaderboardPopup);
            Hide(friendsPannel);
            Hide(notificationPannel);
            _popupHistory.Clear();
        }

        public void ClosePreviousPopup()
        {
            if (_popupHistory.Count == 0)
                return;

            var previous = _popupHistory[^1];
            _popupHistory.RemoveAt(_popupHistory.Count - 1);

            foreach (var popup in previous)
            {
                Hide(popup);
            }
        }

        public void OpenQuickMatch()
        {
            ShowGroup(quickMatchBackdrop);
        }

        public void OpenSettings()
        {
            ShowGroup(settingPopupBackdrop);
        }

        public void OpenPlayNowBackdrop()
        {
            ShowGroup(playNowBackdrop);
        }

        public void OpenFriends()
        {
            ShowGroup(friendsPannel);
        }

        public void OpenNotifications()
        {
            ShowGroup(notificationPannel);
        }

        public void OpenRoomInvitation()
        {
            ShowGroup(roomInvitationBackdrop);
        }

        public void OpenLeaderboard()
        {
            ShowGroup(leaderboardPopup);
            var controller = FindFirstObjectByType<LeaderboardPopupController>(FindObjectsInactive.Include);
            if (controller != null)
                controller.Open();
        }

        private async void HandlePlayTestClicked()
        {
            if (_isStartingPlayTest)
                return;

            var gameManager = GameManager.EnsureInstance();
            if (!gameManager.IsAuthenticated)
            {
                UniversalPopup.ShowError("Please log in before starting a bot test match.");
                return;
            }

            _isStartingPlayTest = true;
            if (playTestButton != null)
                playTestButton.interactable = false;

            try
            {
                var response = await TestMatchService.Instance.StartBotMatchAsync(gameManager.AccessToken);
                if (response?.success != true || response.data == null)
                {
                    UniversalPopup.ShowError(string.IsNullOrWhiteSpace(response?.message)
                        ? "Unable to start bot test match."
                        : response.message);
                    return;
                }

                RoomManager.EnsureInstance().SetCurrentRoom(BuildRoomDetail(response.data, gameManager.Player?.ID), "Bot Test Match");
                UniversalPopup.ShowSuccess(string.IsNullOrWhiteSpace(response.message)
                    ? "Bot test match started."
                    : response.message);
                LoadGameplay();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MainMenu] Bot test match failed: {ex.Message}");
                UniversalPopup.ShowError("Unable to start bot test match.");
            }
            finally
            {
                _isStartingPlayTest = false;
                if (playTestButton != null)
                    playTestButton.interactable = true;
            }
        }

        private void ShowGroup(params Popup[] popups)
        {
            if (popups == null || popups.Length == 0)
                return;

            var shown = new List<Popup>();
            foreach (var popup in popups)
            {
                if (popup == null)
                    continue;

                popup.Show();
                shown.Add(popup);
            }

            if (shown.Count > 0)
                _popupHistory.Add(shown.ToArray());
        }

        private void Show(Popup popup)
        {
            if (popup == null)
                return;

            popup.Show();
        }

        private void Hide(Popup popup)
        {
            if (popup == null)
                return;

            popup.Hide();
        }

        private void AutoBind()
        {
            playTestButton ??= FindByPath("Canvas/PlayTestButton")?.GetComponent<Button>();
        }

        private static RoomDetailDto BuildRoomDetail(BotTestMatchDto match, string localUserId)
        {
            var participants = new List<RoomParticipantDto>();
            if (match?.Participants != null)
            {
                foreach (var participant in match.Participants)
                {
                    if (participant == null)
                        continue;

                    participants.Add(new RoomParticipantDto
                    {
                        UserId = participant.UserId,
                        Nickname = participant.Nickname,
                        AvatarUrl = participant.AvatarUrl,
                        Role = participant.Role,
                        IsReady = participant.IsReady
                    });
                }
            }

            return new RoomDetailDto
            {
                Code = match?.RoomCode ?? string.Empty,
                HostId = localUserId ?? string.Empty,
                Status = "playing",
                IsPublic = false,
                Settings = new RoomSettingsDto
                {
                    MaxPlayers = participants.Count
                },
                CardSets = new List<CardSetInfoDto>(),
                CurrentParticipants = participants,
                Connection = new RoomConnectionDto
                {
                    WsUrl = match?.Connection?.WsUrl ?? string.Empty,
                    WsAccessToken = match?.Connection?.WsAccessToken ?? string.Empty
                }
            };
        }

        private static void LoadGameplay()
        {
            if (NavigationManager.Instance != null)
            {
                NavigationManager.Instance.LoadGameplay();
                return;
            }

            SceneManager.LoadScene("Loading");
        }

        private static Transform FindByPath(string path)
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
            {
                var result = FindByPath(root.transform, path);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static Transform FindByPath(Transform current, string path)
        {
            if (current == null)
                return null;

            var currentPath = current.name;
            var parent = current.parent;
            while (parent != null)
            {
                currentPath = $"{parent.name}/{currentPath}";
                parent = parent.parent;
            }

            if (string.Equals(currentPath, path, StringComparison.Ordinal))
                return current;

            foreach (Transform child in current)
            {
                var result = FindByPath(child, path);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}

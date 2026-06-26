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
        public Popup SettingPopupBackdrop => settingPopupBackdrop;
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
            EnsureInputModule();
            AutoBind();

            if (Instance != null && Instance != this)
                Destroy(gameObject);
            else
                Instance = this;
        }

        private void Start()
        {
            AutoBindPopups();
            BindMenuButtons();
        }

        private void OnEnable()
        {
            AutoBindPopups();
            BindMenuButtons();
        }

        private void OnDisable()
        {
        }

        private void AutoBindPopups()
        {
            var popups = FindObjectsByType<Popup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var p in popups)
            {
                if (p == null) continue;

                var name = p.name.ToLower();
                if (name.Contains("playnow") || name.Contains("play now") || name.Contains("play_now"))
                {
                    if (playNowBackdrop == null) playNowBackdrop = p;
                }
                else if (name.Contains("setting") && (name.Contains("backdrop") || name.Contains("popup")))
                {
                    if (settingPopupBackdrop == null) settingPopupBackdrop = p;
                }
                else if (name.Contains("leaderboard"))
                {
                    if (leaderboardPopup == null) leaderboardPopup = p;
                }
                else if (name.Contains("quickmatch") || name.Contains("quick match"))
                {
                    if (quickMatchBackdrop == null) quickMatchBackdrop = p;
                }
                else if (name.Contains("invitation") || name.Contains("invite"))
                {
                    if (roomInvitationBackdrop == null) roomInvitationBackdrop = p;
                }
                else if (name.Contains("friends"))
                {
                    if (friendsPannel == null) friendsPannel = p;
                }
                else if (name.Contains("notification"))
                {
                    if (notificationPannel == null) notificationPannel = p;
                }
            }
        }

        private void BindMenuButtons()
        {
            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var btn in buttons)
            {
                if (btn == null) continue;

                var name = btn.name.ToLower();
                if (name.Contains("playnow") || name.Contains("play now") || name.Contains("play_now"))
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(OpenPlayNowBackdrop);
                }
                else if (name.Contains("setting button") || name.Contains("settingbutton") || name == "settings" || name == "setting")
                {
                    btn.onClick = new Button.ButtonClickedEvent();
                    btn.onClick.AddListener(OpenSettings);

                    // Center settings icon
                    var img = btn.transform.Find("Image") ?? btn.transform.Find("Icon") ?? (btn.transform.childCount > 0 ? btn.transform.GetChild(0) : null);
                    if (img != null)
                    {
                        var imgRect = img.GetComponent<RectTransform>();
                        if (imgRect != null)
                        {
                            imgRect.anchoredPosition = Vector2.zero;
                        }
                    }
                }
                else if (name.Contains("leaderboard") || name.Contains("leader board"))
                {
                    btn.gameObject.SetActive(false);
                }
                else if (name.Contains("playtest") || name.Contains("play test"))
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(HandlePlayTestClicked);
                }
            }
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
                    if (response != null && response.errorCode == "PLAYER_ALREADY_IN_ROOM" && response.details != null)
                    {
                        var oldRoomId = response.details.Value<string>("roomId");
                        if (!string.IsNullOrWhiteSpace(oldRoomId))
                        {
                            UniversalPopup.ShowInfo("Bạn đang ở trong phòng khác. Đang rời phòng cũ để chơi...");
                            try
                            {
                                await Network.Websocket.AppRoomWebsocketClient.ForceLeaveRoomAsync(gameManager.AccessToken, oldRoomId);
                                RoomManager.EnsureInstance().ClearCurrentRoom();
                                
                                // Retry bot match
                                response = await TestMatchService.Instance.StartBotMatchAsync(gameManager.AccessToken);
                            }
                            catch (Exception wsEx)
                            {
                                Debug.LogWarning($"[MainMenu] Failed to auto-leave old room {oldRoomId} for bot match: {wsEx.Message}");
                            }
                        }
                    }
                }

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

        private Transform FindByPath(string path)
        {
            var roots = gameObject.scene.GetRootGameObjects();
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

        private void EnsureInputModule()
        {
            var eventSystem = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem != null)
            {
                var legacyInput = eventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                if (legacyInput != null)
                {
                    DestroyImmediate(legacyInput);
                    eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                    Debug.Log($"[InputHelper] Successfully upgraded EventSystem in scene {gameObject.scene.name} to InputSystemUIInputModule.");
                }
            }
        }
    }
}

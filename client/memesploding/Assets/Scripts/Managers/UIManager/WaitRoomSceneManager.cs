using Network.API.Models;
using Network.API.Services;
using Network.Websocket;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Managers.UIManager
{
    public class WaitRoomSceneManager : MonoBehaviour
    {
        private const string MainMenuSceneName = "MainMenu";
        private const string HostRoleText = "YOU ARE THE HOST";
        private const string HostDescriptionText = "Start the game when everyone is ready.";
        private const string PlayerRoleText = "YOU ARE A PLAYER";
        private const string PlayerDescriptionText = "Mark ready and wait for the host to start the game.";
        private const string StartButtonText = "START GAME";
        private const string ReadyButtonText = "READY";
        private const string CancelReadyButtonText = "CANCEL READY";
        private const string InviteButtonText = "COPY ROOM CODE";

        [SerializeField] private TextMeshProUGUI roomCodeText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private TextMeshProUGUI roleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI inviteButtonText;
        [SerializeField] private TextMeshProUGUI actionButtonText;
        [SerializeField] private Button headerBackButton;
        [SerializeField] private Button copyButton;
        [SerializeField] private Button inviteButton;
        [SerializeField] private Button actionButton;
        [SerializeField] private Transform memberContentRoot;
        [SerializeField] private WaitingRoomMemberItemView memberItemTemplate;

        private const float RoomRefreshIntervalSeconds = 15f;

        private readonly List<WaitingRoomMemberItemView> _spawnedItems = new List<WaitingRoomMemberItemView>();
        private AppRoomWebsocketClient _roomSocket;
        private bool _isLoading;
        private bool _isTransitioningToGameplay;
        private bool _isRefreshing;
        private float _timeSinceLastRefresh;

        private void Awake()
        {
            EnsureInputModule();
            AutoBind();
            _roomSocket = AppRoomWebsocketClient.Instance;

            if (memberItemTemplate != null)
                memberItemTemplate.gameObject.SetActive(false);
        }

        private async void OnEnable()
        {
            headerBackButton?.onClick.AddListener(HandleBackClicked);
            copyButton?.onClick.AddListener(HandleCopyClicked);
            inviteButton?.onClick.AddListener(HandleInviteClicked);
            actionButton?.onClick.AddListener(HandleActionClicked);

            _timeSinceLastRefresh = 0f;
            _isRefreshing = false;
            SubscribeRoomSocket();
            Render();
            await InitializeAsync();
        }

        private void Update()
        {
            if (_isTransitioningToGameplay || _isLoading || _isRefreshing)
                return;

            _timeSinceLastRefresh += Time.deltaTime;
            if (_timeSinceLastRefresh >= RoomRefreshIntervalSeconds)
            {
                _timeSinceLastRefresh = 0f;
                var gameManager = GameManager.EnsureInstance();
                var roomManager = RoomManager.EnsureInstance();
                if (gameManager.IsAuthenticated && roomManager.HasRoom)
                    DoPeriodicRefresh(gameManager.AccessToken, roomManager.GetRoomCode());
            }
        }

        private async void DoPeriodicRefresh(string accessToken, string roomCode)
        {
            _isRefreshing = true;
            try
            {
                await RefreshRoomAsync(accessToken, roomCode);
                if (!_isTransitioningToGameplay)
                    Render();
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private async void OnDisable()
        {
            headerBackButton?.onClick.RemoveListener(HandleBackClicked);
            copyButton?.onClick.RemoveListener(HandleCopyClicked);
            inviteButton?.onClick.RemoveListener(HandleInviteClicked);
            actionButton?.onClick.RemoveListener(HandleActionClicked);

            UnsubscribeRoomSocket();

            if (_roomSocket != null)
            {
                try
                {
                    await _roomSocket.DisconnectAsync();
                }
                catch
                {
                }
            }
        }

        private async Task InitializeAsync()
        {
            var gameManager = GameManager.EnsureInstance();
            var roomManager = RoomManager.EnsureInstance();
            if (!gameManager.IsAuthenticated)
            {
                UniversalPopup.ShowError("You need to log in first.");
                LoadMainMenu();
                return;
            }

            if (!roomManager.HasRoom)
            {
                UniversalPopup.ShowError("No room information found.");
                LoadMainMenu();
                return;
            }

            var roomCode = roomManager.GetRoomCode();

            // Always start with a clean connection — avoids stuck Connected state from
            // a previous session whose async OnDisable.DisconnectAsync() lost the race.
            try { await _roomSocket.DisconnectAsync(); } catch { }

            // Connect FIRST so no member-join events are missed while waiting for the REST refresh.
            try
            {
                await _roomSocket.ConnectAsync(gameManager.AccessToken, roomCode);
            }
            catch (Exception ex)
            {
                UniversalPopup.ShowError(string.IsNullOrWhiteSpace(ex.Message) ? "Unable to connect to the room." : ex.Message);
            }

            // Refresh after connecting to catch any joins that happened before we established the WS connection.
            await RefreshRoomAsync(gameManager.AccessToken, roomCode);

            Render();
        }

        private async Task RefreshRoomAsync(string accessToken, string roomCode)
        {
            try
            {
                var response = await RoomService.Instance.GetRoomByCodeAsync(roomCode, accessToken);
                if (response != null && response.success && response.data != null)
                {
                    var roomManager = RoomManager.EnsureInstance();
                    roomManager.SetCurrentRoom(response.data, roomManager.GetDisplayRoomName());
                }
                else if (!string.IsNullOrWhiteSpace(response?.message))
                {
                    UniversalPopup.ShowError(response.message);
                }
            }
            catch (Exception ex)
            {
                UniversalPopup.ShowError(string.IsNullOrWhiteSpace(ex.Message) ? "Unable to load room information." : ex.Message);
            }
        }

        private void Render()
        {
            var roomManager = RoomManager.EnsureInstance();
            if (!roomManager.HasRoom)
                return;

            if (roomCodeText != null)
                roomCodeText.text = roomManager.GetRoomCode();

            if (playerCountText != null)
                playerCountText.text = $"{roomManager.GetParticipantCount()}/{roomManager.GetMaxPlayers()}";

            if (inviteButtonText != null)
                inviteButtonText.text = InviteButtonText;

            RenderActionPanel(roomManager);
            RenderMembers(roomManager);
        }

        private void RenderActionPanel(RoomManager roomManager)
        {
            var isHost = roomManager.IsLocalPlayerHost();

            if (roleText != null)
                roleText.text = isHost ? HostRoleText : PlayerRoleText;

            if (descriptionText != null)
                descriptionText.text = isHost ? HostDescriptionText : PlayerDescriptionText;

            if (actionButtonText != null)
                actionButtonText.text = isHost
                    ? StartButtonText
                    : roomManager.IsLocalPlayerReady() ? CancelReadyButtonText : ReadyButtonText;

            if (actionButton != null)
                actionButton.interactable = !_isLoading && (isHost ? roomManager.CanLocalPlayerStartMatch() : true);
        }

        private void RenderMembers(RoomManager roomManager)
        {
            if (memberContentRoot == null || memberItemTemplate == null)
                return;

            for (var i = 0; i < _spawnedItems.Count; i++)
            {
                if (_spawnedItems[i] != null)
                    Destroy(_spawnedItems[i].gameObject);
            }

            _spawnedItems.Clear();

            var localUserId = GameManager.EnsureInstance().Player?.ID;
            var participants = roomManager.GetOrderedParticipants();
            foreach (var participant in participants)
            {
                var item = Instantiate(memberItemTemplate, memberContentRoot);
                item.gameObject.SetActive(true);
                item.Bind(
                    participant,
                    string.Equals(participant.UserId, roomManager.CurrentRoom?.HostId, StringComparison.OrdinalIgnoreCase),
                    string.Equals(participant.UserId, localUserId, StringComparison.OrdinalIgnoreCase));
                _spawnedItems.Add(item);
            }
        }

        private async void HandleBackClicked()
        {
            if (_isLoading)
                return;

            _isLoading = true;

            try
            {
                var roomCode = RoomManager.EnsureInstance().GetRoomCode();
                var accessToken = GameManager.EnsureInstance().AccessToken;
                if (!string.IsNullOrWhiteSpace(roomCode) && !string.IsNullOrWhiteSpace(accessToken))
                {
                    var response = await RoomService.Instance.LeaveRoomAsync(roomCode, accessToken);
                    if (response == null || !response.success)
                        throw new InvalidOperationException(response?.message);
                }
            }
            catch (Exception ex)
            {
                UniversalPopup.ShowError(string.IsNullOrWhiteSpace(ex.Message) ? "Unable to leave room." : ex.Message);
            }
            finally
            {
                _isLoading = false;
            }

            RoomManager.EnsureInstance().ClearCurrentRoom();
            LoadMainMenu();
        }

        private void HandleCopyClicked()
        {
            CopyRoomCode();
        }

        private void HandleInviteClicked()
        {
            CopyRoomCode();
        }

        private async void HandleActionClicked()
        {
            if (_isLoading)
            {
                Debug.LogWarning("[WaitRoom] Action clicked while _isLoading=true — ignored.");
                return;
            }

            var roomManager = RoomManager.EnsureInstance();
            var roomCode = roomManager.GetRoomCode();
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                Debug.LogWarning("[WaitRoom] Action clicked but roomCode is empty — ignored.");
                return;
            }

            var isHost = roomManager.IsLocalPlayerHost();
            var wsStatus = _roomSocket?.Status.ToString() ?? "null";
            Debug.Log($"[WaitRoom] Action clicked — isHost={isHost}, wsStatus={wsStatus}, canStart={roomManager.CanLocalPlayerStartMatch()}, isReady={roomManager.IsLocalPlayerReady()}");

            _isLoading = true;
            RenderActionPanel(roomManager);

            try
            {
                if (roomManager.IsLocalPlayerHost())
                {
                    if (!roomManager.CanLocalPlayerStartMatch())
                    {
                        UniversalPopup.ShowError("All other players must be ready before starting.");
                        return;
                    }

                    await _roomSocket.StartRoomMatchAsync(roomCode);
                }
                else
                {
                    await _roomSocket.SetReadyStatusAsync(roomCode, !roomManager.IsLocalPlayerReady());
                }
            }
            catch (Exception ex)
            {
                UniversalPopup.ShowError(string.IsNullOrWhiteSpace(ex.Message) ? "Unable to update room status." : ex.Message);
            }
            finally
            {
                _isLoading = false;
                RenderActionPanel(roomManager);
            }
        }

        private void HandleRoomMemberJoined(AppRoomMemberJoinedDto payload)
        {
            if (!IsCurrentRoomEvent(payload?.roomCode))
                return;

            RoomManager.EnsureInstance().UpsertParticipant(
                payload.userId,
                payload.nickname,
                payload.avatarUrl,
                payload.role,
                payload.isReady);
            Render();
        }

        private void HandleRoomMemberLeft(AppRoomMemberLeftDto payload)
        {
            if (!IsCurrentRoomEvent(payload?.roomCode))
                return;

            RoomManager.EnsureInstance().RemoveParticipant(payload.userId);
            Render();
        }

        private void HandleRoomMemberKicked(AppRoomMemberKickedDto payload)
        {
            if (!IsCurrentRoomEvent(payload?.roomCode))
                return;

            var localUserId = GameManager.EnsureInstance().Player?.ID;
            if (string.Equals(payload.targetUserId, localUserId, StringComparison.OrdinalIgnoreCase))
            {
                UniversalPopup.ShowError("You were removed from the room.");
                RoomManager.EnsureInstance().ClearCurrentRoom();
                LoadMainMenu();
                return;
            }

            RoomManager.EnsureInstance().RemoveParticipant(payload.targetUserId);
            Render();
        }

        private void HandleReadyStatusChanged(AppRoomReadyStatusChangedDto payload)
        {
            if (!IsCurrentRoomEvent(payload?.roomCode))
                return;

            RoomManager.EnsureInstance().UpdateReadyStatus(payload.userId, payload.isReady);
            Render();
        }

        private void HandleHostChanged(AppRoomHostChangedDto payload)
        {
            if (!IsCurrentRoomEvent(payload?.roomCode))
                return;

            RoomManager.EnsureInstance().UpdateHost(payload.newHostUserId);
            Render();
        }

        private void HandleRoomDissolved(AppRoomDissolvedDto payload)
        {
            if (!IsCurrentRoomEvent(payload?.roomCode))
                return;

            UniversalPopup.ShowError("The room was dissolved.");
            RoomManager.EnsureInstance().ClearCurrentRoom();
            LoadMainMenu();
        }

        private async void HandleRoomMatchStarting(AppRoomMatchStartingDto payload)
        {
            if (!IsCurrentRoomEvent(payload?.roomCode) || payload.connection == null)
                return;

            var roomManager = RoomManager.EnsureInstance();
            roomManager.SetStatus("playing");
            roomManager.SetGameplayConnection(payload.connection.wsUrl, payload.connection.wsAccessToken);
            _isTransitioningToGameplay = true;

            try
            {
                await _roomSocket.DisconnectAsync();
            }
            catch
            {
            }

            if (NavigationManager.Instance != null)
            {
                NavigationManager.Instance.LoadGameplay();
                return;
            }

            SceneManager.LoadScene("Loading");
        }

        private void HandleSocketError(AppRoomErrorDto error)
        {
            if (error == null)
                return;

            UniversalPopup.ShowError(string.IsNullOrWhiteSpace(error.message) ? "A room error occurred." : error.message);
        }

        private void HandleSocketStatusChanged(AppRoomConnectionStatus status)
        {
            if (_isTransitioningToGameplay)
                return;

            if (status == AppRoomConnectionStatus.Faulted)
            {
                var message = string.IsNullOrWhiteSpace(_roomSocket?.Session?.lastError)
                    ? "Room connection failed."
                    : _roomSocket.Session.lastError;
                UniversalPopup.ShowError(message);
                return;
            }

            if (status == AppRoomConnectionStatus.Disconnected && _roomSocket?.Session?.connectedAtUtc != null)
                UniversalPopup.ShowError("Room connection closed.");
        }

        private void SubscribeRoomSocket()
        {
            if (_roomSocket == null)
                return;

            _roomSocket.OnStatusChanged += HandleSocketStatusChanged;
            _roomSocket.OnRoomMemberJoined += HandleRoomMemberJoined;
            _roomSocket.OnRoomMemberLeft += HandleRoomMemberLeft;
            _roomSocket.OnRoomMemberKicked += HandleRoomMemberKicked;
            _roomSocket.OnRoomReadyStatusChanged += HandleReadyStatusChanged;
            _roomSocket.OnRoomHostChanged += HandleHostChanged;
            _roomSocket.OnRoomDissolved += HandleRoomDissolved;
            _roomSocket.OnRoomMatchStarting += HandleRoomMatchStarting;
            _roomSocket.OnError += HandleSocketError;
        }

        private void UnsubscribeRoomSocket()
        {
            if (_roomSocket == null)
                return;

            _roomSocket.OnStatusChanged -= HandleSocketStatusChanged;
            _roomSocket.OnRoomMemberJoined -= HandleRoomMemberJoined;
            _roomSocket.OnRoomMemberLeft -= HandleRoomMemberLeft;
            _roomSocket.OnRoomMemberKicked -= HandleRoomMemberKicked;
            _roomSocket.OnRoomReadyStatusChanged -= HandleReadyStatusChanged;
            _roomSocket.OnRoomHostChanged -= HandleHostChanged;
            _roomSocket.OnRoomDissolved -= HandleRoomDissolved;
            _roomSocket.OnRoomMatchStarting -= HandleRoomMatchStarting;
            _roomSocket.OnError -= HandleSocketError;
        }

        private void CopyRoomCode()
        {
            var roomCode = RoomManager.EnsureInstance().GetRoomCode();
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                UniversalPopup.ShowError("Room code is not available.");
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            CopyToClipboard(roomCode);
#else
            GUIUtility.systemCopyBuffer = roomCode;
#endif
            UniversalPopup.ShowSuccess("Room code copied.");
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void CopyToClipboard(string text);
#endif

        private bool IsCurrentRoomEvent(string roomCode)
        {
            return !string.IsNullOrWhiteSpace(roomCode) &&
                   string.Equals(roomCode, RoomManager.EnsureInstance().GetRoomCode(), StringComparison.OrdinalIgnoreCase);
        }

        private static void LoadMainMenu()
        {
            if (NavigationManager.Instance != null)
            {
                NavigationManager.Instance.LoadMainMenu();
                return;
            }

            SceneManager.LoadScene(MainMenuSceneName);
        }

        private void AutoBind()
        {
            roomCodeText ??= FindByPath("Canvas/Content/HeaderPanel/MiddlePanel/RoomCode/Text (TMP)")?.GetComponent<TextMeshProUGUI>();
            playerCountText ??= FindByPath("Canvas/Content/HeaderPanel/MiddlePanel/PlayerCount/Text (TMP)")?.GetComponent<TextMeshProUGUI>();
            roleText ??= FindByPath("Canvas/Content/Panel/StatusPanel/InfoPanel/RoleText")?.GetComponent<TextMeshProUGUI>();
            descriptionText ??= FindByPath("Canvas/Content/Panel/StatusPanel/InfoPanel/DescText")?.GetComponent<TextMeshProUGUI>();
            headerBackButton ??= FindByPath("Canvas/Content/HeaderPanel/BackButton")?.GetComponent<Button>();
            copyButton ??= FindByPath("Canvas/Content/HeaderPanel/MiddlePanel/RoomCode/CopyButton")?.GetComponent<Button>();
            inviteButton ??= FindByPath("Canvas/Content/HeaderPanel/InviteButton")?.GetComponent<Button>();
            actionButton ??= FindByPath("Canvas/Content/Panel/StatusPanel/ReadyButton")?.GetComponent<Button>();
            memberContentRoot ??= FindByPath("Canvas/Content/Panel/MemberPanel/Scroll View/Viewport/Content");
            memberContentRoot ??= memberItemTemplate?.transform.parent;
            memberItemTemplate ??= FindByPath("Canvas/Content/Panel/MemberPanel/Scroll View/Viewport/Content/WaitingUserItem")?.GetComponent<WaitingRoomMemberItemView>();

            if (inviteButtonText == null && inviteButton != null)
                inviteButtonText = inviteButton.GetComponentInChildren<TextMeshProUGUI>(true);

            if (actionButtonText == null && actionButton != null)
                actionButtonText = actionButton.GetComponentInChildren<TextMeshProUGUI>(true);

            LogAutoBind();
        }

        private void LogAutoBind()
        {
            Debug.Log($"[WaitRoom] AutoBind: roomCodeText={roomCodeText != null}, playerCountText={playerCountText != null}, " +
                      $"roleText={roleText != null}, actionButton={actionButton != null}, " +
                      $"memberContentRoot={memberContentRoot != null}, memberItemTemplate={memberItemTemplate != null}");

            if (actionButton != null)
                return;

            Debug.LogWarning("[WaitRoom] actionButton is NULL — READY/START button won't respond to clicks. " +
                             "Check scene path 'Canvas/Content/Panel/StatusPanel/BackButton' or assign in Inspector.");

            var sb = new System.Text.StringBuilder("[WaitRoom] All Buttons in scene: ");
            var allButtons = FindObjectsByType<Button>(FindObjectsSortMode.None);
            foreach (var btn in allButtons)
            {
                var p = btn.name;
                var t = btn.transform.parent;
                while (t != null) { p = $"{t.name}/{p}"; t = t.parent; }
                sb.Append(p).Append(" | ");
            }
            Debug.LogWarning(sb.ToString());
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

            if (currentPath == path)
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

using System;
using Network.API.Services;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Managers.UIManager
{
    public class JoinRoomSceneManager : MonoBehaviour
    {
        private const string WaitRoomSceneName = "WaitRoom";
        private const string MainMenuSceneName = "MainMenu";

        [Header("References")]
        [SerializeField] private TMP_InputField roomCodeInput;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button backButton;

        private bool _isSubmitting;

        private void Awake()
        {
            EnsureInputModule();
            AutoBind();
        }

        private void OnEnable()
        {
            joinButton?.onClick.AddListener(HandleJoinClicked);
            backButton?.onClick.AddListener(HandleBackClicked);
        }

        private void OnDisable()
        {
            joinButton?.onClick.RemoveListener(HandleJoinClicked);
            backButton?.onClick.RemoveListener(HandleBackClicked);
        }

        private async void HandleJoinClicked()
        {
            if (_isSubmitting)
                return;

            var gameManager = GameManager.EnsureInstance();
            if (!gameManager.IsAuthenticated)
            {
                UniversalPopup.ShowError("Please log in before joining a room.");
                return;
            }

            var roomCode = roomCodeInput != null ? roomCodeInput.text.Trim().ToUpperInvariant() : string.Empty;
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                UniversalPopup.ShowError("Please enter a room code.");
                return;
            }

            _isSubmitting = true;
            try
            {
                var response = await RoomService.Instance.JoinRoomAsync(roomCode, gameManager.AccessToken);
                if (response?.success != true || response.data == null)
                {
                    if (response != null && response.errorCode == "PLAYER_ALREADY_IN_ROOM" && response.details != null)
                    {
                        var oldRoomId = response.details.Value<string>("roomId");
                        if (!string.IsNullOrWhiteSpace(oldRoomId))
                        {
                            UniversalPopup.ShowInfo("Bạn đang ở trong phòng khác. Đang rời phòng cũ để tham gia phòng mới...");
                            try
                            {
                                await Network.Websocket.AppRoomWebsocketClient.ForceLeaveRoomAsync(gameManager.AccessToken, oldRoomId);
                                RoomManager.EnsureInstance().ClearCurrentRoom();
                                
                                // Retry join
                                response = await RoomService.Instance.JoinRoomAsync(roomCode, gameManager.AccessToken);
                            }
                            catch (Exception wsEx)
                            {
                                Debug.LogWarning($"[JoinRoom] Failed to auto-leave old room {oldRoomId}: {wsEx.Message}");
                            }
                        }
                    }
                }

                if (response?.success != true || response.data == null)
                {
                    UniversalPopup.ShowError(string.IsNullOrWhiteSpace(response?.message)
                        ? "Unable to join room."
                        : response.message);
                    return;
                }

                RoomManager.EnsureInstance().SetCurrentRoom(response.data);
                UniversalPopup.ShowSuccess("Joined room.");
                LoadWaitRoom();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[JoinRoom] Join room failed: {ex.Message}");
                UniversalPopup.ShowError("Unable to join room.");
            }
            finally
            {
                _isSubmitting = false;
            }
        }

        private void HandleBackClicked()
        {
            LoadMainMenu();
        }

        private void AutoBind()
        {
            var inputRoot = FindByPath("Canvas/EnterCode View/Panel/EnterCode Box");
            if (roomCodeInput == null && inputRoot != null)
            {
                roomCodeInput = inputRoot.GetComponent<TMP_InputField>();
                if (roomCodeInput == null)
                {
                    roomCodeInput = inputRoot.gameObject.AddComponent<TMP_InputField>();
                    roomCodeInput.lineType = TMP_InputField.LineType.SingleLine;
                    roomCodeInput.characterLimit = 12;
                }

                var textComponent = inputRoot.Find("Code")?.GetComponent<TextMeshProUGUI>();
                if (textComponent != null)
                    roomCodeInput.textComponent = textComponent;
            }

            joinButton ??= FindByPath("Canvas/EnterCode View/Panel/Join Button")?.GetComponent<Button>();
            backButton ??= FindByPath("Canvas/BackButton")?.GetComponent<Button>();
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

        private static void LoadWaitRoom()
        {
            if (NavigationManager.Instance != null)
            {
                NavigationManager.Instance.LoadWaitRoom();
                return;
            }

            SceneManager.LoadScene(WaitRoomSceneName);
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

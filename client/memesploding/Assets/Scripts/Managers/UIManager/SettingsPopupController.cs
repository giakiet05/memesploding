using System;
using Managers.Audio;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Network.Websocket;

namespace Managers.UIManager
{
    public class SettingsPopupController : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TextMeshProUGUI musicValueText;
        [SerializeField] private TextMeshProUGUI sfxValueText;

        [Header("Actions")]
        [SerializeField] private Button logoutButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button closeButton;

        [Header("Unimplemented Buttons")]
        [SerializeField] private Button accountButton;
        [SerializeField] private Button supportButton;
        [SerializeField] private Button graphicButton;
        [SerializeField] private Button gameplayButton;
        [SerializeField] private Button languageButton;

        private bool _isInitialized;
        private GameObject _backdropGo;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            RefreshUi();
        }

        private void OnDestroy()
        {
            if (musicSlider != null)
                musicSlider.onValueChanged.RemoveListener(HandleMusicChanged);

            if (sfxSlider != null)
                sfxSlider.onValueChanged.RemoveListener(HandleSfxChanged);

            if (logoutButton != null)
                logoutButton.onClick.RemoveListener(HandleLogoutClicked);

            if (quitButton != null)
                quitButton.onClick.RemoveListener(HandleQuitClicked);

            if (closeButton != null)
                closeButton.onClick.RemoveListener(HandleCloseClicked);

            if (accountButton != null) accountButton.onClick.RemoveAllListeners();
            if (supportButton != null) supportButton.onClick.RemoveAllListeners();
            if (graphicButton != null) graphicButton.onClick.RemoveAllListeners();
            if (gameplayButton != null) gameplayButton.onClick.RemoveAllListeners();
            if (languageButton != null) languageButton.onClick.RemoveAllListeners();
        }

        public void RefreshUi()
        {
            var audioManager = AudioManager.Instance;

            if (musicSlider != null)
                musicSlider.SetValueWithoutNotify(audioManager.MusicVolume);

            if (sfxSlider != null)
                sfxSlider.SetValueWithoutNotify(audioManager.SfxVolume);

            UpdateValueText(musicValueText, audioManager.MusicVolume);
            UpdateValueText(sfxValueText, audioManager.SfxVolume);
        }

        private void Initialize()
        {
            if (_isInitialized)
                return;

            AudioManager.EnsureInstance();

            // Find backdrop under Canvas
            if (_backdropGo == null)
            {
                if (MainMenuManager.Instance != null && MainMenuManager.Instance.SettingPopupBackdrop != null)
                {
                    _backdropGo = MainMenuManager.Instance.SettingPopupBackdrop.gameObject;
                }

                if (_backdropGo == null)
                {
                    var canvasGo = GameObject.Find("Canvas");
                    if (canvasGo != null)
                    {
                        var child = canvasGo.transform.Find("Setting Popup Backdrop");
                        if (child != null)
                        {
                            _backdropGo = child.gameObject;
                        }
                    }
                }

                if (_backdropGo == null)
                {
                    var popups = FindObjectsByType<Popup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var p in popups)
                    {
                        if (p.name.ToLower().Contains("setting") && (p.name.ToLower().Contains("backdrop") || p.name.ToLower().Contains("popup")))
                        {
                            _backdropGo = p.gameObject;
                            break;
                        }
                    }
                }
            }

            // Find components dynamically if not assigned
            if (musicSlider == null)
            {
                var t = transform.Find("Sfx-slider");
                if (t != null) musicSlider = t.GetComponent<Slider>();
            }

            if (sfxSlider == null)
            {
                var t = transform.Find("Audio Setting Button/Sfx-slider");
                if (t != null) sfxSlider = t.GetComponent<Slider>();
            }

            if (musicValueText == null)
            {
                var t = transform.Find("Audio Setting Button/Text (TMP) (2)");
                if (t != null) musicValueText = t.GetComponent<TextMeshProUGUI>();
            }

            if (sfxValueText == null)
            {
                var t = transform.Find("Audio Setting Button/Text (TMP) (3)");
                if (t != null) sfxValueText = t.GetComponent<TextMeshProUGUI>();
            }

            // Fallback for texts
            if (musicValueText == null || sfxValueText == null)
            {
                var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var txt in texts)
                {
                    if (txt.name == "MusicPercentageIcon" && musicValueText == null) musicValueText = txt;
                    if (txt.name == "SFXPercentageIcon" && sfxValueText == null) sfxValueText = txt;
                }
            }

            // Find buttons
            foreach (Transform child in transform)
            {
                if (child.name == "Button")
                {
                    var btn = child.GetComponent<Button>();
                    if (btn != null)
                    {
                        var txt = child.GetComponentInChildren<TextMeshProUGUI>();
                        if (txt != null && txt.text.ToLower().Contains("logout"))
                        {
                            logoutButton = btn;
                        }
                        else
                        {
                            closeButton = btn;
                        }
                    }
                }
            }

            if (closeButton == null)
            {
                var t = transform.Find("Close Button");
                if (t != null) closeButton = t.GetComponent<Button>();
            }

            if (accountButton == null)
            {
                var t = transform.Find("Account Setting Button");
                if (t != null) accountButton = t.GetComponent<Button>();
            }
            if (supportButton == null)
            {
                var t = transform.Find("Support Setting Button (1)");
                if (t != null) supportButton = t.GetComponent<Button>();
            }
            if (graphicButton == null)
            {
                var t = transform.Find("Graphic Setting Button (1)");
                if (t != null) graphicButton = t.GetComponent<Button>();
            }
            if (gameplayButton == null)
            {
                var t = transform.Find("Gameplay Setting Button (2)");
                if (t != null) gameplayButton = t.GetComponent<Button>();
            }
            if (languageButton == null)
            {
                var t = transform.Find("Language Setting Button");
                if (t != null) languageButton = t.GetComponent<Button>();
            }

            // Bind Sliders
            if (musicSlider != null)
            {
                musicSlider.minValue = 0f;
                musicSlider.maxValue = 1f;
                musicSlider.wholeNumbers = false;
                musicSlider.onValueChanged.RemoveAllListeners();
                musicSlider.onValueChanged.AddListener(HandleMusicChanged);
            }

            if (sfxSlider != null)
            {
                sfxSlider.minValue = 0f;
                sfxSlider.maxValue = 1f;
                sfxSlider.wholeNumbers = false;
                sfxSlider.onValueChanged.RemoveAllListeners();
                sfxSlider.onValueChanged.AddListener(HandleSfxChanged);
            }

            // Bind actions based on scene
            var sceneName = SceneManager.GetActiveScene().name;
            bool isGameplayOrWaitRoom = (sceneName == "Gameplay" || sceneName == "WaitRoom");

            if (logoutButton != null)
            {
                logoutButton.onClick.RemoveAllListeners();
                var btnText = logoutButton.GetComponentInChildren<TextMeshProUGUI>();
                if (isGameplayOrWaitRoom)
                {
                    if (btnText != null) btnText.text = "Thoát phòng";
                    logoutButton.onClick.AddListener(HandleQuitClicked);
                }
                else
                {
                    if (btnText != null) btnText.text = "Đăng xuất";
                    logoutButton.onClick.AddListener(HandleLogoutClicked);
                }
                logoutButton.gameObject.SetActive(true);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveAllListeners();
                quitButton.onClick.AddListener(HandleQuitClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(HandleCloseClicked);
            }

            // Unimplemented tabs
            if (accountButton != null)
            {
                accountButton.onClick.RemoveAllListeners();
                accountButton.onClick.AddListener(() => ShowFeatureUnderDevelopment("Tài khoản"));
            }
            if (supportButton != null)
            {
                supportButton.onClick.RemoveAllListeners();
                supportButton.onClick.AddListener(() => ShowFeatureUnderDevelopment("Hỗ trợ"));
            }
            if (graphicButton != null)
            {
                graphicButton.onClick.RemoveAllListeners();
                graphicButton.onClick.AddListener(() => ShowFeatureUnderDevelopment("Đồ họa"));
            }
            if (languageButton != null)
            {
                languageButton.onClick.RemoveAllListeners();
                languageButton.onClick.AddListener(() => ShowFeatureUnderDevelopment("Ngôn ngữ"));
            }

            // Gameplay (guide) button
            if (gameplayButton != null)
            {
                gameplayButton.onClick.RemoveAllListeners();
                gameplayButton.onClick.AddListener(() => {
                    HandleCloseClicked();
                    GameplayGuidePopup.Show();
                });
            }

            // Bind backdrop click to close
            if (_backdropGo != null)
            {
                var backdropBtn = _backdropGo.GetComponent<Button>() ?? _backdropGo.AddComponent<Button>();
                backdropBtn.transition = Selectable.Transition.None;
                backdropBtn.onClick.RemoveAllListeners();
                backdropBtn.onClick.AddListener(HandleCloseClicked);
            }

            _isInitialized = true;
        }

        private void HandleMusicChanged(float value)
        {
            AudioManager.Instance.SetMusicVolume(value);
            UpdateValueText(musicValueText, value);
        }

        private void HandleSfxChanged(float value)
        {
            AudioManager.Instance.SetSfxVolume(value);
            UpdateValueText(sfxValueText, value);
        }

        private async void HandleLogoutClicked()
        {
            if (logoutButton != null)
                logoutButton.interactable = false;

            UniversalPopup.ShowInfo("Đang đăng xuất...");
            await GameManager.EnsureInstance().LogoutAsync();

            if (logoutButton != null)
                logoutButton.interactable = true;
        }

        private async void HandleQuitClicked()
        {
            if (logoutButton != null)
                logoutButton.interactable = false;
            if (quitButton != null)
                quitButton.interactable = false;

            UniversalPopup.ShowInfo("Đang ngắt kết nối...");

            var roomCode = RoomManager.EnsureInstance().GetRoomCode();
            if (!string.IsNullOrWhiteSpace(roomCode))
            {
                try
                {
                    var gameManager = GameManager.EnsureInstance();
                    if (gameManager != null && !string.IsNullOrWhiteSpace(gameManager.AccessToken))
                    {
                        await AppRoomWebsocketClient.ForceLeaveRoomAsync(gameManager.AccessToken, roomCode);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SettingsQuit] Failed to leave room on API server: {ex.Message}");
                }
            }

            RoomManager.EnsureInstance().ClearCurrentRoom();

            if (GameWebsocketClient.Instance != null)
                await GameWebsocketClient.Instance.DisconnectAsync();

            SceneManager.LoadScene("MainMenu");

            if (logoutButton != null)
                logoutButton.interactable = true;
            if (quitButton != null)
                quitButton.interactable = true;
        }

        private void HandleCloseClicked()
        {
            if (GameplayUIManager.Instance != null)
            {
                GameplayUIManager.Instance.OnSettingsPopupClosed();
                return;
            }

            if (_backdropGo != null)
            {
                var popup = _backdropGo.GetComponent<Popup>();
                if (popup != null)
                    popup.Hide();
                else
                    _backdropGo.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void ShowFeatureUnderDevelopment(string featureName)
        {
            UniversalPopup.ShowInfo($"{featureName}: Tính năng đang phát triển!");
        }

        private static void UpdateValueText(TextMeshProUGUI label, float value)
        {
            if (label == null)
                return;

            label.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
        }
    }
}

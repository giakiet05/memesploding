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
        // Comic Style Theme — matches GameplayGuidePopup
        private static readonly Color ComicInk      = new Color(0.09f, 0.07f, 0.06f, 1.00f);
        private static readonly Color ComicNavBg    = new Color(0.92f, 0.48f, 0.15f, 1.00f);
        private static readonly Color ComicNavHov   = new Color(0.98f, 0.58f, 0.25f, 1.00f);
        private static readonly Color ComicNavPrs   = new Color(0.80f, 0.38f, 0.08f, 1.00f);
        private static readonly Color ComicDanger   = new Color(0.75f, 0.18f, 0.12f, 1.00f);
        private static readonly Color ComicDangerHov = new Color(0.86f, 0.25f, 0.18f, 1.00f);
        private static readonly Color ComicDangerPrs = new Color(0.62f, 0.12f, 0.08f, 1.00f);

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
        [SerializeField] private Button audioButton;

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

            if (accountButton != null) accountButton.onClick = new Button.ButtonClickedEvent();
            if (supportButton != null) supportButton.onClick = new Button.ButtonClickedEvent();
            if (graphicButton != null) graphicButton.onClick = new Button.ButtonClickedEvent();
            if (gameplayButton != null) gameplayButton.onClick.RemoveAllListeners();
            if (languageButton != null) languageButton.onClick.RemoveAllListeners();
            if (audioButton != null) audioButton.onClick.RemoveAllListeners();
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
                var t = transform.Find("Audio Setting Button/Text (TMP) (3)");
                if (t != null) musicValueText = t.GetComponent<TextMeshProUGUI>();
            }

            if (sfxValueText == null)
            {
                var t = transform.Find("Audio Setting Button/Text (TMP) (2)");
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

            if (audioButton == null)
            {
                var t = transform.Find("Audio Setting Button");
                if (t != null) audioButton = t.GetComponent<Button>();
            }
            if (audioButton == null)
            {
                foreach (var btn in GetComponentsInChildren<Button>(true))
                {
                    if (btn != null && btn.name.ToLower().Contains("audio"))
                    { audioButton = btn; break; }
                }
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
                logoutButton.onClick = new Button.ButtonClickedEvent();
                var btnText = logoutButton.GetComponentInChildren<TextMeshProUGUI>();
                if (isGameplayOrWaitRoom)
                {
                    if (btnText != null) btnText.text = "Thoát phòng";
                    logoutButton.onClick.AddListener(HandleQuitClicked);
                    // Hide child icon images so they don't overlap the text
                    HideChildIconImages(logoutButton);
                    if (btnText != null)
                    {
                        btnText.rectTransform.anchorMin = Vector2.zero;
                        btnText.rectTransform.anchorMax = Vector2.one;
                        btnText.rectTransform.offsetMin = Vector2.zero;
                        btnText.rectTransform.offsetMax = Vector2.zero;
                        btnText.alignment = TextAlignmentOptions.Center;
                    }
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
                quitButton.onClick = new Button.ButtonClickedEvent();
                quitButton.onClick.AddListener(HandleQuitClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick = new Button.ButtonClickedEvent();
                closeButton.onClick.AddListener(HandleCloseClicked);
            }

            // Unimplemented tabs
            if (accountButton != null)
            {
                accountButton.onClick = new Button.ButtonClickedEvent();
                accountButton.onClick.AddListener(() => ShowFeatureUnderDevelopment("Tài khoản"));
            }
            if (supportButton != null)
            {
                supportButton.onClick = new Button.ButtonClickedEvent();
                supportButton.onClick.AddListener(() => ShowFeatureUnderDevelopment("Hỗ trợ"));
            }
            if (graphicButton != null)
            {
                graphicButton.onClick = new Button.ButtonClickedEvent();
                graphicButton.onClick.AddListener(() => ShowFeatureUnderDevelopment("Đồ họa"));
            }
            if (languageButton != null)
            {
                languageButton.onClick = new Button.ButtonClickedEvent();
                languageButton.onClick.AddListener(() => ShowFeatureUnderDevelopment("Ngôn ngữ"));
            }

            if (audioButton != null)
            {
                audioButton.onClick = new Button.ButtonClickedEvent();
                audioButton.onClick.AddListener(() => ShowFeatureUnderDevelopment("Âm thanh"));
            }

            // Gameplay (guide) button
            if (gameplayButton != null)
            {
                gameplayButton.onClick = new Button.ButtonClickedEvent();
                gameplayButton.onClick.AddListener(() => {
                    HandleCloseClicked();
                    GameplayGuidePopup.Show();
                });
            }

            // Bind backdrop click to close (backdrop is the transparent overlay BEHIND the panel)
            if (_backdropGo != null && _backdropGo != gameObject)
            {
                var backdropBtn = _backdropGo.GetComponent<Button>() ?? _backdropGo.AddComponent<Button>();
                backdropBtn.transition = Selectable.Transition.None;
                backdropBtn.onClick = new Button.ButtonClickedEvent();
                backdropBtn.onClick.AddListener(HandleCloseClicked);
            }

            // Fallback: any unbound child button shows "under development" toast
            BindUnhandledButtons(logoutButton, closeButton, quitButton,
                accountButton, supportButton, graphicButton, gameplayButton, languageButton, audioButton);

            _isInitialized = true;
            ApplyComicTheme();
        }

        private void ApplyComicTheme()
        {
            // Panel background — must block raycasts so backdrop Button doesn't fire through the panel
            var panelImg = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            panelImg.raycastTarget = true;
            var sprite = Resources.Load<Sprite>("bg-SettingPopup");
            if (sprite != null)
            {
                panelImg.sprite = sprite;
                panelImg.type = Image.Type.Sliced;
            }
            else if (panelImg.color.a < 0.01f)
            {
                panelImg.color = new Color(1f, 1f, 1f, 0.01f);
            }
            if (!gameObject.TryGetComponent<Outline>(out _))
            {
                var outline = gameObject.AddComponent<Outline>();
                outline.effectColor = ComicInk;
                outline.effectDistance = new Vector2(5f, -5f);
            }

            // Absorb pointer events on the panel itself so they don't bubble up to the backdrop Button.
            // Without this, clicking a slider (which doesn't implement IPointerClickHandler) lets the
            // click propagate upward through the hierarchy and fires the backdrop's close handler.
            var clickBlocker = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
            clickBlocker.transition = Selectable.Transition.None;
            clickBlocker.onClick.RemoveAllListeners();

            // Navigation / info buttons — orange accent
            StyleComicButton(accountButton,  ComicNavBg, ComicNavHov, ComicNavPrs);
            StyleComicButton(supportButton,  ComicNavBg, ComicNavHov, ComicNavPrs);
            StyleComicButton(graphicButton,  ComicNavBg, ComicNavHov, ComicNavPrs);
            StyleComicButton(gameplayButton, ComicNavBg, ComicNavHov, ComicNavPrs);
            StyleComicButton(languageButton, ComicNavBg, ComicNavHov, ComicNavPrs);

            // Destructive buttons — red accent
            StyleComicButton(logoutButton, ComicDanger, ComicDangerHov, ComicDangerPrs);
            StyleComicButton(quitButton,   ComicDanger, ComicDangerHov, ComicDangerPrs);

            // Close button — transparent with ink X
            StyleComicCloseButton(closeButton);

            // Sliders
            StyleComicSlider(musicSlider);
            StyleComicSlider(sfxSlider);
        }

        private void StyleComicButton(Button btn, Color normal, Color hover, Color pressed)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = Color.white;
                if (!btn.TryGetComponent<Outline>(out _))
                {
                    var o = btn.gameObject.AddComponent<Outline>();
                    o.effectColor = ComicInk;
                    o.effectDistance = new Vector2(2f, -2f);
                }
            }
            btn.colors = new ColorBlock
            {
                normalColor      = normal,
                highlightedColor = hover,
                pressedColor     = pressed,
                selectedColor    = hover,
                disabledColor    = new Color(0.70f, 0.70f, 0.70f, 0.5f),
                colorMultiplier  = 1f,
                fadeDuration     = 0.1f
            };
            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.color = Color.white;
        }

        private void StyleComicCloseButton(Button btn)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = Color.clear;
            btn.colors = new ColorBlock
            {
                normalColor      = Color.clear,
                highlightedColor = new Color(0.86f, 0.12f, 0.10f, 0.15f),
                pressedColor     = new Color(0.86f, 0.12f, 0.10f, 0.35f),
                selectedColor    = new Color(0.86f, 0.12f, 0.10f, 0.15f),
                disabledColor    = new Color(0.7f, 0.7f, 0.7f, 0.5f),
                colorMultiplier  = 1f,
                fadeDuration     = 0.1f
            };
            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.color = ComicInk;
        }

        private static void StyleComicSlider(Slider slider)
        {
            if (slider == null) return;

            // Ensure every Image in the slider blocks raycasts so clicks on the track/fill
            // don't fall through to the backdrop Button behind the panel.
            foreach (var img in slider.GetComponentsInChildren<Image>(true))
                img.raycastTarget = true;

            var fillImg = slider.fillRect?.GetComponent<Image>();
            if (fillImg != null) fillImg.color = ComicNavBg;
            var handleImg = slider.handleRect?.GetComponent<Image>();
            if (handleImg != null)
            {
                handleImg.color = Color.white;
                if (!handleImg.TryGetComponent<Outline>(out _))
                {
                    var o = handleImg.gameObject.AddComponent<Outline>();
                    o.effectColor = ComicInk;
                    o.effectDistance = new Vector2(2f, -2f);
                }
            }
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
                    Debug.LogWarning($"[SettingsQuit] Failed to leave room via WebSocket: {ex.Message}");
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

        private void BindUnhandledButtons(params Button[] knownButtons)
        {
            var known = new System.Collections.Generic.HashSet<Button>(knownButtons);
            foreach (var btn in GetComponentsInChildren<Button>(true))
            {
                if (btn == null || known.Contains(btn)) continue;
                var capturedName = btn.name;
                // Replace the entire event object to clear both runtime and persistent listeners
                btn.onClick = new Button.ButtonClickedEvent();
                btn.onClick.AddListener(() => ShowFeatureUnderDevelopment(capturedName));
            }
        }

        private static void HideChildIconImages(Button btn)
        {
            if (btn == null) return;
            foreach (Transform child in btn.transform)
            {
                var img = child.GetComponent<Image>();
                if (img != null)
                    img.gameObject.SetActive(false);
            }
        }

        private static void UpdateValueText(TextMeshProUGUI label, float value)
        {
            if (label == null)
                return;

            label.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
        }
    }
}

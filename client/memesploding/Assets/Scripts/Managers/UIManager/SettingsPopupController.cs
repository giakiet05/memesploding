using Managers.Audio;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

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

        private bool _isInitialized;

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

            if (musicSlider != null)
            {
                musicSlider.minValue = 0f;
                musicSlider.maxValue = 1f;
                musicSlider.wholeNumbers = false;
                musicSlider.onValueChanged.AddListener(HandleMusicChanged);
            }

            if (sfxSlider != null)
            {
                sfxSlider.minValue = 0f;
                sfxSlider.maxValue = 1f;
                sfxSlider.wholeNumbers = false;
                sfxSlider.onValueChanged.AddListener(HandleSfxChanged);
            }

            if (logoutButton != null)
                logoutButton.onClick.AddListener(HandleLogoutClicked);

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

            UniversalPopup.ShowInfo("Logging out...");
            await GameManager.EnsureInstance().LogoutAsync();

            if (logoutButton != null)
                logoutButton.interactable = true;
        }

        private static void UpdateValueText(TextMeshProUGUI label, float value)
        {
            if (label == null)
                return;

            label.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
        }
    }
}

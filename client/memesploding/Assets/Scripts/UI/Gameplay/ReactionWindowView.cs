using System;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Gameplay
{
    public class ReactionWindowView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI actionText;
        [SerializeField] private TextMeshProUGUI nopeCountText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Button nopeButton;
        [SerializeField] private Button passButton;
        [SerializeField] private Image timerFill;

        private DateTime? _endsAtUtc;
        private float _initialSeconds;

        private void Awake()
        {
            BindReferences();
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            BindButtons();
        }

        private void OnDisable()
        {
            UnbindButtons();
        }

        private void Update()
        {
            if (!_endsAtUtc.HasValue)
                return;

            var remaining = Mathf.Max(0f, (float)(_endsAtUtc.Value - DateTime.UtcNow).TotalSeconds);
            SetTimer(remaining);

            if (remaining <= 0f)
                Hide();
        }

        public void Show(string userId, string cardCode, int nopeCount, TimeSpan? timeLeft, bool canNope)
        {
            BindReferences();

            if (titleText != null)
                titleText.text = "NOPE WINDOW";

            if (actionText != null)
                actionText.text = string.IsNullOrWhiteSpace(cardCode) ? "Action pending" : cardCode;

            if (nopeCountText != null)
                nopeCountText.text = $"Nope count: {Mathf.Max(0, nopeCount)}";

            if (nopeButton != null)
                nopeButton.interactable = canNope;

            _endsAtUtc = null;
            _initialSeconds = 0f;

            if (timeLeft.HasValue && timeLeft.Value.TotalSeconds > 0)
            {
                _initialSeconds = Mathf.Max(0.01f, (float)timeLeft.Value.TotalSeconds);
                _endsAtUtc = DateTime.UtcNow.AddSeconds(_initialSeconds);
                SetTimer(_initialSeconds);
            }
            else
            {
                if (timerText != null)
                    timerText.text = "Waiting";

                if (timerFill != null)
                    timerFill.fillAmount = 1f;
            }

            gameObject.SetActive(true);
        }

        public void ShowNoped(string cardCode, int nopeCount)
        {
            BindReferences();

            if (titleText != null)
                titleText.text = "NOPED";

            if (actionText != null)
                actionText.text = string.IsNullOrWhiteSpace(cardCode) ? "Action canceled" : $"{cardCode} canceled";

            if (nopeCountText != null)
                nopeCountText.text = $"Final count: {Mathf.Max(0, nopeCount)}";

            if (timerText != null)
                timerText.text = string.Empty;

            if (timerFill != null)
                timerFill.fillAmount = 0f;

            if (nopeButton != null)
                nopeButton.interactable = false;

            _endsAtUtc = null;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _endsAtUtc = null;
            gameObject.SetActive(false);
        }

        private void SetTimer(float remaining)
        {
            if (timerText != null)
                timerText.text = $"{Mathf.CeilToInt(remaining)}s";

            if (timerFill != null)
                timerFill.fillAmount = _initialSeconds > 0f ? Mathf.Clamp01(remaining / _initialSeconds) : 1f;
        }

        private void HandleNopeClicked()
        {
            if (GameManager.Instance == null)
                return;

            nopeButton.interactable = false;
            GameManager.Instance.Nope();
        }

        private void HandlePassClicked()
        {
            Hide();
        }

        private void BindReferences()
        {
            titleText ??= transform.Find("Header/Title")?.GetComponent<TextMeshProUGUI>();
            actionText ??= transform.Find("Body/ActionText")?.GetComponent<TextMeshProUGUI>();
            nopeCountText ??= transform.Find("Body/NopeCountText")?.GetComponent<TextMeshProUGUI>();
            timerText ??= transform.Find("Body/Timer/TimerText")?.GetComponent<TextMeshProUGUI>();
            timerFill ??= transform.Find("Body/Timer/TimerFill")?.GetComponent<Image>();
            nopeButton ??= transform.Find("Actions/NopeButton")?.GetComponent<Button>();
            passButton ??= transform.Find("Actions/PassButton")?.GetComponent<Button>();
        }

        private void BindButtons()
        {
            if (nopeButton != null)
            {
                nopeButton.onClick.RemoveListener(HandleNopeClicked);
                nopeButton.onClick.AddListener(HandleNopeClicked);
            }

            if (passButton != null)
            {
                passButton.onClick.RemoveListener(HandlePassClicked);
                passButton.onClick.AddListener(HandlePassClicked);
            }
        }

        private void UnbindButtons()
        {
            if (nopeButton != null)
                nopeButton.onClick.RemoveListener(HandleNopeClicked);

            if (passButton != null)
                passButton.onClick.RemoveListener(HandlePassClicked);
        }
    }
}

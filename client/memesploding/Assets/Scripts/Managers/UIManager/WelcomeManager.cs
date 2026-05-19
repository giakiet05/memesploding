using System.Collections.Generic;
using Network.API.Models;
using Network.API.Services;
using TMPro;
using UI;
using UnityEngine;

namespace Managers.UIManager
{
    public class WelcomeManager : MonoBehaviour
    {
        public static WelcomeManager Instance;

        [Header("Popups")]
        [SerializeField] private Popup loginBackdrop;
        [SerializeField] private Popup loginWithEmailPopup;
        [SerializeField] private Popup registerWithEmailPopup;
        [SerializeField] private Popup forgotPasswordSendOtpBackdrop;
        [SerializeField] private Popup forgotPasswordSendOtpPopup;
        [SerializeField] private Popup forgotPasswordNewPasswordBackdrop;
        [SerializeField] private Popup verifyPopup;
        [SerializeField] private Popup loadingPopup;

        [Header("Login")]
        [SerializeField] private TMP_InputField loginEmailInput;
        [SerializeField] private TMP_InputField loginPasswordInput;
        [SerializeField] private TMP_InputField googleIdTokenInput;

        [Header("Register")]
        [SerializeField] private TMP_InputField registerEmailInput;
        [SerializeField] private TMP_InputField registerPasswordInput;
        [SerializeField] private TMP_InputField registerConfirmPasswordInput;

        [Header("Forgot Password")]
        [SerializeField] private TMP_InputField forgotPasswordEmailInput;
        [SerializeField] private TMP_InputField forgotPasswordOtpInput;
        [SerializeField] private TMP_InputField newPasswordInput;
        [SerializeField] private TMP_InputField confirmNewPasswordInput;
        [SerializeField] private TMP_Text statusText;

        private const string DeviceIdKey = "memesploding.device_id";
        private const string AccessTokenKey = "memesploding.access_token";
        private const string RefreshTokenKey = "memesploding.refresh_token";

        private readonly List<Popup[]> _popupHistory = new();
        private string _pendingForgotPasswordEmail;
        private string _pendingForgotPasswordOtp;
        private bool _isWaitingForServer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Destroy(gameObject);
            else
                Instance = this;
        }

        public void HideAllPopups()
        {
            Hide(loginBackdrop);
            Hide(loginWithEmailPopup);
            Hide(registerWithEmailPopup);
            Hide(forgotPasswordSendOtpBackdrop);
            Hide(forgotPasswordSendOtpPopup);
            Hide(forgotPasswordNewPasswordBackdrop);
            Hide(verifyPopup);
            Hide(loadingPopup);
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

        public void OpenLogin()
        {
            ShowGroup(loginBackdrop);
        }

        public void OpenLoginWithEmail()
        {
            ShowGroup(loginWithEmailPopup);
        }

        public void OpenRegisterWithEmail()
        {
            ShowGroup(registerWithEmailPopup);
        }

        public void OpenForgotPasswordSendOtp()
        {
            ShowGroup(forgotPasswordSendOtpBackdrop, forgotPasswordSendOtpPopup);
        }

        public void OpenForgotPasswordNewPassword()
        {
            ShowGroup(forgotPasswordNewPasswordBackdrop);
        }

        public void OpenVerify()
        {
            ShowGroup(verifyPopup);
        }

        public void ShowLoading()
        {
            ShowGroup(loadingPopup);
        }

        public async void ContinueAsGuest()
        {
            if (_isWaitingForServer)
                return;

            BeginServerRequest();
            SetStatus("Signing in as guest...");

            try
            {
                var response = await AuthService.Instance.RegisterGuestAsync(new RegisterGuestRequestDto
                {
                    DeviceId = GetOrCreateDeviceId()
                });

                if (!TryHandleAuthSuccess(response, "Guest login failed"))
                    return;

                SetStatus(response.message);
                LoadMainMenu();
            }
            finally
            {
                EndServerRequest();
            }
        }

        public void SubmitGoogleLogin()
        {
            SubmitGoogleLogin(GetText(googleIdTokenInput));
        }

        public async void SubmitGoogleLogin(string idToken)
        {
            if (_isWaitingForServer)
                return;

            idToken = idToken?.Trim();
            if (string.IsNullOrWhiteSpace(idToken))
            {
                Debug.LogWarning("Google id token is required", this);
                return;
            }

            BeginServerRequest();
            SetStatus("Signing in with Google...");

            try
            {
                var response = await AuthService.Instance.LoginGoogleAsync(new LoginGoogleRequestDto
                {
                    IdToken = idToken
                });

                if (!TryHandleAuthSuccess(response, "Google login failed"))
                    return;

                SetStatus(response.message);
                LoadMainMenu();
            }
            finally
            {
                EndServerRequest();
            }
        }

        public async void SubmitEmailLogin()
        {
            if (_isWaitingForServer)
                return;

            var email = GetText(loginEmailInput).Trim();
            var password = GetText(loginPasswordInput);

            if (!ValidateEmailPassword(email, password))
                return;

            BeginServerRequest();
            SetStatus("Signing in...");

            try
            {
                var response = await AuthService.Instance.LoginEmailAsync(new LoginEmailRequestDto
                {
                    Email = email,
                    Password = password
                });

                if (!TryHandleAuthSuccess(response, "Email login failed"))
                    return;

                SetStatus(response.message);
                LoadMainMenu();
            }
            finally
            {
                EndServerRequest();
            }
        }

        public async void SubmitEmailRegister()
        {
            if (_isWaitingForServer)
                return;

            var email = GetText(registerEmailInput).Trim();
            var password = GetText(registerPasswordInput);
            var confirmPassword = GetText(registerConfirmPasswordInput);

            if (!ValidateEmailPassword(email, password) || !ValidatePasswordConfirmation(password, confirmPassword))
                return;

            BeginServerRequest();
            SetStatus("Creating account...");

            try
            {
                var response = await AuthService.Instance.RegisterEmailAsync(new RegisterEmailRequestDto
                {
                    Email = email,
                    Password = password
                });

                if (!TryHandleAuthSuccess(response, "Email registration failed"))
                    return;

                SetStatus(response.message);
                LoadMainMenu();
            }
            finally
            {
                EndServerRequest();
            }
        }

        public async void SubmitForgotPasswordSendOtp()
        {
            if (_isWaitingForServer)
                return;

            var email = GetText(forgotPasswordEmailInput).Trim();
            if (!IsValidEmail(email))
            {
                Debug.LogWarning("A valid email is required", this);
                return;
            }

            BeginServerRequest();
            SetStatus("Sending OTP...");

            try
            {
                var response = await AuthService.Instance.SendForgotPasswordOtpAsync(new ForgotPasswordSendOtpRequestDto
                {
                    Email = email
                });

                if (!TryHandleSimpleSuccess(response, "Unable to send OTP"))
                    return;

                _pendingForgotPasswordEmail = email;
                SetStatus(response.message);
                OpenVerify();
            }
            finally
            {
                EndServerRequest();
            }
        }

        public async void SubmitForgotPasswordVerifyOtp()
        {
            if (_isWaitingForServer)
                return;

            var otp = GetText(forgotPasswordOtpInput).Trim();
            if (otp.Length != 6)
            {
                Debug.LogWarning("A 6-digit OTP code is required", this);
                return;
            }

            BeginServerRequest();
            SetStatus("Verifying OTP...");

            try
            {
                var response = await AuthService.Instance.VerifyForgotPasswordOtpAsync(new ForgotPasswordVerifyOtpRequestDto
                {
                    Email = _pendingForgotPasswordEmail,
                    Otp = otp
                });

                if (!TryHandleSimpleSuccess(response, "OTP verification failed"))
                    return;

                _pendingForgotPasswordOtp = otp;
                SetStatus(response.message);
                OpenForgotPasswordNewPassword();
            }
            finally
            {
                EndServerRequest();
            }
        }

        public async void SubmitForgotPasswordReset()
        {
            if (_isWaitingForServer)
                return;

            var password = GetText(newPasswordInput);
            var confirmPassword = GetText(confirmNewPasswordInput);
            if (!ValidatePasswordConfirmation(password, confirmPassword))
                return;

            BeginServerRequest();
            SetStatus("Resetting password...");

            try
            {
                var response = await AuthService.Instance.ResetForgotPasswordAsync(new ForgotPasswordResetRequestDto
                {
                    Email = _pendingForgotPasswordEmail,
                    Otp = _pendingForgotPasswordOtp,
                    NewPassword = password
                });

                if (!TryHandleSimpleSuccess(response, "Password reset failed"))
                    return;

                SetStatus(response.message);
                HideAllPopups();
                OpenLoginWithEmail();
            }
            finally
            {
                EndServerRequest();
            }
        }

        private void BeginServerRequest()
        {
            _isWaitingForServer = true;
            // TODO: Add richer UI effect for waiting for server response, such as disabling buttons and animating a spinner.
            Show(loadingPopup);
        }

        private void EndServerRequest()
        {
            _isWaitingForServer = false;
            Hide(loadingPopup);
        }

        private bool TryHandleAuthSuccess(ApiResponse<AuthResponseDto> response, string fallbackMessage)
        {
            if (response?.success != true || response.data == null)
            {
                SetStatus(response?.message ?? fallbackMessage);
                return false;
            }

            SaveAuthSession(response.data);
            return true;
        }

        private bool TryHandleSimpleSuccess(ApiResponse<object> response, string fallbackMessage)
        {
            if (response?.success != true)
            {
                SetStatus(response?.message ?? fallbackMessage);
                return false;
            }

            return true;
        }

        private void SaveAuthSession(AuthResponseDto auth)
        {
            PlayerPrefs.SetString(AccessTokenKey, auth.AccessToken ?? string.Empty);
            PlayerPrefs.SetString(RefreshTokenKey, auth.RefreshToken ?? string.Empty);

            if (auth.User != null)
            {
                PlayerPrefs.SetString("memesploding.user_id", auth.User.Id ?? string.Empty);
                PlayerPrefs.SetString("memesploding.username", auth.User.Username ?? string.Empty);
            }

            PlayerPrefs.Save();
        }

        private string GetOrCreateDeviceId()
        {
            var deviceId = PlayerPrefs.GetString(DeviceIdKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(deviceId))
                return deviceId;

            deviceId = System.Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(DeviceIdKey, deviceId);
            PlayerPrefs.Save();
            return deviceId;
        }

        private bool ValidateEmailPassword(string email, string password)
        {
            if (!IsValidEmail(email))
            {
                SetStatus("A valid email is required");
                return false;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                SetStatus("Password is required");
                return false;
            }

            return true;
        }

        private bool ValidatePasswordConfirmation(string password, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                SetStatus("Password is required");
                return false;
            }

            if (password != confirmPassword)
            {
                SetStatus("Passwords do not match");
                return false;
            }

            return true;
        }

        private bool IsValidEmail(string email)
        {
            return !string.IsNullOrWhiteSpace(email) && email.Contains("@") && email.Contains(".");
        }

        private string GetText(TMP_InputField input)
        {
            return input != null ? input.text ?? string.Empty : string.Empty;
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[WelcomeManager] {message}", this);
        }

        private void LoadMainMenu()
        {
            if (NavigationManager.Instance == null)
            {
                Debug.LogWarning("NavigationManager is missing from the scene", this);
                return;
            }

            NavigationManager.Instance.LoadMainMenu();
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
    }
}

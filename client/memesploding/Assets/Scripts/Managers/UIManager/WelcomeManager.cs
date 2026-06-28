using System.Collections.Generic;
using System;
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
        [SerializeField] private TMP_Text loginEmailErrorText;
        [SerializeField] private TMP_Text loginPasswordErrorText;

        [Header("Register")]
        [SerializeField] private TMP_InputField registerEmailInput;
        [SerializeField] private TMP_InputField registerPasswordInput;
        [SerializeField] private TMP_InputField registerConfirmPasswordInput;
        [SerializeField] private TMP_Text registerEmailErrorText;
        [SerializeField] private TMP_Text registerPasswordErrorText;
        [SerializeField] private TMP_Text registerConfirmPasswordErrorText;

        [Header("Forgot Password")]
        [SerializeField] private TMP_InputField forgotPasswordEmailInput;
        [SerializeField] private TMP_InputField forgotPasswordOtpInput;
        [SerializeField] private TMP_InputField newPasswordInput;
        [SerializeField] private TMP_InputField confirmNewPasswordInput;
        [SerializeField] private TMP_Text forgotPasswordEmailErrorText;
        [SerializeField] private TMP_Text forgotPasswordOtpErrorText;
        [SerializeField] private TMP_Text newPasswordErrorText;
        [SerializeField] private TMP_Text confirmNewPasswordErrorText;

        private const string DeviceIdKey = "memesploding.device_id";
        private const string AccessTokenKey = "memesploding.access_token";
        private const string RefreshTokenKey = "memesploding.refresh_token";
        private const string UserIdKey = "memesploding.user_id";
        private const string UsernameKey = "memesploding.username";
        private const string AvatarUrlKey = "memesploding.avatar_url";
        private const string BioKey = "memesploding.bio";
        private const string LevelKey = "memesploding.level";
        private const string ScoreKey = "memesploding.score";
        private const string GuestProvider = "Guest";
        private const string DefaultBio = "Ready to play.";

        private readonly List<Popup[]> _popupHistory = new();
        private string _pendingForgotPasswordEmail;
        private string _pendingForgotPasswordOtp;
        private bool _isWaitingForServer;

        private void Awake()
        {
            EnsureInputModule();
            if (Instance != null && Instance != this)
                Destroy(gameObject);
            else
                Instance = this;

            ClearAllInlineErrors();
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
            ClearAllInlineErrors();
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
            ClearAllInlineErrors();
            ShowGroup(loginBackdrop);
        }

        public void OpenLoginWithEmail()
        {
            ClearAllInlineErrors();
            ShowGroup(loginWithEmailPopup);
        }

        public void OpenRegisterWithEmail()
        {
            ClearAllInlineErrors();
            ShowGroup(registerWithEmailPopup);
        }

        public void OpenForgotPasswordSendOtp()
        {
            ClearAllInlineErrors();
            ShowGroup(forgotPasswordSendOtpBackdrop, forgotPasswordSendOtpPopup);
        }

        public void OpenForgotPasswordNewPassword()
        {
            ClearAllInlineErrors();
            ShowGroup(forgotPasswordNewPasswordBackdrop);
        }

        public void OpenVerify()
        {
            ClearAllInlineErrors();
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
                ShowSuccess(response.message, "Guest login successful");
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
                ShowInlineError(loginEmailErrorText, "Google id token is required");
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
                ShowSuccess(response.message, "Login successful");
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

            if (!ValidateEmailPassword(email, password, loginEmailErrorText, loginPasswordErrorText))
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
                ShowSuccess(response.message, "Login successful");
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

            if (!ValidateEmailPassword(email, password, registerEmailErrorText, registerPasswordErrorText) ||
                !ValidatePasswordConfirmation(password, confirmPassword, registerConfirmPasswordErrorText))
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
                ShowSuccess(response.message, "Registration successful");
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
                ShowInlineError(forgotPasswordEmailErrorText, "A valid email is required");
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
                ShowInlineError(forgotPasswordOtpErrorText, "A 6-digit OTP code is required");
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
            if (!ValidatePasswordConfirmation(password, confirmPassword, confirmNewPasswordErrorText, newPasswordErrorText))
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
            ClearAllInlineErrors();
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
                var message = response?.message ?? fallbackMessage;
                ShowActiveInlineError(message);
                return false;
            }

            SaveAuthSession(response.data);
            return true;
        }

        private bool TryHandleSimpleSuccess(ApiResponse<object> response, string fallbackMessage)
        {
            if (response?.success != true)
            {
                var message = response?.message ?? fallbackMessage;
                ShowActiveInlineError(message);
                return false;
            }

            return true;
        }

        private void SaveAuthSession(AuthResponseDto auth)
        {
            var user = NormalizeUser(auth.User);

            PlayerPrefs.SetString(AccessTokenKey, auth.AccessToken ?? string.Empty);
            PlayerPrefs.SetString(RefreshTokenKey, auth.RefreshToken ?? string.Empty);

            PlayerPrefs.SetString(UserIdKey, user.Id);
            PlayerPrefs.SetString(UsernameKey, user.Username);
            PlayerPrefs.SetString(AvatarUrlKey, user.AvatarUrl ?? string.Empty);
            PlayerPrefs.SetString(BioKey, user.Bio);
            PlayerPrefs.SetInt(LevelKey, user.Level);
            PlayerPrefs.SetInt(ScoreKey, user.Score);

            PlayerPrefs.Save();
            GameManager.EnsureInstance().SetAuthenticatedUser(user, auth.AccessToken, auth.RefreshToken);
        }

        private MeDto NormalizeUser(MeDto user)
        {
            user ??= new MeDto();

            var deviceId = GetOrCreateDeviceId();
            var suffix = deviceId.Length >= 6 ? deviceId[^6..] : deviceId;

            if (string.IsNullOrWhiteSpace(user.Id))
                user.Id = $"guest-{deviceId}";

            if (string.IsNullOrWhiteSpace(user.Username))
                user.Username = $"Guest {suffix}";

            if (string.IsNullOrWhiteSpace(user.Provider))
                user.Provider = GuestProvider;

            if (string.IsNullOrWhiteSpace(user.Bio))
                user.Bio = DefaultBio;

            if (user.Level <= 0)
                user.Level = 1;

            if (user.CreatedAt == default)
                user.CreatedAt = DateTime.UtcNow;

            if (user.UpdatedAt == default)
                user.UpdatedAt = user.CreatedAt;

            return user;
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

        private bool ValidateEmailPassword(string email, string password, TMP_Text emailErrorText, TMP_Text passwordErrorText)
        {
            if (!IsValidEmail(email))
            {
                ShowInlineError(emailErrorText, "A valid email is required");
                return false;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowInlineError(passwordErrorText, "Password is required");
                return false;
            }

            return true;
        }

        private bool ValidatePasswordConfirmation(string password, string confirmPassword, TMP_Text confirmPasswordErrorText, TMP_Text passwordErrorText = null)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                ShowInlineError(passwordErrorText ?? confirmPasswordErrorText, "Password is required");
                return false;
            }

            if (password != confirmPassword)
            {
                ShowInlineError(confirmPasswordErrorText, "Passwords do not match");
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
            if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[WelcomeManager] {message}", this);
        }

        private void ShowActiveInlineError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[WelcomeManager] {message}", this);

            if (loginWithEmailPopup != null && loginWithEmailPopup.IsVisible)
            {
                ShowInlineError(loginPasswordErrorText, message);
                return;
            }

            if (registerWithEmailPopup != null && registerWithEmailPopup.IsVisible)
            {
                ShowInlineError(registerConfirmPasswordErrorText, message);
                return;
            }

            if (forgotPasswordSendOtpPopup != null && forgotPasswordSendOtpPopup.IsVisible)
            {
                ShowInlineError(forgotPasswordEmailErrorText, message);
                return;
            }

            if (verifyPopup != null && verifyPopup.IsVisible)
            {
                ShowInlineError(forgotPasswordOtpErrorText, message);
                return;
            }

            if (forgotPasswordNewPasswordBackdrop != null && forgotPasswordNewPasswordBackdrop.IsVisible)
                ShowInlineError(confirmNewPasswordErrorText, message);
        }

        private void ShowInlineError(TMP_Text target, string message)
        {
            ClearAllInlineErrors();

            if (target != null)
                target.text = message ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[WelcomeManager] {message}", this);
        }

        private void ClearAllInlineErrors()
        {
            ClearInlineError(loginEmailErrorText);
            ClearInlineError(loginPasswordErrorText);
            ClearInlineError(registerEmailErrorText);
            ClearInlineError(registerPasswordErrorText);
            ClearInlineError(registerConfirmPasswordErrorText);
            ClearInlineError(forgotPasswordEmailErrorText);
            ClearInlineError(forgotPasswordOtpErrorText);
            ClearInlineError(newPasswordErrorText);
            ClearInlineError(confirmNewPasswordErrorText);
        }

        private static void ClearInlineError(TMP_Text target)
        {
            if (target != null)
                target.text = string.Empty;
        }

        private void ShowSuccess(string message, string fallbackMessage)
        {
            UniversalPopup.ShowSuccess(string.IsNullOrWhiteSpace(message) ? fallbackMessage : message);
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

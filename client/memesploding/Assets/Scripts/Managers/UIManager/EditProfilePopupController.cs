using System;
using System.IO;
using Managers;
using Network.API.Models;
using Network.API.Services;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Managers.UIManager
{
    public class EditProfilePopupController : MonoBehaviour
    {
        private const string UsernameKey = "memesploding.username";
        private const string AvatarUrlKey = "memesploding.avatar_url";
        private const string BioKey = "memesploding.bio";
        private const string LevelKey = "memesploding.level";
        private const string ScoreKey = "memesploding.score";

        [Header("Scene References (optional; auto-bound if empty)")]
        [SerializeField] private Button openPopupButton;
        [SerializeField] private GameObject popupRoot;

        [Header("Popup UI")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button avatarButton;
        [SerializeField] private Image avatarImage;
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private TMP_InputField bioInput;

        private string _pendingAvatarUrl;
        private bool _isSaving;

        private void OnEnable()
        {
            AutoBind();

            if (openPopupButton != null)
            {
                openPopupButton.onClick.RemoveListener(Open);
                openPopupButton.onClick.AddListener(Open);
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(Confirm);
                confirmButton.onClick.AddListener(Confirm);
            }

            if (avatarButton != null)
            {
                avatarButton.onClick.RemoveListener(ChooseAvatar);
                avatarButton.onClick.AddListener(ChooseAvatar);
            }

            if (popupRoot != null)
                popupRoot.SetActive(false);
        }

        private void OnDisable()
        {
            if (openPopupButton != null)
                openPopupButton.onClick.RemoveListener(Open);

            if (confirmButton != null)
                confirmButton.onClick.RemoveListener(Confirm);

            if (avatarButton != null)
                avatarButton.onClick.RemoveListener(ChooseAvatar);
        }

        private Transform GetUiRoot()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            return canvas != null ? canvas.transform : transform.root;
        }

        private void AutoBind()
        {
            var root = GetUiRoot();

            // Open button: existing profile scene button labeled "edit profile".
            if (openPopupButton == null)
            {
                var t = transform.Find("JoinRoom Background/ContentPanel/EnterCode Box")
                        ?? root.Find("Profile/JoinRoom Background/ContentPanel/EnterCode Box");

                if (t != null)
                    openPopupButton = t.GetComponent<Button>();
            }

            if (popupRoot == null)
            {
                var popup = root.Find("EditProfilePopup");
                popupRoot = popup != null ? popup.gameObject : null;
            }

            if (popupRoot == null)
                return;

            var background = popupRoot.transform.Find("BackgroundPanel");
            var content = background != null ? background.Find("ContentPanel") : null;

            if (usernameInput == null)
                usernameInput = FindInput(content, "Username Textbox");

            if (emailInput == null)
                emailInput = FindInput(content, "Email Textbox");

            if (bioInput == null)
                bioInput = FindInput(content, "Bio TextBox");

            if (confirmButton == null)
            {
                var t = content != null ? content.Find("Confirm Button") : null;
                confirmButton = t != null ? t.GetComponent<Button>() : null;
            }

            if (avatarButton == null)
            {
                var t = background != null ? background.Find("Avatar") : null;
                avatarButton = t != null ? t.GetComponent<Button>() : null;
            }

            if (avatarImage == null && avatarButton != null)
                avatarImage = avatarButton.GetComponent<Image>();

            if (emailInput != null)
                emailInput.readOnly = true;
        }

        private static TMP_InputField FindInput(Transform contentPanel, string containerName)
        {
            var container = contentPanel != null ? contentPanel.Find(containerName) : null;
            if (container == null)
                return null;

            var input = container.Find("InputField (TMP)");
            return input != null ? input.GetComponent<TMP_InputField>() : null;
        }

        private async void Open()
        {
            AutoBind();

            if (popupRoot == null)
            {
                UniversalPopup.ShowError("Edit popup is missing in the scene.");
                return;
            }

            if (_isSaving)
                return;

            popupRoot.SetActive(true);
            SeedFromCache();

            var gm = GameManager.EnsureInstance();
            if (!gm.IsAuthenticated)
                return;

            var response = await UserService.Instance.GetMeProfileAsync(gm.AccessToken);
            if (response?.success == true && response.data != null)
            {
                ApplyFromApi(response.data);
                gm.SetPlayer(response.data);
                PersistCachedPlayer(response.data);
            }
        }

        private void SeedFromCache()
        {
            var player = GameManager.EnsureInstance().Player;

            if (usernameInput != null)
                usernameInput.text = player?.Username ?? string.Empty;

            if (bioInput != null)
                bioInput.text = player?.Bio ?? string.Empty;

            _pendingAvatarUrl = player?.AvatarUrl;
            ApplyAvatarPreview(_pendingAvatarUrl);
        }

        private void ApplyFromApi(MeDto me)
        {
            if (me == null)
                return;

            if (usernameInput != null)
                usernameInput.text = me.Username ?? string.Empty;

            if (emailInput != null)
                emailInput.text = me.Email ?? string.Empty;

            if (bioInput != null)
                bioInput.text = me.Bio ?? string.Empty;

            _pendingAvatarUrl = me.AvatarUrl;
            ApplyAvatarPreview(_pendingAvatarUrl);
        }

        private void ApplyAvatarPreview(string avatarUrl)
        {
            if (avatarImage == null)
                return;

            if (string.IsNullOrWhiteSpace(avatarUrl))
                return;

            if (AvatarDataUrl.TryDecodeToTexture(avatarUrl, out var texture))
            {
                avatarImage.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                avatarImage.gameObject.SetActive(true);
            }
        }

        private void ChooseAvatar()
        {
            if (_isSaving)
                return;

#if UNITY_EDITOR
            var path = EditorUtility.OpenFilePanel("Choose avatar", "", "png,jpg,jpeg");
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                var bytes = File.ReadAllBytes(path);

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes))
                {
                    UniversalPopup.ShowError("Invalid image file.");
                    return;
                }

                tex = ResizeToMax(tex, 256);

                var jpg = tex.EncodeToJPG(82);
                _pendingAvatarUrl = AvatarDataUrl.CreateJpegDataUrl(jpg);

                ApplyAvatarPreview(_pendingAvatarUrl);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EditProfilePopupController] Choose avatar failed: {ex.Message}");
                UniversalPopup.ShowError("Choose avatar failed.");
            }
#else
            UniversalPopup.ShowError("Avatar picker is only supported in the Unity Editor right now.");
#endif
        }

        private async void Confirm()
        {
            if (_isSaving)
                return;

            var gm = GameManager.EnsureInstance();
            if (!gm.IsAuthenticated)
            {
                UniversalPopup.ShowError("You must be logged in to update your profile.");
                return;
            }

            var username = (usernameInput != null ? usernameInput.text : string.Empty)?.Trim();
            var bio = (bioInput != null ? bioInput.text : string.Empty)?.Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                UniversalPopup.ShowError("Username is required.");
                return;
            }

            _isSaving = true;

            try
            {
                var request = new UpdateUserRequestDto
                {
                    Username = username,
                    Bio = bio,
                    AvatarUrl = _pendingAvatarUrl
                };

                var response = await UserService.Instance.UpdateMeProfileAsync(request, gm.AccessToken);
                if (response?.success != true || response.data == null)
                {
                    UniversalPopup.ShowError(string.IsNullOrWhiteSpace(response?.message) ? "Update profile failed." : response.message);
                    return;
                }

                gm.SetPlayer(response.data);
                PersistCachedPlayer(response.data);

                UniversalPopup.ShowSuccess("Profile updated.");
                popupRoot.SetActive(false);

                // Refresh ProfileUserInfoView if present
                var view = FindFirstObjectByType<UI.ProfileUserInfoView>();
                if (view != null)
                {
                    view.enabled = false;
                    view.enabled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EditProfilePopupController] Update profile failed: {ex.Message}");
                UniversalPopup.ShowError("Update profile failed.");
            }
            finally
            {
                _isSaving = false;
            }
        }

        private static void PersistCachedPlayer(MeDto user)
        {
            if (user == null)
                return;

            PlayerPrefs.SetString(UsernameKey, user.Username ?? string.Empty);
            PlayerPrefs.SetString(AvatarUrlKey, user.AvatarUrl ?? string.Empty);
            PlayerPrefs.SetString(BioKey, user.Bio ?? string.Empty);
            PlayerPrefs.SetInt(LevelKey, user.Level);
            PlayerPrefs.SetInt(ScoreKey, user.Score);
            PlayerPrefs.Save();
        }

        private static Texture2D ResizeToMax(Texture2D source, int maxSize)
        {
            if (source == null)
                return null;

            if (source.width <= maxSize && source.height <= maxSize)
                return source;

            var scale = Mathf.Min((float)maxSize / source.width, (float)maxSize / source.height);
            var w = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            var h = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));

            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;

            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;

                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                return tex;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }

    internal static class AvatarDataUrl
    {
        private const string PrefixJpeg = "data:image/jpeg;base64,";
        private const string PrefixPng = "data:image/png;base64,";

        public static string CreateJpegDataUrl(byte[] jpgBytes)
        {
            if (jpgBytes == null || jpgBytes.Length == 0)
                return string.Empty;

            return PrefixJpeg + Convert.ToBase64String(jpgBytes);
        }

        public static bool TryDecodeToTexture(string avatarUrl, out Texture2D texture)
        {
            texture = null;

            if (string.IsNullOrWhiteSpace(avatarUrl))
                return false;

            if (!avatarUrl.StartsWith(PrefixJpeg, StringComparison.OrdinalIgnoreCase) &&
                !avatarUrl.StartsWith(PrefixPng, StringComparison.OrdinalIgnoreCase))
                return false;

            var comma = avatarUrl.IndexOf(',');
            if (comma < 0 || comma >= avatarUrl.Length - 1)
                return false;

            try
            {
                var base64 = avatarUrl.Substring(comma + 1);
                var bytes = Convert.FromBase64String(base64);

                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                return texture.LoadImage(bytes);
            }
            catch
            {
                texture = null;
                return false;
            }
        }
    }
}

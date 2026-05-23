using System;
using Network.API.Models;
using Network.API.Services;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UIManager
{
    public class ProfileEditManager : MonoBehaviour
    {
        private const string UsernameKey = "memesploding.username";
        private const string AvatarUrlKey = "memesploding.avatar_url";
        private const string BioKey = "memesploding.bio";
        private const string LevelKey = "memesploding.level";
        private const string ScoreKey = "memesploding.score";

        [Header("Scene References (optional; auto-bound if empty)")]
        [SerializeField] private Button editProfileButton;
        [SerializeField] private UI.ProfileUserInfoView profileUserInfoView;

        [Header("Edit Fields")]
        [SerializeField] private bool allowAvatarEdit;

        private bool _isEditing;
        private bool _isSaving;

        private string _username;
        private string _bio;
        private string _avatarUrl;

        private void OnEnable()
        {
            AutoBind();

            if (editProfileButton != null)
            {
                editProfileButton.onClick.RemoveListener(ToggleEdit);
                editProfileButton.onClick.AddListener(ToggleEdit);
            }

            SeedFromCache();
        }

        private void OnDisable()
        {
            if (editProfileButton != null)
                editProfileButton.onClick.RemoveListener(ToggleEdit);
        }

        private void AutoBind()
        {
            if (profileUserInfoView == null)
                profileUserInfoView = GetComponent<UI.ProfileUserInfoView>();

            if (editProfileButton == null)
            {
                // Profile scene uses a reused button named "EnterCode Box" with label "edit profile"
                var t = transform.Find("JoinRoom Background/ContentPanel/EnterCode Box");
                if (t != null)
                    editProfileButton = t.GetComponent<Button>();
            }
        }

        private void SeedFromCache()
        {
            var player = GameManager.EnsureInstance().Player;
            _username = string.IsNullOrWhiteSpace(player?.Username) ? string.Empty : player.Username;
            _bio = string.IsNullOrWhiteSpace(player?.Bio) ? string.Empty : player.Bio;
            _avatarUrl = string.IsNullOrWhiteSpace(player?.AvatarUrl) ? string.Empty : player.AvatarUrl;
        }

        private void ToggleEdit()
        {
            if (_isSaving)
                return;

            _isEditing = !_isEditing;
            if (_isEditing)
                SeedFromCache();
        }

        private void OnGUI()
        {
            if (!_isEditing)
                return;

            const float width = 720f;
            const float height = 420f;

            var x = (Screen.width - width) * 0.5f;
            var y = (Screen.height - height) * 0.5f;
            var rect = new Rect(x, y, width, height);

            GUI.Box(rect, string.Empty);

            var content = new Rect(rect.x + 20f, rect.y + 20f, rect.width - 40f, rect.height - 40f);

            GUILayout.BeginArea(content);

            GUILayout.Label("Edit Profile");
            GUILayout.Space(10f);

            GUILayout.Label("Username");
            _username = GUILayout.TextField(_username ?? string.Empty);

            GUILayout.Space(10f);
            GUILayout.Label("Bio");
            _bio = GUILayout.TextArea(_bio ?? string.Empty, GUILayout.Height(120f));

            if (allowAvatarEdit)
            {
                GUILayout.Space(10f);
                GUILayout.Label("Avatar URL");
                _avatarUrl = GUILayout.TextField(_avatarUrl ?? string.Empty);
            }

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();

            GUI.enabled = !_isSaving;
            if (GUILayout.Button("Cancel", GUILayout.Height(36f)))
            {
                _isEditing = false;
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }

            if (GUILayout.Button(_isSaving ? "Saving..." : "Save", GUILayout.Height(36f)))
            {
                SaveAsync();
            }
            GUI.enabled = true;

            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private async void SaveAsync()
        {
            if (_isSaving)
                return;

            var gameManager = GameManager.EnsureInstance();
            if (!gameManager.IsAuthenticated)
            {
                UniversalPopup.ShowError("You must be logged in to update your profile.");
                return;
            }

            var username = (_username ?? string.Empty).Trim();
            var bio = (_bio ?? string.Empty).Trim();
            var avatarUrl = (_avatarUrl ?? string.Empty).Trim();

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
                    AvatarUrl = allowAvatarEdit ? avatarUrl : null
                };

                var response = await UserService.Instance.UpdateMeProfileAsync(request, gameManager.AccessToken);
                if (response?.success != true || response.data == null)
                {
                    UniversalPopup.ShowError(string.IsNullOrWhiteSpace(response?.message) ? "Update profile failed." : response.message);
                    return;
                }

                gameManager.SetPlayer(response.data);
                PersistCachedPlayer(response.data);

                UniversalPopup.ShowSuccess("Profile updated.");
                _isEditing = false;

                // Force refresh of ProfileUserInfoView (it will re-apply cached player and re-fetch from API)
                if (profileUserInfoView != null)
                {
                    profileUserInfoView.enabled = false;
                    profileUserInfoView.enabled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProfileEditManager] Update profile failed: {ex.Message}");
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
    }
}

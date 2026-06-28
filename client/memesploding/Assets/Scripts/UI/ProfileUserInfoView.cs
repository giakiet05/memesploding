using System;
using System.Collections;
using Managers;
using Network.API.Models;
using Network.API.Services;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace UI
{
    public class ProfileUserInfoView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private TextMeshProUGUI emailText;
        [SerializeField] private TextMeshProUGUI providerText;
        [SerializeField] private TextMeshProUGUI bioText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI userIdText;
        [SerializeField] private TextMeshProUGUI createdAtText;
        [SerializeField] private TextMeshProUGUI updatedAtText;
        [SerializeField] private Image avatarImage;

        private Coroutine _avatarLoadRoutine;

        private async void OnEnable()
        {
            var gameManager = GameManager.EnsureInstance();
            ApplyCachedPlayer(gameManager);

            if (!gameManager.IsAuthenticated)
                return;

            var response = await UserService.Instance.GetMeProfileAsync(gameManager.AccessToken);
            if (response?.success != true || response.data == null)
            {
                Debug.LogWarning($"[ProfileUserInfoView] Unable to load profile: {response?.message}", this);
                return;
            }

            gameManager.SetPlayer(response.data);
            Apply(response.data);
        }

        private void ApplyCachedPlayer(GameManager gameManager)
        {
            var player = gameManager.Player;

            if (usernameText != null)
                usernameText.text = string.IsNullOrWhiteSpace(player?.Username) ? "Player" : player.Username;

            if (emailText != null)
                emailText.text = "No email";

            if (providerText != null)
                providerText.text = "Guest";

            if (bioText != null)
                bioText.text = string.IsNullOrWhiteSpace(player?.Bio) ? "Ready to play." : player.Bio;

            if (levelText != null)
                levelText.text = player == null ? "Level 0" : $"Level {player.Level}";

            if (userIdText != null)
                userIdText.text = string.IsNullOrWhiteSpace(player?.ID) ? "Unknown" : player.ID;

            LoadAvatar(player?.AvatarUrl);
        }

        private void Apply(MeDto user)
        {
            if (usernameText != null)
                usernameText.text = string.IsNullOrWhiteSpace(user.Username) ? "Player" : user.Username;

            if (emailText != null)
                emailText.text = string.IsNullOrWhiteSpace(user.Email) ? "No email" : user.Email;

            if (providerText != null)
                providerText.text = string.IsNullOrWhiteSpace(user.Provider) ? "Guest" : user.Provider;

            if (bioText != null)
                bioText.text = string.IsNullOrWhiteSpace(user.Bio) ? "Ready to play." : user.Bio;

            if (levelText != null)
                levelText.text = $"Level {user.Level}";

            if (userIdText != null)
                userIdText.text = string.IsNullOrWhiteSpace(user.Id) ? "Unknown" : user.Id;

            if (createdAtText != null)
                createdAtText.text = $"Created at: {FormatDate(user.CreatedAt)}";

            if (updatedAtText != null)
                updatedAtText.text = $"Last Update: {FormatDate(user.UpdatedAt)}";

            LoadAvatar(user.AvatarUrl);
        }

        private static string FormatDate(DateTime dateTime)
        {
            return dateTime == default ? string.Empty : dateTime.ToLocalTime().ToString("dd/MM/yyyy");
        }

        private void LoadAvatar(string avatarUrl)
        {
            if (_avatarLoadRoutine != null)
                StopCoroutine(_avatarLoadRoutine);

            if (avatarImage == null || string.IsNullOrWhiteSpace(avatarUrl))
                return;

            if (TryApplyDataUrlAvatar(avatarUrl))
                return;

            _avatarLoadRoutine = StartCoroutine(LoadAvatarRoutine(avatarUrl));
        }

        private bool TryApplyDataUrlAvatar(string avatarUrl)
        {
            if (string.IsNullOrWhiteSpace(avatarUrl) || !avatarUrl.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                return false;

            var comma = avatarUrl.IndexOf(',');
            if (comma < 0 || comma >= avatarUrl.Length - 1)
                return false;

            try
            {
                var base64 = avatarUrl.Substring(comma + 1);
                var bytes = Convert.FromBase64String(base64);

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes))
                    return false;

                avatarImage.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                avatarImage.gameObject.SetActive(true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private IEnumerator LoadAvatarRoutine(string avatarUrl)
        {
            using var request = UnityWebRequestTexture.GetTexture(avatarUrl);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success || avatarImage == null)
                yield break;

            var texture = DownloadHandlerTexture.GetContent(request);
            avatarImage.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            avatarImage.gameObject.SetActive(true);
        }
    }
}

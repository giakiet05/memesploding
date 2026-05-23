using System;
using System.Collections;
using Network.API.Models;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace UI
{
    public class LeaderboardEntryView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI rankingText;
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Image avatarImage;

        private Coroutine _avatarLoadRoutine;

        private void Awake()
        {
            AutoBind();
        }

        public void Bind(UserProfileDto user, int rank)
        {
            AutoBind();

            if (rankingText != null)
                rankingText.text = rank > 0 ? rank.ToString() : "-";

            if (usernameText != null)
                usernameText.text = string.IsNullOrWhiteSpace(user?.Username) ? "Player" : user.Username;

            if (scoreText != null)
                scoreText.text = user == null ? "0 pts" : $"{user.Score} pts";

            if (levelText != null)
                levelText.text = user == null ? "Level 1" : $"Level {Mathf.Max(1, user.Level)}";

            LoadAvatar(user?.AvatarUrl);
        }

        protected virtual void AutoBind()
        {
            rankingText ??= transform.Find("Ranking Number")?.GetComponent<TextMeshProUGUI>();
            usernameText ??= transform.Find("Username")?.GetComponent<TextMeshProUGUI>();
            scoreText ??= transform.Find("Username (1)")?.GetComponent<TextMeshProUGUI>();
            levelText ??= transform.Find("LevelStatus")?.GetComponent<TextMeshProUGUI>();
            avatarImage ??= transform.Find("Avatar Cover/Avatar")?.GetComponent<Image>();
        }

        protected void LoadAvatar(string avatarUrl)
        {
            if (_avatarLoadRoutine != null)
                StopCoroutine(_avatarLoadRoutine);

            if (avatarImage == null)
                return;

            avatarImage.gameObject.SetActive(false);
            if (string.IsNullOrWhiteSpace(avatarUrl))
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

                ApplyTexture(texture);
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

            if (request.result != UnityWebRequest.Result.Success)
                yield break;

            var texture = DownloadHandlerTexture.GetContent(request);
            ApplyTexture(texture);
        }

        private void ApplyTexture(Texture2D texture)
        {
            if (texture == null || avatarImage == null)
                return;

            avatarImage.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            avatarImage.gameObject.SetActive(true);
        }
    }
}

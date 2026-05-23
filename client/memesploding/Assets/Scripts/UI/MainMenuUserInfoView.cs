using System.Collections;
using Managers;
using Models;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace UI
{
    public class MainMenuUserInfoView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private TextMeshProUGUI levelStatusText;
        [SerializeField] private Image avatarImage;

        private Coroutine _avatarLoadRoutine;

        private void OnEnable()
        {
            Load();
        }

        public void Load()
        {
            var player = GameManager.EnsureInstance().Player;
            Apply(player);
        }

        private void Apply(Player player)
        {
            if (usernameText != null)
                usernameText.text = string.IsNullOrWhiteSpace(player?.Username) ? "Player" : player.Username;

            if (levelStatusText != null)
                levelStatusText.text = player == null ? "Level 1" : $"Level {Mathf.Max(1, player.Level)}";

            LoadAvatar(player?.AvatarUrl);
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
            if (string.IsNullOrWhiteSpace(avatarUrl) || !avatarUrl.StartsWith("data:image", System.StringComparison.OrdinalIgnoreCase))
                return false;

            var comma = avatarUrl.IndexOf(',');
            if (comma < 0 || comma >= avatarUrl.Length - 1)
                return false;

            try
            {
                var base64 = avatarUrl.Substring(comma + 1);
                var bytes = System.Convert.FromBase64String(base64);

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

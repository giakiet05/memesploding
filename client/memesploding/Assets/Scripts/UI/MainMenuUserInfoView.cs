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

            _avatarLoadRoutine = StartCoroutine(LoadAvatarRoutine(avatarUrl));
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

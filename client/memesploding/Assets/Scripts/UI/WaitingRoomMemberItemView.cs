using Network.API.Models;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace UI
{
    public class WaitingRoomMemberItemView : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image avatarImage;
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Sprite otherPlayerBackground;
        [SerializeField] private Sprite readyPlayerBackground;

        private Coroutine _avatarLoadRoutine;
        private Sprite _hostBackground;

        private void Awake()
        {
            AutoBind();

            if (backgroundImage != null && _hostBackground == null)
                _hostBackground = backgroundImage.sprite;
        }

        public void Bind(RoomParticipantDto participant, bool isHost, bool isLocal)
        {
            AutoBind();

            if (participant == null)
                return;

            if (backgroundImage != null)
            {
                if (isHost)
                {
                    backgroundImage.sprite = _hostBackground ?? backgroundImage.sprite;
                }
                else
                {
                    backgroundImage.sprite = participant.IsReady
                        ? readyPlayerBackground ?? otherPlayerBackground ?? backgroundImage.sprite
                        : otherPlayerBackground ?? backgroundImage.sprite;
                }
            }

            if (usernameText != null)
            {
                var displayName = string.IsNullOrWhiteSpace(participant.Nickname) ? "Player" : participant.Nickname;
                usernameText.text = isLocal ? $"{displayName} (You)" : displayName;
            }

            if (statusText != null)
                statusText.text = isHost ? "HOST" : participant.IsReady ? "READY" : "WAITING";

            LoadAvatar(participant.AvatarUrl);
        }

        private void AutoBind()
        {
            backgroundImage ??= GetComponent<Image>();
            avatarImage ??= transform.Find("AvatarCover/Avatar")?.GetComponent<Image>();
            usernameText ??= transform.Find("Username")?.GetComponent<TextMeshProUGUI>();
            statusText ??= transform.Find("Image/Username (1)")?.GetComponent<TextMeshProUGUI>();
        }

        private void LoadAvatar(string avatarUrl)
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
                var texture = new Texture2D(2, 2);
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
            if (texture == null)
                yield break;

            avatarImage.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            avatarImage.gameObject.SetActive(true);
        }
    }
}

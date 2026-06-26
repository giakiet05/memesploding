using System;
using System.Collections;
using Events;
using Events.GameEvents;
using Gameplay.Card;
using Managers;
using Network.Websocket;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using EventType = Events.EventType;
using Random = UnityEngine.Random;

namespace UI.Gameplay
{
    public class OpponentProfile : MonoBehaviour
    {
        #region References

        [Header("Layout")]
        [SerializeField] private RectTransform spawnPoint;
        [SerializeField] private RectTransform playArea;

        [Header("UI Elements")]
        [SerializeField] private Image selectArrow;
        [SerializeField] private TextMeshProUGUI cardCounterText;
        [SerializeField] private Image profileImage;
        [SerializeField] private TextMeshProUGUI nicknameText;

        #endregion

        #region Animation Settings

        [Header("Card Animation")]
        [SerializeField] private float jumpHeight = 150f;
        [SerializeField] private float duration = 0.5f;

        [Header("Other Animation")]
        [SerializeField] private GlowImage glowImage;

        #endregion

        public event Action<string> OnProfileClickedEvent;

        private string _userID;
        private Coroutine _avatarLoadRoutine;
        private TextMeshProUGUI _eliminatedLabel;
        private bool _isEliminated;

        private void Awake()
        {
            spawnPoint ??= transform as RectTransform;
            selectArrow ??= transform.Find("SelecterArrow")?.GetComponent<Image>();
            cardCounterText ??= transform.Find("CardCounter/Text (TMP)")?.GetComponent<TextMeshProUGUI>();
            profileImage ??= transform.Find("Image")?.GetComponent<Image>();
            glowImage ??= transform.Find("GlowImage")?.GetComponent<GlowImage>();
            nicknameText ??= transform.Find("NicknamePlate/NicknameText")?.GetComponent<TextMeshProUGUI>();

            if (nicknameText == null)
                Debug.LogWarning("[OpponentProfile] NicknameText reference is missing on the prefab.");
        }

        private void Start()
        {
            EventBus.Subscribe<TurnStartEventPayload>(EventType.TurnStart, OnTurnStart);
            EventBus.Subscribe<TurnEndEventPayload>(EventType.TurnEnd, OnTurnEnd);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<TurnStartEventPayload>(EventType.TurnStart, OnTurnStart);
            EventBus.Unsubscribe<TurnEndEventPayload>(EventType.TurnEnd, OnTurnEnd);
        }

        public void Init(WsPlayerPublicStateDto player, string avatarUrl, bool useDefaultAvatar)
        {
            if (player == null)
                return;

            _userID = player.userId;
            LoadAvatar(avatarUrl, useDefaultAvatar);

            if (cardCounterText != null)
                cardCounterText.text = Mathf.Max(0, player.handCount).ToString("D2");

            if (nicknameText != null && !string.IsNullOrWhiteSpace(player.nickname))
                nicknameText.text = player.nickname;
        }

        public void OnTurnStart(TurnStartEventPayload payload)
        {
            if (payload == null || payload.UserID != _userID)
            {
                SetTurnIndicator(false);
                return;
            }

            SetTurnIndicator(true);
        }

        private void OnTurnEnd(TurnEndEventPayload payload)
        {
            if (payload != null && payload.UserID == _userID)
                SetTurnIndicator(false);
        }

        public void SetCurrentTurn(bool isCurrentTurn)
        {
            SetTurnIndicator(isCurrentTurn);
        }

        public void SetPlayArea(RectTransform targetPlayArea)
        {
            playArea = targetPlayArea;
        }

        public void PlayCard(string cardName)
        {
            StartCoroutine(PlayCardRoutine(cardName));
        }

        private IEnumerator PlayCardRoutine(string cardName)
        {
            if (playArea == null)
            {
                Debug.LogError($"[OpponentProfile] PlayArea reference is missing for user '{_userID}'.");
                yield break;
            }

            if (CardManager.Instance == null)
            {
                Debug.LogError($"[OpponentProfile] CardManager instance is missing while playing '{cardName}'.");
                yield break;
            }

            PlayableCard card = CardManager.Instance.CreatePlayableCard(cardName, playArea.transform);
            if (card == null)
            {
                Debug.LogWarning($"[OpponentProfile] Unable to create playable card for '{cardName}'.");
                yield break;
            }

            RectTransform rect = card.RectTransform;
            if (rect == null)
            {
                Debug.LogError($"[OpponentProfile] Spawned card '{cardName}' is missing RectTransform.");
                yield break;
            }

            rect.localScale = Vector3.zero;
            rect.position = spawnPoint != null ? spawnPoint.position : transform.position;

            Vector2 target = RandomPosition(playArea);

            Vector2 start = rect.anchoredPosition;

            float time = 0f;

            var effectiveDuration = Mathf.Max(0.8f, duration);
            while (time < effectiveDuration)
            {
                float t = time / effectiveDuration;

                Vector2 pos = Vector2.Lerp(start, target, t);

                float arc = Mathf.Sin(t * Mathf.PI) * jumpHeight;
                pos.y += arc;

                rect.anchoredPosition = pos;

                rect.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);

                time += Time.deltaTime;
                yield return null;
            }

            rect.anchoredPosition = target;
            rect.localScale = Vector3.one;

            //Publish event
            //TODO: Change player name
            CardPlayedEventPayload payload = new CardPlayedEventPayload(card, _userID);
            EventBus.Publish(EventType.CardPlayedEvent, payload);
        }

        private Vector2 RandomPosition(RectTransform rect)
        {
            float width = rect.rect.width;
            float height = rect.rect.height;

            float x = Random.Range(-width * 0.5f, width * 0.5f);
            float y = Random.Range(-height * 0.5f, height * 0.5f);

            return new Vector2(x, y);
        }
        public void SetArrowActive(bool active)
        {
            if (selectArrow != null)
                selectArrow.gameObject.SetActive(active);
        }

        public void SetEliminated(bool eliminated)
        {
            if (_isEliminated == eliminated)
                return;

            _isEliminated = eliminated;
            SetArrowActive(false);
            SetTurnIndicator(false);
            if (profileImage != null)
                profileImage.color = eliminated ? new Color(0.28f, 0.28f, 0.28f, 1f) : Color.white;

            if (_eliminatedLabel == null)
            {
                var labelObject = new GameObject("EliminatedLabel", typeof(RectTransform));
                var rect = (RectTransform)labelObject.transform;
                rect.SetParent(transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                _eliminatedLabel = labelObject.AddComponent<TextMeshProUGUI>();
                _eliminatedLabel.text = "DEAD";
                _eliminatedLabel.fontSize = 30f;
                _eliminatedLabel.fontStyle = FontStyles.Bold;
                _eliminatedLabel.color = Color.red;
                _eliminatedLabel.alignment = TextAlignmentOptions.Center;
            }

            _eliminatedLabel.gameObject.SetActive(eliminated);
            if (eliminated)
                StartCoroutine(PlayEliminationAnimation());
        }

        private IEnumerator PlayEliminationAnimation()
        {
            var rect = transform as RectTransform;
            if (rect == null)
                yield break;

            var origin = rect.anchoredPosition;
            var originScale = rect.localScale;
            for (var elapsed = 0f; elapsed < 0.8f; elapsed += Time.unscaledDeltaTime)
            {
                var t = elapsed / 0.8f;
                var strength = Mathf.Lerp(18f, 0f, t);
                rect.anchoredPosition = origin + Random.insideUnitCircle * strength;
                rect.localScale = originScale * (1f + Mathf.Sin(t * Mathf.PI * 3f) * (1f - t) * 0.2f);
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 6f) * (1f - t) * 8f);
                yield return null;
            }
            rect.anchoredPosition = origin;
            rect.localScale = originScale;
            rect.localRotation = Quaternion.identity;
        }
        public void OnProfileClicked()
        {
            if (selectArrow != null && selectArrow.gameObject.activeSelf)
            {
                OnProfileClickedEvent?.Invoke(_userID);
                Debug.Log($"Profile clicked: {_userID}");
            }
        }

        private void SetTurnIndicator(bool isActive)
        {
            if (glowImage != null)
                glowImage.gameObject.SetActive(isActive);
        }

        private void LoadAvatar(string avatarUrl, bool useDefaultAvatar)
        {
            if (_avatarLoadRoutine != null)
                StopCoroutine(_avatarLoadRoutine);

            if (profileImage == null)
                return;

            if (useDefaultAvatar || string.IsNullOrWhiteSpace(avatarUrl))
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

                profileImage.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
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

            if (request.result != UnityWebRequest.Result.Success || profileImage == null)
                yield break;

            var texture = DownloadHandlerTexture.GetContent(request);
            if (texture == null)
                yield break;

            profileImage.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
    }
}

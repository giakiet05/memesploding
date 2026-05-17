using System;
using System.Collections;
using Events;
using Events.GameEvents;
using Gameplay.Card;
using Managers;
using Network.Websocket;
using TMPro;
using UnityEngine;
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

        [Header("UI Elements")]
        [SerializeField] private Image selectArrow;
        [SerializeField] private TextMeshProUGUI cardCounterText;
        [SerializeField] private Image profileImage;

        #endregion

        #region Animation Settings

        [Header("Card Animation")]
        [SerializeField] private float jumpHeight = 150f;
        [SerializeField] private float duration = 0.5f;

        [Header("Other Animation")]
        [SerializeField] private GlowImage glowImage;

        #endregion

        public event Action<string> OnProfileClickedEvent;

        //TODO: Assign play area for opponent to play card in
        private RectTransform playArea;

        //For testing only
        [SerializeField] private bool autoPlay = false;

        private string _userID;

        private bool _isActive;

        public OpponentProfile(RectTransform playArea)
        {
            this.playArea = playArea;
        }

        private void Start()
        {
            EventBus.Subscribe<TurnStartEventPayload>(EventType.TurnStart, OnTurnStart);

            if (autoPlay)
                StartCoroutine(AutoPlay());
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<TurnStartEventPayload>(EventType.TurnStart, OnTurnStart);
        }

        private IEnumerator AutoPlay()
        {
            while (true)
            {
                PlayCard("DEFUSE");
                yield return new WaitForSeconds(2f);
            }
        }

        public void Init(WsPlayerPublicStateDto player, Sprite profileSprite)
        {
            _userID = player.userId;
            profileImage.sprite = profileSprite;
        }

        public void OnTurnStart(TurnStartEventPayload payload)
        {
            if (payload.UserID != _userID)
            {
                glowImage.gameObject.SetActive(false);
                return;
            }

            //TODO: Add current turn effect for opponent
            glowImage.gameObject.SetActive(true);
        }

        public void PlayCard(string cardName)
        {
            StartCoroutine(PlayCardRoutine(cardName));
        }

        private IEnumerator PlayCardRoutine(string cardName)
        {
            PlayableCard card = CardManager.Instance.CreatePlayableCard(cardName, playArea.transform);

            RectTransform rect = card.RectTransform;
            rect.localScale = Vector3.zero;
            rect.position = spawnPoint.position;

            Vector2 target = RandomPosition(playArea);

            Vector2 start = rect.anchoredPosition;

            float time = 0f;

            while (time < duration)
            {
                float t = time / duration;

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
            CardPlayedEventPayload payload = new CardPlayedEventPayload(card, "Vak0506");
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
            selectArrow.gameObject.SetActive(active);
        }
        public void OnProfileClicked()
        {
            
            if (selectArrow.gameObject.activeSelf)
            {
                OnProfileClickedEvent?.Invoke(_userID);
                Debug.Log($"Profile clicked: {_userID}");
            }
        }
    }
}

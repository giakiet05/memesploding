using Events;
using Events.GameEvents;
using Gameplay.Card;
using Managers;
using Network.Websocket;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using EventType = Events.EventType;
using Random = UnityEngine.Random;

namespace UI
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

        #endregion
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

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive == value)
                    return;

                _isActive = value;

                OnActiveChanged();
            }
        }

        private void Start()
        {
            if (autoPlay)
                StartCoroutine(AutoPlay());
            IsActive = false;
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

        private void OnActiveChanged()
        {
            //TODO: Add active player effect
            Debug.Log($"Player {_userID} is active. Remember to add effect");
        }
    }
}

using Events;
using Events.GameEvents;
using Gameplay.Card;
using UnityEngine;
using EventType = Events.EventType;

namespace Gameplay
{
    public class BoardArea : MonoBehaviour
    {
        [SerializeField] private RectTransform playableArea;
        [SerializeField] private Vector2 randomPadding = new Vector2(70f, 100f);

        private BaseCard _newestCard;

        public RectTransform PlayableArea => playableArea != null ? playableArea : (RectTransform)transform;

        private void Start()
        {
            _newestCard = null;

            EventBus.Subscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
        }

        //Handle when opponent play a card
        private void OnCardPlayed(CardPlayedEventPayload payload)
        {
            if (_newestCard != null)
                _newestCard.SetNewest(false);
            _newestCard = payload.PlayedCard;
        }

        public Vector2 GetRandomCardPosition()
        {
            Rect rect = PlayableArea.rect;

            float minX = rect.xMin + randomPadding.x;
            float maxX = rect.xMax - randomPadding.x;
            float minY = rect.yMin + randomPadding.y;
            float maxY = rect.yMax - randomPadding.y;

            if (minX > maxX)
                minX = maxX = rect.center.x;

            if (minY > maxY)
                minY = maxY = rect.center.y;

            return new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
        }
    }
}

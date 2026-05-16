using Events;
using Events.GameEvents;
using System.Collections;
using System.Collections.Generic;
using Gameplay.Card;
using Managers;
using UnityEngine;
using UnityEngine.UI;
using EventType = Events.EventType;

namespace Gameplay
{
    public class HandLayout : MonoBehaviour
    {
        public float baseSpacing = 120f;
        public float minSpacing = 60f;
        public float selectedYOffset = 80f;

        public float baseFanAngle = 30f;
        public float minFanAngle = 6f;

        public float cardMoveSpeed = 12f;
        public float reorderSpeed = 8f;
        public float scrollSpeed = 10f;

        public float extraSpace = 250f;

        [Header("Play Animation")]
        [SerializeField] private BoardArea boardArea;
        [SerializeField, Min(0.01f)] private float playAnimationSpeed = 1f;
        [SerializeField] private float scrollToCardDuration = 0.25f;
        [SerializeField] private float moveToBoardDuration = 0.35f;
        [SerializeField, Min(0f)] private float effectPauseDuration = 0.5f;

        private readonly List<BaseCard> _slots = new();
        private readonly List<BaseCard> _targetOrder = new();
        private readonly Dictionary<string, BaseCard> _cardById = new();
        private readonly Dictionary<string, int> _previousSlot = new();

        private RectTransform _rect;
        private ScrollRect _scrollRect;

        private float _reorderTimer;
        private bool _isPlayingSelectedCards;

        void Start()
        {
            _rect = GetComponent<RectTransform>();
            _scrollRect = GetComponentInParent<ScrollRect>();

            // Get the parent ScrollRect and boost sensitivity
            ScrollRect sr = GetComponentInParent<ScrollRect>();
            if (sr != null)
                sr.scrollSensitivity = scrollSpeed;

            if (boardArea == null)
                boardArea = FindFirstObjectByType<BoardArea>();

            RegisterExistingCards();

            //Event
            EventBus.Subscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
        }

        private void OnCardPlayed(CardPlayedEventPayload payload)
        {
            BaseCard card = GetCardById(payload.PlayedCard.Id);

            if (card != null)
                RemoveCard(card);
        }

        void LateUpdate()
        {
            int count = _slots.Count;
            if (count == 0)
                return;

            float cardWidth = 0f;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i])
                {
                    cardWidth = _slots[i].RectTransform.rect.width;
                    break;
                }
            }

            float dynamicSpacing = Mathf.Max(minSpacing, baseSpacing / Mathf.Max(1, count * 0.25f));
            float dynamicFan = Mathf.Max(minFanAngle, baseFanAngle / Mathf.Max(1, count * 0.3f));

            for (int i = 0; i < count; i++)
            {
                BaseCard card = _slots[i];
                if (!card)
                    continue;

                float t = count > 1 ? (float)i / (count - 1) : 0.5f;
                float angle = Mathf.Lerp(-dynamicFan / 2f, dynamicFan / 2f, t);
                float x = (i - (count - 1) / 2f) * dynamicSpacing;

                Vector2 targetPos = new Vector2(x, GetCardTargetY(card));

                card.RectTransform.anchoredPosition =
                    Vector2.Lerp(card.RectTransform.anchoredPosition, targetPos, Time.deltaTime * cardMoveSpeed);

                Quaternion targetRot = Quaternion.Euler(0, 0, -angle);

                card.RectTransform.localRotation =
                    Quaternion.Lerp(card.RectTransform.localRotation, targetRot, Time.deltaTime * cardMoveSpeed);
            }

            float pivotSpan = (count - 1) * dynamicSpacing;
            float totalWidth = pivotSpan + cardWidth + extraSpace;

            // Center the pivot so cards span correctly
            _rect.sizeDelta = new Vector2(totalWidth, _rect.sizeDelta.y);
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.anchorMin = new Vector2(0.5f, 0.5f);
            _rect.anchorMax = new Vector2(0.5f, 0.5f);

            _reorderTimer += Time.deltaTime;

            if (_reorderTimer >= 1f / reorderSpeed)
            {
                _reorderTimer = 0f;
                StepTowardTarget();
            }
        }

        void StepTowardTarget()
        {
            if (_targetOrder.Count == 0 || _slots.Count == 0)
                return;

            // Ensure both lists are in sync
            if (_targetOrder.Count != _slots.Count)
            {
                UpdateVisual();
                return;
            }

            for (int i = 0; i < _targetOrder.Count; i++)
            {
                if (_slots[i] == _targetOrder[i])
                    continue;

                int j = _slots.IndexOf(_targetOrder[i]);

                if (j > i && j < _slots.Count)
                {
                    BaseCard temp = _slots[j - 1];
                    _slots[j - 1] = _slots[j];
                    _slots[j] = temp;

                    if (_slots[j - 1] != null)
                        _slots[j - 1].transform.SetSiblingIndex(j - 1);

                    if (_slots[j] != null)
                        _slots[j].transform.SetSiblingIndex(j);

                    SaveSlot(_slots[j - 1], j - 1);
                    SaveSlot(_slots[j], j);

                    RefreshRenderOrder();
                    return;
                }
            }
        }

        void SaveSlot(BaseCard card, int index)
        {
            if (card == null)
                return;

            _previousSlot[card.Id] = index;
        }

        public void AddCard(BaseCard card)
        {
            card.RectTransform.SetParent(transform, false);

            _slots.Add(card);
            _previousSlot[card.Id] = _slots.Count - 1;
            _cardById[card.Id] = card;

            RefreshRenderOrder();
        }

        public bool RemoveCardById(string cardId)
        {
            return RemoveCardById(cardId, true);
        }

        public bool DetachCard(BaseCard card)
        {
            if (card == null)
                return false;

            return RemoveCardById(card.Id, false);
        }

        private bool RemoveCardById(string cardId, bool destroyCard)
        {
            if (string.IsNullOrEmpty(cardId))
                return false;

            if (!_cardById.TryGetValue(cardId, out var card))
            {
                return false;
            }

            int index = _slots.IndexOf(card);
            if (index >= 0)
            {
                _slots.RemoveAt(index);
            }

            _cardById.Remove(cardId);
            _previousSlot.Remove(cardId);

            _targetOrder.Remove(card);

            if (card != null && destroyCard)
            {
                Destroy(card.gameObject);
            }

            RefreshRenderOrder();
            UpdateVisual();

            return true;
        }

        public bool RemoveCard(BaseCard card)
        {
            if (card == null)
                return false;

            return RemoveCardById(card.Id);
        }

        public void UpdateVisual()
        {
            _targetOrder.Clear();

            foreach (var c in _slots)
                if (c != null)
                    _targetOrder.Add(c);

            _targetOrder.Sort((a, b) =>
            {
                int cmp = string.Compare(a.Data.cardCode, b.Data.cardCode, System.StringComparison.Ordinal);

                if (cmp != 0)
                    return cmp;

                return _previousSlot[a.Id].CompareTo(_previousSlot[b.Id]);
            });
        }

        public List<PlayableCard> GetSelectedPlayableCards()
        {
            List<PlayableCard> selectedCards = new();

            foreach (var card in _slots)
            {
                if (card is not PlayableCard playableCard)
                    continue;

                if (playableCard.IsSelectedForPlay)
                    selectedCards.Add(playableCard);
            }

            return selectedCards;
        }

        public void NotifyCardSelectionChanged()
        {
            RefreshRenderOrder();
        }

        public void PlaySelectedCards()
        {
            if (_isPlayingSelectedCards)
                return;

            StartCoroutine(PlaySelectedCardsRoutine());
        }

        private IEnumerator PlaySelectedCardsRoutine()
        {
            if (boardArea == null)
                boardArea = FindFirstObjectByType<BoardArea>();

            if (boardArea == null)
            {
                Debug.LogError("BoardArea is not assigned for HandLayout", this);
                yield break;
            }

            List<PlayableCard> selectedCards = GetSelectedPlayableCards();
            if (selectedCards.Count == 0)
                yield break;

            _isPlayingSelectedCards = true;

            foreach (PlayableCard card in selectedCards)
            {
                if (card == null)
                    continue;

                yield return ScrollCardToCenter(card);

                DetachCard(card);
                card.DisableDrag();

                Vector2 boardPosition = boardArea.GetRandomCardPosition();
                yield return card.PlayToBoard(
                    boardArea.PlayableArea,
                    boardPosition,
                    GetScaledDuration(moveToBoardDuration));

                CardPlayedEventPayload payload = new CardPlayedEventPayload(card, GameManager.Instance.Player.ID);
                EventBus.Publish(EventType.CardPlayedEvent, payload);

                if (effectPauseDuration > 0f)
                    yield return new WaitForSeconds(GetScaledDuration(effectPauseDuration));
            }

            UpdateVisual();
            _isPlayingSelectedCards = false;
        }

        private IEnumerator ScrollCardToCenter(BaseCard card)
        {
            if (card == null || _scrollRect == null || _scrollRect.viewport == null)
                yield break;

            Canvas.ForceUpdateCanvases();
            _scrollRect.StopMovement();
            _scrollRect.velocity = Vector2.zero;

            RectTransform viewport = _scrollRect.viewport;
            Vector3 cardWorldCenter = card.RectTransform.TransformPoint(card.RectTransform.rect.center);
            Vector3 cardViewportPosition = viewport.InverseTransformPoint(cardWorldCenter);

            float xOffsetFromCenter = cardViewportPosition.x - viewport.rect.center.x;
            Vector2 startPosition = _rect.anchoredPosition;
            Vector2 targetPosition = startPosition - new Vector2(xOffsetFromCenter, 0f);
            targetPosition.x = ClampScrollContentX(targetPosition.x, viewport);

            float duration = GetScaledDuration(scrollToCardDuration);

            if (duration <= 0f)
            {
                _rect.anchoredPosition = targetPosition;
                yield break;
            }

            float time = 0f;
            while (time < duration)
            {
                float t = Mathf.SmoothStep(0f, 1f, time / duration);
                _rect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
                time += Time.deltaTime;
                yield return null;
            }

            _rect.anchoredPosition = targetPosition;
        }

        private float ClampScrollContentX(float targetX, RectTransform viewport)
        {
            float overflow = Mathf.Max(0f, _rect.rect.width - viewport.rect.width);
            float halfOverflow = overflow * 0.5f;

            return Mathf.Clamp(targetX, -halfOverflow, halfOverflow);
        }

        private float GetScaledDuration(float duration)
        {
            return duration / Mathf.Max(0.01f, playAnimationSpeed);
        }

        void RefreshRenderOrder()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] != null)
                    _slots[i].transform.SetSiblingIndex(i);
            }
        }

        void RegisterExistingCards()
        {
            if (transform.childCount <= 0)
                return;

            _slots.Clear();
            _cardById.Clear();
            _previousSlot.Clear();

            for (int i = 0; i < transform.childCount; i++)
            {
                BaseCard card = transform.GetChild(i).GetComponent<BaseCard>();

                if (card == null)
                    continue;

                if (string.IsNullOrEmpty(card.Id))
                    card.GenerateId();

                _slots.Add(card);
                _cardById[card.Id] = card;
                _previousSlot[card.Id] = i;

                card.RectTransform.SetParent(transform, false);
            }

            UpdateVisual();
            RefreshRenderOrder();
        }

        public BaseCard GetCardById(string id)
        {
            _cardById.TryGetValue(id, out BaseCard card);
            return card;
        }

        private float GetCardTargetY(BaseCard card)
        {
            if (card is not PlayableCard playableCard)
                return 0f;

            return playableCard.IsSelectedForPlay ? selectedYOffset : 0f;
        }
    }
}

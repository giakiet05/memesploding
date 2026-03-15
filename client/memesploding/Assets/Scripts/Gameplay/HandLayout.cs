using System;
using System.Collections.Generic;
using Events;
using Events.GameEvents;
using ScriptableObjects;
using UnityEngine;
using EventType = Events.EventType;

namespace Gameplay
{
    public class HandLayout : MonoBehaviour
    {
        public float baseSpacing = 120f;
        public float minSpacing = 60f;

        public float baseFanAngle = 30f;
        public float minFanAngle = 6f;

        public float moveSpeed = 12f;
        public float reorderSpeed = 8f;

        private readonly List<Card> _slots = new();
        private readonly List<Card> _targetOrder = new();
        private readonly Dictionary<string, Card> _cardById = new();
        private readonly Dictionary<string, int> _previousSlot = new();

        private RectTransform _rect;

        private float _reorderTimer;

        [SerializeField] public CardData defaultCardData;

        void Start()
        {
            _rect = GetComponent<RectTransform>();
            for (int i = 0; i < transform.childCount; i++)
            {
                Card card = transform.GetChild(i).GetComponent<Card>();

                if (!card)
                    continue;

                if (string.IsNullOrEmpty(card.Id))
                    card.GenerateId();

                if (card.Data == null)
                    card.Data = defaultCardData;

                _slots.Add(card);
                _cardById[card.Id] = card;
                _previousSlot[card.Id] = i;
            }

            //Event
            EventBus.Subscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
        }

        private void OnCardPlayed(CardPlayedEventPayload payload)
        {
            Card card = GetCardById(payload.PlayedCard.Id);

            if (card != null)
                RemoveCard(card);

            UpdateVisual();
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
                Card card = _slots[i];
                if (!card)
                    continue;

                float t = count > 1 ? (float)i / (count - 1) : 0.5f;
                float angle = Mathf.Lerp(-dynamicFan / 2f, dynamicFan / 2f, t);
                float x = (i - (count - 1) / 2f) * dynamicSpacing;

                Vector2 targetPos = new Vector2(x, 0);

                card.RectTransform.anchoredPosition =
                    Vector2.Lerp(card.RectTransform.anchoredPosition, targetPos, Time.deltaTime * moveSpeed);

                Quaternion targetRot = Quaternion.Euler(0, 0, -angle);

                card.RectTransform.localRotation =
                    Quaternion.Lerp(card.RectTransform.localRotation, targetRot, Time.deltaTime * moveSpeed);
            }

            float pivotSpan = (count - 1) * dynamicSpacing;
            float totalWidth = pivotSpan + cardWidth;

            _rect.sizeDelta = new Vector2(totalWidth, _rect.sizeDelta.y);

            _reorderTimer += Time.deltaTime;

            if (_reorderTimer >= 1f / reorderSpeed)
            {
                _reorderTimer = 0f;
                StepTowardTarget();
            }
        }

        void StepTowardTarget()
        {
            if (_targetOrder.Count == 0)
                return;

            for (int i = 0; i < _targetOrder.Count; i++)
            {
                if (_slots[i] == _targetOrder[i])
                    continue;

                int j = _slots.IndexOf(_targetOrder[i]);

                if (j > i)
                {
                    Card temp = _slots[j - 1];
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

        void SaveSlot(Card card, int index)
        {
            if (card == null)
                return;

            _previousSlot[card.Id] = index;
        }

        public void AddCard(Card card)
        {
            card.RectTransform.SetParent(transform, false);

            int preferredIndex = -1;

            if (_previousSlot.TryGetValue(card.Id, out int old))
            {
                if (old < _slots.Count && _slots[old] == null)
                    preferredIndex = old;
            }

            if (preferredIndex >= 0)
                _slots[preferredIndex] = card;
            else
                _slots.Add(card);

            _cardById[card.Id] = card;
            RefreshRenderOrder();
        }

        public void RemoveCard(Card card)
        {
            int index = _slots.IndexOf(card);

            if (index >= 0)
            {
                _slots[index] = null;
                _previousSlot[card.Id] = index;
            }

            _cardById.Remove(card.Id);
            RefreshRenderOrder();
        }

        public void UpdateVisual()
        {
            _targetOrder.Clear();

            foreach (var c in _slots)
                if (c != null)
                    _targetOrder.Add(c);

            _targetOrder.Sort((a, b) =>
            {
                int cmp = string.Compare(a.Data.cardName, b.Data.cardName, System.StringComparison.Ordinal);

                if (cmp != 0)
                    return cmp;

                return _previousSlot[a.Id].CompareTo(_previousSlot[b.Id]);
            });
        }

        void RefreshRenderOrder()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] != null)
                    _slots[i].transform.SetSiblingIndex(i);
            }
        }

        public Card GetCardById(string id)
        {
            _cardById.TryGetValue(id, out Card card);
            return card;
        }
    }
}
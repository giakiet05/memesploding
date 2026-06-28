using System;
using System.Collections.Generic;
using Gameplay;
using Gameplay.Card;
using ScriptableObjects;
using UnityEngine;

namespace Managers
{
    public enum CardType
    {
        Display,
        Playable,
        PlayerDraw,
        OpponentDraw
    }

    public class CardFactory : MonoBehaviour
    {
        [SerializeField] private CardDatabase cardDatabase;

        [Header("Prefabs")]
        [SerializeField] private DisplayCard displayCardPrefab;
        [SerializeField] private PlayableCard playableCardPrefab;
        [SerializeField] private PlayerDrawCard playerDrawCardPrefab;
        [SerializeField] private OpponentDrawCard opponentDrawCardPrefab;

        private Dictionary<CardType, BaseCard> _prefabMap;

        private void Awake()
        {
            _prefabMap = new Dictionary<CardType, BaseCard>
            {
                { CardType.Display, displayCardPrefab },
                { CardType.Playable, playableCardPrefab },
                { CardType.PlayerDraw, playerDrawCardPrefab },
                { CardType.OpponentDraw, opponentDrawCardPrefab }
            };
        }

        public void SetCardDatabase(CardDatabase database)
        {
            cardDatabase = database;
        }

        public CardData GetCardData(string cardName)
        {
            return cardDatabase != null ? cardDatabase.Get(cardName) : null;
        }

        public T Create<T>(string cardName, Transform parent) where T : BaseCard
        {
            var data = cardDatabase.Get(cardName);
            if (data == null)
            {
                Debug.LogWarning($"Card data not found: {cardName}");
                return null;
            }

            var prefab = GetPrefab<T>();
            if (prefab == null)
            {
                Debug.LogError($"No prefab registered for type {typeof(T)}");
                return null;
            }

            var card = Instantiate(prefab, parent, false) as T;
            card.Initialize(data);

            return card;
        }

        public BaseCard Create(CardType type, string cardName, Transform parent)
        {
            var data = cardDatabase.Get(cardName);
            if (data == null)
            {
                Debug.LogWarning($"Card data not found: {cardName}");
                return null;
            }

            if (!_prefabMap.TryGetValue(type, out var prefab) || prefab == null)
            {
                Debug.LogError($"No prefab mapped for type {type}");
                return null;
            }

            var card = Instantiate(prefab, parent, false);
            card.Initialize(data);

            return card;
        }

        public bool InitializeExisting(BaseCard card, string cardName)
        {
            if (card == null)
            {
                Debug.LogError("Cannot initialize a null card instance.");
                return false;
            }

            var data = cardDatabase.Get(cardName);
            if (data == null)
            {
                Debug.LogWarning($"Card data not found: {cardName}");
                return false;
            }

            card.Initialize(data);
            return true;
        }

        private BaseCard GetPrefab<T>() where T : BaseCard
        {
            foreach (var kvp in _prefabMap)
            {
                if (kvp.Value is T)
                    return kvp.Value;
            }
            return null;
        }
    }
}

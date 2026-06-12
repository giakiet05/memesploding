using Events;
using Events.GameEvents;
using Gameplay;
using Gameplay.Card;
using ScriptableObjects;
using System.Collections.Generic;
using UnityEngine;
using EventType = Events.EventType;

namespace Managers
{
    public class CardManager : MonoBehaviour
    {
        public static CardManager Instance;

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Destroy(gameObject);
            else
                Instance = this;
        }

        [SerializeField] private CardFactory factory;
        [SerializeField] private HandLayout handLayout;
        [SerializeField] private RectTransform dragLayer;

        public HandLayout HandLayout => handLayout;
        public RectTransform DragLayer => dragLayer;
        public CardData GetCardData(string cardCode) => factory != null ? factory.GetCardData(cardCode) : null;

        private void Start()
        {
            ValidateReferences();
        }

        public void AddCardToHand(string cardCode)
        {
            PlayableCard card = factory.Create<PlayableCard>(cardCode, HandLayout.transform);
            if (card == null)
                return;

            handLayout.AddCard(card);
        }

        public void AddCardsToHand(List<string> cardCodes)
        {
            if (cardCodes == null)
                return;

            foreach (var cardCode in cardCodes)
            {
                PlayableCard card = factory.Create<PlayableCard>(cardCode, HandLayout.transform);
                if (card == null)
                    continue;

                handLayout.AddCard(card);
            }
        }

        public bool RemoveCardFromHand(string cardCode)
        {
            return handLayout.RemoveFirstCardByCode(cardCode);
        }

        public bool ConfirmPendingPlay()
        {
            return handLayout != null && handLayout.ConfirmPendingPlay();
        }

        public void RejectPendingPlay()
        {
            handLayout?.RejectPendingPlay();
        }

        public void SyncHand(List<string> cardCodes)
        {
            if (handLayout == null)
                return;

            handLayout.ClearCards();
            AddCardsToHand(cardCodes);
        }

        public PlayableCard CreatePlayableCard(string cardName, Transform parent)
        {
            return factory.Create<PlayableCard>(cardName, parent);
        }

        public DisplayCard CreateDisplayCard(string cardName, Transform parent)
        {
            return factory.Create<DisplayCard>(cardName, parent);
        }

        public bool InitializePlayerDrawCard(PlayerDrawCard card, string cardCode)
        {
            return factory != null && factory.InitializeExisting(card, cardCode);
        }

        private void ValidateReferences()
        {
            if (!handLayout)
                Debug.LogError("HandLayout not assigned in CardManager", this);

            if (!dragLayer)
                Debug.LogError("DragLayer not assigned in CardManager", this);
        }
    }
}

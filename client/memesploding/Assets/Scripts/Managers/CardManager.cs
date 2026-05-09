using Events;
using Events.GameEvents;
using Gameplay;
using Gameplay.Card;
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

        private void Start()
        {
            ValidateReferences();

            InitStartingHand(30, "DEFUSE");
        }

        //For testing
        private void InitStartingHand(int amount, string cardName)
        {
            for (int i = 0; i < amount; i++)
            {
                PlayableCard card = factory.Create<PlayableCard>(cardName, HandLayout.transform);
                handLayout.AddCard(card);
            }
        }

        public void AddCardToHand(string cardCode)
        {
            PlayableCard card = factory.Create<PlayableCard>(cardCode, HandLayout.transform);
            handLayout.AddCard(card);
        }

        public void AddCardsToHand(List<string> cardCodes)
        {
            foreach (var cardCode in cardCodes)
            {
                PlayableCard card = factory.Create<PlayableCard>(cardCode, HandLayout.transform);
                handLayout.AddCard(card);
            }
        }

        public bool RemoveCardFromHand(string cardCode)
        {
            return handLayout.RemoveCardById(cardCode);
        }

        public PlayableCard CreatePlayableCard(string cardName, Transform parent)
        {
            return factory.Create<PlayableCard>(cardName, parent);
        }

        public DisplayCard CreateDisplayCard(string cardName, Transform parent)
        {
            return factory.Create<DisplayCard>(cardName, parent);
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
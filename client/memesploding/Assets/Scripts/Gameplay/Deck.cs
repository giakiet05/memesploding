using Events;
using Managers;
using System;
using Events.GameEvents;
using UI;
using UI.Gameplay;
using UnityEngine;
using EventType = Events.EventType;

namespace Gameplay
{
    public class Deck : MonoBehaviour
    {
        [SerializeField] private GlowImage glowImage;

        private void Start()
        {
            EventBus.Subscribe<TurnStartEventPayload>(EventType.TurnStart, OnTurnStart);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<TurnStartEventPayload>(EventType.TurnStart, OnTurnStart);
        }

        public void OnTurnStart(TurnStartEventPayload payload)
        {
            if (payload.UserID != GameManager.Instance.Player.ID)
                return;

            glowImage.gameObject.SetActive(true);
        }

        public void OnDeckClicked()
        {
            GameManager.Instance.DrawCard();
            glowImage.gameObject.SetActive(false);
        }
    }
}
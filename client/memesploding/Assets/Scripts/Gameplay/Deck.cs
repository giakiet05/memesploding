using Events;
using Managers;
using System;
using Events.GameEvents;
using UI;
using UnityEngine;
using EventType = Events.EventType;
using UI.Gameplay;

namespace Gameplay
{
    public class Deck : MonoBehaviour
    {
        [SerializeField] private GlowImage glowImage;
        private bool _suppressGlowUntilLocalTurn;


        private void Awake()
        {
            glowImage ??= transform.Find("GlowImage")?.GetComponent<GlowImage>();

            if (glowImage == null)
                Debug.LogError("[Deck] GlowImage reference is missing.");
        }

        private void Start()
        {
            EventBus.Subscribe<TurnStartEventPayload>(EventType.TurnStart, OnTurnStart);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<TurnStartEventPayload>(EventType.TurnStart, OnTurnStart);
        }

        private void Update()
        {
            SetGlowActive(
                !_suppressGlowUntilLocalTurn &&
                GameManager.Instance != null &&
                GameManager.Instance.CanDrawLocalCard());
        }

        public void OnTurnStart(TurnStartEventPayload payload)
        {
            var localUserId = GameManager.Instance?.Player?.ID;
            var isLocalTurn = payload != null &&
                              !string.IsNullOrWhiteSpace(localUserId) &&
                              payload.UserID == localUserId;

            if (isLocalTurn)
                _suppressGlowUntilLocalTurn = false;

            SetGlowActive(isLocalTurn && !_suppressGlowUntilLocalTurn);
        }

        public void OnDeckClicked()
        {
            Debug.Log("[DrawTrace] Deck clicked.");
            if (GameManager.Instance == null || !GameManager.Instance.CanDrawLocalCard())
            {
                Debug.Log("[DrawTrace] Deck click ignored while another interaction is active.");
                return;
            }
            _suppressGlowUntilLocalTurn = true;
            GameManager.Instance.DrawCard();
            SetGlowActive(false);
        }

        private void SetGlowActive(bool isActive)
        {
            if (glowImage != null)
                glowImage.gameObject.SetActive(isActive);
        }
    }
}

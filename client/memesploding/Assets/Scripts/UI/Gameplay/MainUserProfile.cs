using Events;
using Events.GameEvents;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using EventType = Events.EventType;

namespace UI.Gameplay
{
    public class MainUserProfile : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI cardCounterText;
        [SerializeField] private Image profileImage;
        [SerializeField] private GlowImage glowImage;

        private int _currentCards = 0;

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
            var localUserId = GameManager.Instance?.Player?.ID;
            if (string.IsNullOrWhiteSpace(localUserId) || payload == null || payload.UserID != localUserId)
            {
                SetTurnIndicator(false);
                return;
            }

            SetTurnIndicator(true);
        }
        
        public void UpdateCardCounter(int amount)
        {
            _currentCards = amount;
            cardCounterText.text = _currentCards.ToString("D2");
        }

        public void UpdateProfileImage(Sprite image)
        {
            if (profileImage != null && image != null)
                profileImage.sprite = image;
        }

        public void SetCurrentTurn(bool isCurrentTurn)
        {
            SetTurnIndicator(isCurrentTurn);
        }

        private void SetTurnIndicator(bool isActive)
        {
            if (glowImage != null)
                glowImage.gameObject.SetActive(isActive);
        }
    }
}

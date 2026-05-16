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
            if (payload.UserID != GameManager.Instance.Player.ID)
            {
                glowImage.gameObject.SetActive(false);
                return;
            }

            glowImage.gameObject.SetActive(true);
        }
        
        public void UpdateCardCounter(int amount)
        {
            _currentCards = amount;
            cardCounterText.text = _currentCards.ToString("D2");
        }

        public void UpdateProfileImage(Sprite image)
        {
            profileImage.sprite = image;
        }

        public void SetCurrentTurn()
        {
            //TODO: Indicate this is the user turn
        }
    }
}
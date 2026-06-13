using Events;
using Events.GameEvents;
using Managers;
using System.Collections;
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
        private bool _isEliminated;

        private void Start()
        {
            EventBus.Subscribe<TurnStartEventPayload>(EventType.TurnStart, OnTurnStart);
            EventBus.Subscribe<TurnEndEventPayload>(EventType.TurnEnd, OnTurnEnd);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<TurnStartEventPayload>(EventType.TurnStart, OnTurnStart);
            EventBus.Unsubscribe<TurnEndEventPayload>(EventType.TurnEnd, OnTurnEnd);
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

        private void OnTurnEnd(TurnEndEventPayload payload)
        {
            if (payload != null && payload.UserID == GameManager.Instance?.Player?.ID)
                SetTurnIndicator(false);
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

        public void SetEliminated(bool eliminated)
        {
            if (_isEliminated == eliminated)
                return;

            _isEliminated = eliminated;
            SetTurnIndicator(false);
            if (profileImage != null)
                profileImage.color = eliminated ? new Color(0.28f, 0.28f, 0.28f, 1f) : Color.white;
            if (eliminated)
                StartCoroutine(PlayEliminationAnimation());
        }

        private IEnumerator PlayEliminationAnimation()
        {
            var rect = transform as RectTransform;
            if (rect == null)
                yield break;

            var originScale = rect.localScale;
            for (var elapsed = 0f; elapsed < 0.7f; elapsed += Time.unscaledDeltaTime)
            {
                var t = elapsed / 0.7f;
                var pulse = 1f + Mathf.Sin(t * Mathf.PI * 4f) * (1f - t) * 0.18f;
                rect.localScale = originScale * pulse;
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 8f) * (1f - t) * 6f);
                yield return null;
            }
            rect.localScale = originScale;
            rect.localRotation = Quaternion.identity;
        }

        private void SetTurnIndicator(bool isActive)
        {
            if (glowImage != null)
                glowImage.gameObject.SetActive(isActive);
        }
    }
}

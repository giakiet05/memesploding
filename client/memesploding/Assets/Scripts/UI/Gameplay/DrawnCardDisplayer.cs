using Gameplay;
using Managers.UIManager;
using UnityEngine;

using Managers;

namespace UI.Gameplay
{
    public class DrawnCardDisplayer : MonoBehaviour
    { 
        [SerializeField] private PlayerDrawCard playerDrawCard;
        private System.Action _onAnimationFinished;


        private void Awake()
        {
            playerDrawCard ??= GetComponentInChildren<PlayerDrawCard>(true);

            if (playerDrawCard == null)
            {
                Debug.LogError("[DrawnCardDisplayer] PlayerDrawCard reference is missing.");
                return;
            }

            playerDrawCard.OnAnimationFinished += OnAnimationFinished;
        }

        private void OnAnimationFinished(PlayerDrawCard obj)
        {
            CompletePendingDraw();
            GameplayUIManager.Instance.ResetUI();
        }

        private void OnDestroy()
        {
            if (playerDrawCard != null)
                playerDrawCard.OnAnimationFinished -= OnAnimationFinished;
        }

        private void OnDisable()
        {
            CompletePendingDraw();
            if (playerDrawCard != null)
            {
                playerDrawCard.Reset();
                playerDrawCard.gameObject.SetActive(false);
            }
        }

        public bool PlayDrawCardAnimation(
            string cardCode,
            Vector3 deckWorldPosition,
            Vector3 handWorldPosition,
            System.Action onAnimationFinished = null)
        {
            if (playerDrawCard == null)
            {
                Debug.LogError("[DrawnCardDisplayer] Unable to play draw animation because PlayerDrawCard is missing.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(cardCode))
            {
                Debug.LogWarning("[DrawnCardDisplayer] Draw animation requested without a card code.");
                return false;
            }

            if (!CardManager.Instance.InitializePlayerDrawCard(playerDrawCard, cardCode))
                return false;

            var parentRect = playerDrawCard.RectTransform.parent as RectTransform;
            var screenPoint = RectTransformUtility.WorldToScreenPoint(null, deckWorldPosition);
            var startPosition = Vector2.zero;
            var handPosition = Vector2.zero;
            if (parentRect != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, null, out startPosition);
                var handScreenPoint = RectTransformUtility.WorldToScreenPoint(null, handWorldPosition);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, handScreenPoint, null, out handPosition);
            }

            _onAnimationFinished = onAnimationFinished;
            playerDrawCard.gameObject.SetActive(true);
            playerDrawCard.PlayAnimation(startPosition, handPosition);
            return true;
        }


        private void CompletePendingDraw()
        {
            var callback = _onAnimationFinished;
            _onAnimationFinished = null;
            callback?.Invoke();
        }
    }
}

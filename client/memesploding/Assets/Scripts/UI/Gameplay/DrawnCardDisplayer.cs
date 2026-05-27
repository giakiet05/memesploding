using Gameplay;
using Managers.UIManager;
using UnityEngine;

using Managers;

namespace UI.Gameplay
{
    public class DrawnCardDisplayer : MonoBehaviour
    { 
        [SerializeField] private PlayerDrawCard playerDrawCard;

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
            GameplayUIManager.Instance.ResetUI();
        }

        private void OnDestroy()
        {
            if (playerDrawCard != null)
                playerDrawCard.OnAnimationFinished -= OnAnimationFinished;
        }

        private void OnDisable()
        {
            if (playerDrawCard != null)
            {
                playerDrawCard.Reset();
                playerDrawCard.gameObject.SetActive(false);
            }
        }

        public void PlayDrawCardAnimation(string cardCode)
        {
            if (playerDrawCard == null)
            {
                Debug.LogError("[DrawnCardDisplayer] Unable to play draw animation because PlayerDrawCard is missing.");
                return;
            }

            if (string.IsNullOrWhiteSpace(cardCode))
            {
                Debug.LogWarning("[DrawnCardDisplayer] Draw animation requested without a card code.");
                return;
            }

            if (!CardManager.Instance.InitializePlayerDrawCard(playerDrawCard, cardCode))
                return;

            playerDrawCard.gameObject.SetActive(true);
            playerDrawCard.PlayAnimation();
        }
    }
}

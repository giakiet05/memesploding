using Gameplay;
using Managers.UIManager;
using UnityEngine;

namespace UI.Gameplay
{
    public class DrawnCardDisplayer : MonoBehaviour
    { 
        [SerializeField] private PlayerDrawCard playerDrawCard;

        private void Start()
        {
            playerDrawCard.OnAnimationFinished += OnAnimationFinished;
        }

        private void OnAnimationFinished(PlayerDrawCard obj)
        {
            GameplayUIManager.Instance.ResetUI();
        }

        private void OnEnable()
        {
            PlayDrawCardAnimation();
        }

        private void OnDisable()
        {
            playerDrawCard.gameObject.SetActive(false);
        }

        public void PlayDrawCardAnimation()
        {
            //playerDrawCard.Reset();
            playerDrawCard.gameObject.SetActive(true);
        }
    }
}
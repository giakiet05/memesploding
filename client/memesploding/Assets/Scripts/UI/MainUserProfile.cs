using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class MainUserProfile : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI cardCounterText;
        [SerializeField] private Image profileImage;

        private int _currentCards = 0;

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
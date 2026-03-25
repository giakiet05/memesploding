using System.Collections.Generic;
using Card;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class CardDisplayer : MonoBehaviour
    {
        [SerializeField] private RectTransform contentRect;

        public void Initialize(List<DisplayCard> cards)
        {
            foreach (var card in cards)
            {
                if (card == null)
                    continue;

                RectTransform rect = card.GetComponent<RectTransform>();

                rect.SetParent(contentRect, false);
            }
        }
    }
}

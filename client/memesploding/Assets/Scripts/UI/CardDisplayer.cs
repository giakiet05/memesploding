using Gameplay.Card;
using System.Collections.Generic;
using UnityEngine;

namespace UI
{
    public class CardDisplayer : MonoBehaviour
    {
        [SerializeField] private RectTransform contentRect;

        public void SetCards(List<DisplayCard> cards)
        {
            Clear();

            foreach (var card in cards)
            {
                if (card == null)
                    continue;

                card.transform.SetParent(contentRect, false);
            }
        }

        public void AddCard(DisplayCard card)
        {
            if (card == null)
                return;

            card.transform.SetParent(contentRect, false);
        }

        public void RemoveCard(DisplayCard card)
        {
            if (card == null)
                return;

            Destroy(card.gameObject);
        }

        public void Clear()
        {
            for (int i = contentRect.childCount - 1; i >= 0; i--)
            {
                Destroy(contentRect.GetChild(i).gameObject);
            }
        }
    }
}
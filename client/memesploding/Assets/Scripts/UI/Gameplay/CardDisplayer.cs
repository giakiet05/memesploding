using System.Collections.Generic;
using Gameplay.Card;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Gameplay
{
    public class CardDisplayer : MonoBehaviour
    {
        [SerializeField] private RectTransform contentRect;

        private void Awake()
        {
            ResolveContentRect();
            Clear();
        }

        private void OnEnable()
        {
            ResolveContentRect();
        }

        public void SetCards(List<DisplayCard> cards)
        {
            if (!ResolveContentRect())
                return;

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

            if (!ResolveContentRect())
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
            if (!ResolveContentRect())
                return;

            for (int i = contentRect.childCount - 1; i >= 0; i--)
            {
                var child = contentRect.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private bool ResolveContentRect()
        {
            foreach (var rect in GetComponentsInChildren<RectTransform>(true))
            {
                if (rect != transform && rect.name == "Content" && rect.GetComponent<HorizontalLayoutGroup>() != null)
                {
                    contentRect = rect;
                    return true;
                }
            }

            Debug.LogError("[CardDisplayer] Content Rect with HorizontalLayoutGroup was not found.", this);
            return false;
        }
    }
}

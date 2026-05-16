using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Gameplay
{
    public class CardSelector : MonoBehaviour
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content;

        [SerializeField] private float snapSpeed = 10f;

        private List<RectTransform> _cards = new();
        private int _currentIndex = 0;

        private bool _isDragging;

        void Start()
        {
            foreach (Transform child in content)
            {
                _cards.Add(child as RectTransform);
            }
        }

        void Update()
        {
            if (_isDragging) return;

            float target = GetTargetPosition(_currentIndex);

            float newX = Mathf.Lerp(
                content.anchoredPosition.x,
                target,
                Time.deltaTime * snapSpeed
            );

            content.anchoredPosition =
                new Vector2(newX, content.anchoredPosition.y);
        }

        public void OnBeginDrag()
        {
            _isDragging = true;
        }

        public void OnEndDrag()
        {
            _isDragging = false;
            _currentIndex = GetNearestCard();
        }

        int GetNearestCard()
        {
            float min = float.MaxValue;
            int index = 0;

            for (int i = 0; i < _cards.Count; i++)
            {
                float dist = Mathf.Abs(
                    content.anchoredPosition.x - GetTargetPosition(i)
                );

                if (dist < min)
                {
                    min = dist;
                    index = i;
                }
            }

            return index;
        }

        float GetTargetPosition(int index)
        {
            return -_cards[index].anchoredPosition.x;
        }

        public void Next()
        {
            _currentIndex = Mathf.Min(_currentIndex + 1, _cards.Count - 1);
        }

        public void Previous()
        {
            _currentIndex = Mathf.Max(_currentIndex - 1, 0);
        }
    }
}

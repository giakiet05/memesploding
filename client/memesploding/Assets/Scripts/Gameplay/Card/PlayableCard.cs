using System.Collections;
using Managers;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Gameplay.Card
{
    public class PlayableCard : BaseCard, IPointerClickHandler
    {
        private HandLayout _handLayout;

        public bool Draggable { get; private set; } = true;
        public bool IsSelectedForPlay { get; private set; }

        public void DisableDrag()
        {
            Draggable = false;
            SetSelectedForPlay(false);
        }

        protected override void Awake()
        {
            base.Awake();
        }

        void Start()
        {
            _handLayout = CardManager.Instance.HandLayout;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Draggable)
                return;

            ToggleSelectedForPlay();
        }

        public void ToggleSelectedForPlay()
        {
            SetSelectedForPlay(!IsSelectedForPlay);
        }

        public void SetSelectedForPlay(bool isSelected)
        {
            if (!Draggable && isSelected)
                return;

            if (IsSelectedForPlay == isSelected)
                return;

            IsSelectedForPlay = isSelected;
            _handLayout?.NotifyCardSelectionChanged();
        }

        public IEnumerator PlayToBoard(
            RectTransform boardArea,
            Vector2 boardPosition,
            float moveToBoardDuration)
        {
            RectTransform.SetParent(boardArea, true);
            RectTransform.SetAsLastSibling();
            RectTransform.localRotation = Quaternion.identity;

            yield return AnimateAnchoredPosition(boardPosition, moveToBoardDuration);
        }

        private IEnumerator AnimateAnchoredPosition(Vector2 targetPosition, float duration)
        {
            Vector2 startPosition = RectTransform.anchoredPosition;
            Quaternion startRotation = RectTransform.localRotation;
            Quaternion targetRotation = Quaternion.identity;

            if (duration <= 0f)
            {
                RectTransform.anchoredPosition = targetPosition;
                RectTransform.localRotation = targetRotation;
                yield break;
            }

            float time = 0f;
            while (time < duration)
            {
                float t = Mathf.SmoothStep(0f, 1f, time / duration);
                RectTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
                RectTransform.localRotation = Quaternion.Lerp(startRotation, targetRotation, t);
                time += Time.deltaTime;
                yield return null;
            }

            RectTransform.anchoredPosition = targetPosition;
            RectTransform.localRotation = targetRotation;
        }
    }
}

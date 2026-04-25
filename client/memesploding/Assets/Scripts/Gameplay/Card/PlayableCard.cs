using System.Collections;
using Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gameplay.Card
{
    public class PlayableCard : BaseCard, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        private CanvasGroup _canvasGroup;

        [Header("Drag Settings")]
        [SerializeField] private float holdThreshold = 0.2f;
        [SerializeField] private float verticalDragThreshold = 30f;
        [SerializeField] private Vector3 dragScale = new Vector3(1.2f, 1.2f, 1.2f);

        private Canvas _rootCanvas;
        private RectTransform _canvasRect;
        private RectTransform _dragLayer;
        private ScrollRect _scrollRect;
        private HandLayout _handLayout;
        private Vector3 _originalScale;

        private bool _isDragging;
        private bool _isHoldReady;
        private bool _scrollForwarded;
        private PointerEventData _pendingDragEvent;
        private Vector2 _pointerDownPosition;

        public bool Draggable { get; private set; } = true;

        public void DisableDrag()
        {
            Draggable = false;
        }

        protected override void Awake()
        {
            base.Awake();
            
            _canvasGroup = GetComponent<CanvasGroup>();

            _rootCanvas = GetComponentInParent<Canvas>();
            _canvasRect = _rootCanvas.GetComponent<RectTransform>();
            _scrollRect = GetComponentInParent<ScrollRect>();

            _originalScale = RectTransform.localScale;
        }

        void Start()
        {
            _handLayout = CardManager.Instance.HandLayout;
            _dragLayer = CardManager.Instance.DragLayer;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isHoldReady = false;
            _pointerDownPosition = eventData.position;
            StartCoroutine(HoldTimer());
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            StopAllCoroutines();

            // Clean up any forwarded scroll on pointer up (tap without a full drag)
            if (_scrollForwarded && _scrollRect != null && !_isDragging)
            {
                _scrollRect.OnEndDrag(eventData);
                _scrollForwarded = false;
            }

            if (_isDragging)
            {
                _isDragging = false;
                _isHoldReady = false;
                ReturnToHand();
            }
            else
            {
                _isHoldReady = false;
                _pendingDragEvent = null;
            }
        }

        private IEnumerator HoldTimer()
        {
            yield return new WaitForSeconds(holdThreshold);
            _isHoldReady = true;

            if (_pendingDragEvent != null)
            {
                BeginCardDrag(_pendingDragEvent);
                _pendingDragEvent = null;
            }
        }

        // Drag
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!Draggable)
                return;

            Vector2 dragDelta = eventData.position - _pointerDownPosition;
            float verticalDrag = Mathf.Abs(dragDelta.y);
            float horizontalDrag = Mathf.Abs(dragDelta.x);

            // If dragging vertically more than threshold, immediately start card drag
            if (verticalDrag > verticalDragThreshold && verticalDrag > horizontalDrag)
            {
                StopAllCoroutines();
                _isHoldReady = true;
                BeginCardDrag(eventData);
                return;
            }

            // If not ready and dragging horizontally, allow scroll
            if (!_isHoldReady)
            {
                _pendingDragEvent = eventData;
                if (_scrollRect != null)
                {
                    _scrollRect.OnBeginDrag(eventData);
                    _scrollForwarded = true;
                }
                return;
            }

            BeginCardDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!Draggable)
                return;

            if (!_isDragging)
            {
                // Check if user drags vertically enough to switch from scroll to card drag
                Vector2 dragDelta = eventData.position - _pointerDownPosition;
                float verticalDrag = Mathf.Abs(dragDelta.y);
                float horizontalDrag = Mathf.Abs(dragDelta.x);

                if (verticalDrag > verticalDragThreshold && verticalDrag > horizontalDrag && !_scrollForwarded)
                {
                    StopAllCoroutines();
                    _isHoldReady = true;
                    BeginCardDrag(eventData);
                    return;
                }

                if (_scrollRect != null)
                    _scrollRect.OnDrag(eventData);
                return;
            }

            MoveToPointer(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDragging)
            {
                // Always deliver the matching OnEndDrag to ScrollRect if we forwarded to it
                if (_scrollRect != null && _scrollForwarded)
                {
                    _scrollRect.OnEndDrag(eventData);
                    _scrollForwarded = false;
                }
                return;
            }

            _isDragging = false;
            _isHoldReady = false;
            _scrollForwarded = false;

            _canvasGroup.blocksRaycasts = true;

            ReturnToHand();
        }

        void BeginCardDrag(PointerEventData eventData)
        {
            // If ScrollRect was already told about a drag, cancel it cleanly before reparenting
            if (_scrollForwarded && _scrollRect != null)
            {
                _scrollRect.OnEndDrag(eventData);
                _scrollForwarded = false;
            }

            _canvasGroup.blocksRaycasts = false;

            _isDragging = true;
            RectTransform.localScale = dragScale;

            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                _canvasRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector3 worldPoint);

            if (_handLayout != null)
                _handLayout.RemoveCard(this);

            transform.SetParent(_dragLayer, true);
            transform.SetAsLastSibling();
            RectTransform.position = worldPoint;
        }

        void MoveToPointer(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    _canvasRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector3 worldPoint))
            {
                RectTransform.position = worldPoint;
            }
        }

        void ReturnToHand()
        {
            if (_handLayout == null)
            {
                Debug.Log("Hand layout is not assign for card");
                return;
            }

            _handLayout.AddCard(this);
            _handLayout.UpdateVisual();
            RectTransform.localScale = _originalScale;
        }
    }
}

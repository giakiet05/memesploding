using Managers;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

namespace Gameplay
{
    public class Card : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        public string Id { get; private set; }
        public CardData Data { get; set; }
        public RectTransform RectTransform { get; private set; }
        private CanvasGroup _canvasGroup;
        private Image _cardImage;

        [Header("Drag Settings")]
        [SerializeField] private float holdThreshold = 0.4f;
        [SerializeField] private Vector3 dragScale = new Vector3(1.2f, 1.2f, 1.2f);

        private Canvas _rootCanvas;
        private RectTransform _canvasRect;
        private RectTransform _dragLayer;
        private ScrollRect _scrollRect;
        private HandLayout _handLayout;
        private Vector3 _originalScale;

        private bool _isDragging;
        private bool _isHoldReady;
        private PointerEventData _pendingDragEvent;

        public bool Draggable { get; private set; } = true;

        public void DisableDrag()
        {
            Draggable = false;
        }

        public Color normalColor = Color.white;
        public Color inactiveColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        void Start()
        {
            RectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            _cardImage = GetComponent<Image>();

            _rootCanvas = GetComponentInParent<Canvas>();
            _canvasRect = _rootCanvas.GetComponent<RectTransform>();
            _scrollRect = GetComponentInParent<ScrollRect>();
            _handLayout = CardManager.Instance.handLayout;
            _originalScale = RectTransform.localScale;
            _dragLayer = CardManager.Instance.dragLayer;
        }

        public void Initialize(CardData data)
        {
            Data = data;
            GenerateId();
            UpdateVisuals();
        }

        public void GenerateId()
        {
            Id = Guid.NewGuid().ToString();
        }

        void UpdateVisuals()
        {
            if (Data == null)
                return;
            _cardImage.sprite = Data.artwork;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isHoldReady = false;
            StartCoroutine(HoldTimer());
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            StopAllCoroutines();

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

            if (!_isHoldReady)
            {
                _pendingDragEvent = eventData;
                if (_scrollRect != null)
                    _scrollRect.OnBeginDrag(eventData);
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
                if (_scrollRect != null)
                    _scrollRect.OnEndDrag(eventData);
                return;
            }

            _isDragging = false;
            _isHoldReady = false;

            _canvasGroup.blocksRaycasts = true;

            ReturnToHand();
        }

        void BeginCardDrag(PointerEventData eventData)
        {
            _canvasGroup.blocksRaycasts = false;

            _isDragging = true;
            RectTransform.localScale = dragScale;

            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                _canvasRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector3 worldPoint);
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
            _handLayout.AddCard(this);
            _handLayout.UpdateVisual();
            RectTransform.localScale = _originalScale;
        }

        public void SetNewest(bool newest)
        {
            if (_cardImage == null)
                return;

            _cardImage.color = newest ? normalColor : inactiveColor;
        }
    }
}
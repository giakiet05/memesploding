using System.Collections;
using Gameplay.Card;
using ScriptableObjects;
using UnityEngine;

namespace Gameplay
{
    public class OpponentDrawCard : BaseCard
    {
        [Header("Animation Settings")]
        [SerializeField] private float duration = 0.5f;
        [SerializeField] private float speed = 1f;
        [SerializeField] private float jumpHeight = 40f;

        [Header("Curve Control")]
        [SerializeField] private float curveOffsetX = 30f;
        [SerializeField] private float curveOffsetY = 60f;
        [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Rotation")]
        [SerializeField] private float startRotation = 15f;
        [SerializeField] private float endRotation = -10f;
        [SerializeField] private bool alignRotationToPath = true;
        [SerializeField][Range(1f, 30f)] private float rotationSmoothing = 8f;

        [Header("References")]
        [SerializeField] private RectTransform targetRect;

        private Coroutine _playRoutine;
        private bool _destroyOnFinish;
        private Vector2 _originalAnchoredPosition;
        private Quaternion _originalLocalRotation;
        private Vector3 _originalLocalScale;

        protected override void Awake()
        {
            base.Awake();
            CacheOriginalTransform();
        }

        public override void Initialize(CardData data)
        {
            base.Initialize(data);
            // Add opponent-specific setup here if needed
        }

        public void Play(Vector2 startPosition, Vector2 targetPosition, bool destroyOnFinish = false)
        {
            if (_playRoutine != null)
                StopCoroutine(_playRoutine);

            _destroyOnFinish = destroyOnFinish;
            RestoreToDeckPosition();
            gameObject.SetActive(true);
            _playRoutine = StartCoroutine(PlayRoutine(startPosition, targetPosition));
        }

        private IEnumerator PlayRoutine(Vector2 startPosition, Vector2 targetPosition)
        {
            RectTransform.anchoredPosition = startPosition;
            RectTransform.localScale = Vector3.one;

            Quaternion currentRotation = Quaternion.Euler(0f, 0f, startRotation);
            RectTransform.localRotation = currentRotation;

            float effectiveDuration = duration / Mathf.Max(speed, 0.01f);
            float time = 0f;

            Vector2 direction = (targetPosition - startPosition).normalized;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            float dist = (targetPosition - startPosition).magnitude;

            Vector2 p0 = startPosition;
            Vector2 p1 = startPosition + perpendicular * curveOffsetX + Vector2.up * curveOffsetY + direction * (0.25f * dist);
            Vector2 p2 = targetPosition + perpendicular * (curveOffsetX * 0.5f) + Vector2.up * (jumpHeight * 0.5f) - direction * (0.15f * dist);
            Vector2 p3 = targetPosition;

            Vector2 prevPos = startPosition;

            while (time < effectiveDuration)
            {
                float rawT = time / effectiveDuration;
                float t = easeCurve.Evaluate(rawT);

                Vector2 pos = CubicBezier(p0, p1, p2, p3, t);
                RectTransform.anchoredPosition = pos;

                if (alignRotationToPath)
                {
                    Vector2 delta = pos - prevPos;
                    if (delta.sqrMagnitude > 0.0001f)
                    {
                        float targetAngle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f;
                        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);

                        currentRotation = Quaternion.Slerp(
                            currentRotation,
                            targetRotation,
                            rotationSmoothing * Time.deltaTime
                        );

                        RectTransform.localRotation = currentRotation;
                    }
                }
                else
                {
                    float angle = Mathf.Lerp(startRotation, endRotation, t);
                    currentRotation = Quaternion.Euler(0f, 0f, angle);
                    RectTransform.localRotation = currentRotation;
                }

                RectTransform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);

                prevPos = pos;
                time += Time.deltaTime;
                yield return null;
            }

            RectTransform.anchoredPosition = p3;
            RectTransform.localScale = Vector3.zero;
            RectTransform.localRotation = Quaternion.Euler(0f, 0f, endRotation);

            _playRoutine = null;

            if (_destroyOnFinish)
            {
                Destroy(gameObject);
                yield break;
            }

            RestoreToDeckPosition();
            gameObject.SetActive(false);
        }

        private Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1f - t;
            return (u * u * u) * p0
                 + (3f * u * u * t) * p1
                 + (3f * u * t * t) * p2
                 + (t * t * t) * p3;
        }

        public Vector2 GetTargetAnchoredPosition()
        {
            if (targetRect == null)
                return RectTransform.anchoredPosition;

            RectTransform parent = RectTransform.parent as RectTransform;

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, targetRect.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, null, out Vector2 localPoint);

            return localPoint;
        }

        private void CacheOriginalTransform()
        {
            if (RectTransform == null)
                return;

            _originalAnchoredPosition = RectTransform.anchoredPosition;
            _originalLocalRotation = RectTransform.localRotation;
            _originalLocalScale = RectTransform.localScale;
        }

        private void RestoreToDeckPosition()
        {
            if (RectTransform == null)
                return;

            RectTransform.anchoredPosition = _originalAnchoredPosition;
            RectTransform.localRotation = _originalLocalRotation;
            RectTransform.localScale = _originalLocalScale;
        }
    }
}

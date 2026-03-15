using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Gameplay
{
    public class Timer : MonoBehaviour
    {
        [Header("Timer UI references :")]
        [SerializeField] private Image uiFillImage;
        [SerializeField] private Text uiText;

        [Header("Timer Colors")]
        [SerializeField] private Color greenColor = Color.green;
        [SerializeField] private Color yellowColor = Color.yellow;
        [SerializeField] private Color redColor = Color.red;

        [Header("Color Thresholds (percentage left)")]
        [Range(0f, 1f)]
        [SerializeField] private float yellowThreshold = 0.6f;

        [Range(0f, 1f)]
        [SerializeField] private float redThreshold = 0.3f;

        public int Duration { get; private set; }

        public bool IsPaused { get; private set; }

        private int _remainingDuration;

        // Events --
        private UnityAction onTimerBeginAction;
        private UnityAction<int> onTimerChangeAction;
        private UnityAction onTimerEndAction;
        private UnityAction<bool> onTimerPauseAction;

        private void Awake()
        {
            ResetTimer();
        }

        private void ResetTimer()
        {
            uiText.text = "00:00";
            uiFillImage.fillAmount = 0f;

            Duration = _remainingDuration = 0;

            onTimerBeginAction = null;
            onTimerChangeAction = null;
            onTimerEndAction = null;
            onTimerPauseAction = null;

            IsPaused = false;
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;

            if (onTimerPauseAction != null)
                onTimerPauseAction.Invoke(IsPaused);
        }

        public Timer SetDuration(int seconds)
        {
            Duration = _remainingDuration = seconds;
            return this;
        }

        //-- Events ----------------------------------

        public Timer OnBegin(UnityAction action)
        {
            onTimerBeginAction = action;
            return this;
        }

        public Timer OnChange(UnityAction<int> action)
        {
            onTimerChangeAction = action;
            return this;
        }

        public Timer OnEnd(UnityAction action)
        {
            onTimerEndAction = action;
            return this;
        }

        public Timer OnPause(UnityAction<bool> action)
        {
            onTimerPauseAction = action;
            return this;
        }

        public void Begin()
        {
            if (onTimerBeginAction != null)
                onTimerBeginAction.Invoke();

            StopAllCoroutines();
            StartCoroutine(UpdateTimer());
        }

        private IEnumerator UpdateTimer()
        {
            while (_remainingDuration > 0)
            {
                if (!IsPaused)
                {
                    if (onTimerChangeAction != null)
                        onTimerChangeAction.Invoke(_remainingDuration);

                    UpdateUI(_remainingDuration);
                    _remainingDuration--;
                }

                yield return new WaitForSeconds(1f);
            }

            End();
        }

        private void UpdateUI(int seconds)
        {
            uiText.text = $"{seconds / 60:D2}:{seconds % 60:D2}";

            float percent = (float)seconds / Duration;

            uiFillImage.fillAmount = percent;

            Color currentColor;

            if (percent > yellowThreshold)
                currentColor = greenColor;
            else if (percent > redThreshold)
                currentColor = yellowColor;
            else
                currentColor = redColor;

            uiFillImage.color = currentColor;
            uiText.color = currentColor;
        }

        public void End()
        {
            if (onTimerEndAction != null)
                onTimerEndAction.Invoke();

            ResetTimer();
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }
    }
}
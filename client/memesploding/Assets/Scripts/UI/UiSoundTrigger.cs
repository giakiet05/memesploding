using UnityEngine;
using UnityEngine.EventSystems;

namespace Managers.Audio
{
    /// <summary>
    /// Attach to any UI element to play sounds on pointer click and/or hover.
    /// UI sounds play on the Sfx channel (controlled by the SFX volume slider).
    /// Fully configurable in the Inspector — no code changes required per-button.
    ///
    /// Usage:
    ///   1. Add this component to a Button, Panel, or any UI GameObject.
    ///   2. Set ClickSound and/or HoverSound to the desired SoundEvent.
    ///   3. Toggle PlayOnClick / PlayOnHover as needed.
    /// </summary>
    [AddComponentMenu("Memesploding/Audio/UI Sound Trigger")]
    public class UiSoundTrigger : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
    {
        [Header("Click")]
        [SerializeField] private bool playOnClick = true;
        [SerializeField] private SoundEvent clickSound = SoundEvent.UiButtonClick;

        [Header("Hover")]
        [SerializeField] private bool playOnHover = false;
        [SerializeField] private SoundEvent hoverSound = SoundEvent.CardHover;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (playOnClick)
                SoundManager.PlaySound(clickSound);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (playOnHover)
                SoundManager.PlaySound(hoverSound);
        }
    }
}

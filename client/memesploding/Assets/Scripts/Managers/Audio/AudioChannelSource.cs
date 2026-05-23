using UnityEngine;

namespace Managers.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioChannelSource : MonoBehaviour
    {
        [SerializeField] private AudioChannel channel = AudioChannel.Sfx;
        [SerializeField, Min(0f)] private float baseVolume = 1f;

        public AudioChannel Channel => channel;
        public float BaseVolume => baseVolume;
        public AudioSource Source { get; private set; }

        private void Awake()
        {
            Source = GetComponent<AudioSource>();
            if (Source != null)
                baseVolume = Source.volume;
        }

        private void OnEnable()
        {
            Source ??= GetComponent<AudioSource>();
            AudioManager.Instance.RegisterSource(this);
        }

        private void OnDisable()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.UnregisterSource(this);
        }
    }
}

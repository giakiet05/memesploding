using System.Collections;
using ScriptableObjects;
using UnityEngine;

namespace Managers.Audio
{
    /// <summary>
    /// Central sound playback manager with two channels:
    ///   • Music — one dedicated looping AudioSource with cross-fade support (BGM)
    ///   • Sfx   — round-robin pool of <see cref="sfxPoolSize"/> AudioSources
    ///
    /// Volume is scaled by AudioManager's per-channel settings and updated live
    /// whenever the player changes settings.
    ///
    /// SoundMap (SoundMapSO) is either:
    ///   a) Assigned in the Inspector (if this GameObject is placed in a scene), or
    ///   b) Auto-loaded from Resources/Audio/SoundMap at runtime.
    ///
    /// Play from anywhere: <c>SoundManager.PlaySound(SoundEvent.CardDraw);</c>
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        private const string SoundMapResourcePath = "Audio/SoundMap";

        // ── Singleton ─────────────────────────────────────────────────────────────

        private static SoundManager _instance;
        public static SoundManager Instance => EnsureInstance();

        public static SoundManager EnsureInstance()
        {
            if (_instance != null) return _instance;

            _instance = FindFirstObjectByType<SoundManager>(FindObjectsInactive.Include);
            if (_instance != null)
            {
                DontDestroyOnLoad(_instance.gameObject);
                return _instance;
            }

            var go = new GameObject(nameof(SoundManager));
            _instance = go.AddComponent<SoundManager>();
            return _instance;
        }

        // ── Inspector Fields ──────────────────────────────────────────────────────

        [Header("Sound Map")]
        [Tooltip("Assign the SoundMap asset here. If blank, auto-loads from Resources/Audio/SoundMap.")]
        [SerializeField] private SoundMapSO soundMap;

        [Header("Starting BGM  (optional)")]
        [Tooltip("AudioCueSO (Music channel) to play immediately on Awake.")]
        [SerializeField] private AudioCueSO startingBgm;

        [Header("SFX Pool")]
        [SerializeField, Min(1), Tooltip("Number of AudioSources in the SFX pool.")]
        private int sfxPoolSize = 10;

        // ── Runtime State ─────────────────────────────────────────────────────────

        // BGM — single dedicated source
        private AudioSource _bgmSource;
        private AudioCueSO  _currentBgmCue;
        private Coroutine   _bgmCoroutine;

        // SFX — round-robin pool
        private AudioSource[] _sfxPool;
        private int _sfxRR;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitPools();
            LoadSoundMap();

            if (startingBgm != null)
                PlayBgm(startingBgm);
        }

        private void OnEnable()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.OnVolumeChanged += HandleVolumeChanged;
        }

        private void OnDisable()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.OnVolumeChanged -= HandleVolumeChanged;
        }

        // ── Setup ─────────────────────────────────────────────────────────────────

        private void InitPools()
        {
            // BGM — single looping source
            _bgmSource = CreateSource("BGM");

            // SFX — round-robin pool
            _sfxPool = new AudioSource[sfxPoolSize];
            for (int i = 0; i < sfxPoolSize; i++)
                _sfxPool[i] = CreateSource($"SFX_{i:D2}");
        }

        private AudioSource CreateSource(string label)
        {
            var go = new GameObject($"[Audio] {label}");
            go.transform.SetParent(transform);
            return go.AddComponent<AudioSource>();
        }

        private void LoadSoundMap()
        {
            if (soundMap != null) return; // already assigned in Inspector
            soundMap = Resources.Load<SoundMapSO>(SoundMapResourcePath);
            if (soundMap == null)
                Debug.LogWarning(
                    $"[SoundManager] SoundMap not found at Resources/{SoundMapResourcePath}. " +
                    "Place the SoundMapSO asset there or assign it directly on the SoundManager component.");
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolve <paramref name="soundEvent"/> via the SoundMap and play it.
        /// Unmapped events are silently ignored.
        /// </summary>
        public void Play(SoundEvent soundEvent)
        {
            if (soundMap == null)
            {
                Debug.LogWarning($"[SoundManager] No SoundMap – cannot play {soundEvent}.");
                return;
            }

            if (!soundMap.TryGetCue(soundEvent, out var cue) || cue == null || !cue.HasClips)
                return;

            Dispatch(cue);
        }

        /// <summary>Play an AudioCueSO directly, bypassing the SoundMap lookup.</summary>
        public void PlayDirect(AudioCueSO cue)
        {
            if (cue == null || !cue.HasClips) return;
            Dispatch(cue);
        }

        /// <summary>
        /// Play (or cross-fade to) the given Music-channel cue as BGM.
        /// Uses <see cref="AudioCueSO.FadeDuration"/> for cross-fade when &gt; 0.
        /// </summary>
        public void PlayBgm(AudioCueSO cue)
        {
            if (cue == null || !cue.HasClips) return;

            if (_currentBgmCue == cue && _bgmSource != null && _bgmSource.isPlaying)
            {
                if (_bgmCoroutine == null)
                    _bgmSource.volume = cue.Volume * AudioManager.Instance.GetVolume(AudioChannel.Music);
                return;
            }

            _currentBgmCue = cue;

            if (_bgmCoroutine != null) StopCoroutine(_bgmCoroutine);

            if (cue.FadeDuration > 0f)
                _bgmCoroutine = StartCoroutine(CrossFade(cue));
            else
                ApplyBgmImmediate(cue);
        }

        /// <summary>Stop BGM, optionally fading out over <paramref name="fadeDuration"/> seconds.</summary>
        public void StopBgm(float fadeDuration = 0f)
        {
            if (_bgmCoroutine != null) StopCoroutine(_bgmCoroutine);

            if (fadeDuration > 0f)
                _bgmCoroutine = StartCoroutine(FadeOut(fadeDuration));
            else
                _bgmSource.Stop();
        }

        /// <summary>Static shorthand — safe to call from anywhere.</summary>
        public static void PlaySound(SoundEvent soundEvent) => _instance?.Play(soundEvent);

        // ── Dispatch ──────────────────────────────────────────────────────────────

        private void Dispatch(AudioCueSO cue)
        {
            switch (cue.Channel)
            {
                case AudioChannel.Music:
                    PlayBgm(cue);
                    break;
                case AudioChannel.Sfx:
                    PlaySfx(cue);
                    break;
            }
        }

        private void PlaySfx(AudioCueSO cue)
        {
            var clip = cue.GetClip();
            if (clip == null) return;

            var source = _sfxPool[_sfxRR % _sfxPool.Length];
            _sfxRR = (_sfxRR + 1) % _sfxPool.Length;

            float vol    = cue.Volume * AudioManager.Instance.GetVolume(AudioChannel.Sfx);
            source.pitch = cue.Pitch;
            source.PlayOneShot(clip, vol);
        }

        private void ApplyBgmImmediate(AudioCueSO cue)
        {
            var clip = cue.GetClip();
            if (clip == null) return;

            _bgmSource.clip   = clip;
            _bgmSource.loop   = cue.Loop;
            _bgmSource.pitch  = cue.Pitch;
            _bgmSource.volume = cue.Volume * AudioManager.Instance.GetVolume(AudioChannel.Music);
            _bgmSource.Play();
        }

        // ── BGM Coroutines ────────────────────────────────────────────────────────

        private IEnumerator CrossFade(AudioCueSO cue)
        {
            float half = cue.FadeDuration * 0.5f;

            // Fade out current
            if (_bgmSource.isPlaying)
            {
                float startVol = _bgmSource.volume;
                for (float t = 0f; t < half; t += Time.deltaTime)
                {
                    _bgmSource.volume = Mathf.Lerp(startVol, 0f, t / half);
                    yield return null;
                }
                _bgmSource.Stop();
            }

            // Swap clip and fade in
            var clip = cue.GetClip();
            if (clip == null) { _bgmCoroutine = null; yield break; }

            float targetVol   = cue.Volume * AudioManager.Instance.GetVolume(AudioChannel.Music);
            _bgmSource.clip   = clip;
            _bgmSource.loop   = cue.Loop;
            _bgmSource.pitch  = cue.Pitch;
            _bgmSource.volume = 0f;
            _bgmSource.Play();

            for (float t = 0f; t < half; t += Time.deltaTime)
            {
                _bgmSource.volume = Mathf.Lerp(0f, targetVol, t / half);
                yield return null;
            }

            _bgmSource.volume = targetVol;
            _bgmCoroutine     = null;
        }

        private IEnumerator FadeOut(float duration)
        {
            float startVol = _bgmSource.volume;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                _bgmSource.volume = Mathf.Lerp(startVol, 0f, t / duration);
                yield return null;
            }
            _bgmSource.Stop();
            _bgmCoroutine = null;
        }

        // ── Volume Change Handler ─────────────────────────────────────────────────

        private void HandleVolumeChanged(AudioChannel channel)
        {
            if (channel != AudioChannel.Music) return;
            if (!_bgmSource.isPlaying || _currentBgmCue == null) return;
            // Sync BGM volume immediately (unless a fade coroutine is running)
            if (_bgmCoroutine == null)
                _bgmSource.volume = _currentBgmCue.Volume * AudioManager.Instance.GetVolume(AudioChannel.Music);
        }
    }
}

using Managers.Audio;
using UnityEngine;

namespace ScriptableObjects
{
    /// <summary>
    /// Data container for a single audio cue.
    /// Assign one or more AudioClips — if multiple are provided, one is picked randomly at runtime.
    ///
    /// Create via: Assets → Create → Memesploding → Audio → Audio Cue
    /// </summary>
    [CreateAssetMenu(fileName = "NewAudioCue", menuName = "Memesploding/Audio/Audio Cue")]
    public class AudioCueSO : ScriptableObject
    {
        // ── Clips ─────────────────────────────────────────────────────────────────

        [Header("Clips  (multiple = random pick each play)")]
        [SerializeField] private AudioClip[] clips = System.Array.Empty<AudioClip>();

        // ── Playback ──────────────────────────────────────────────────────────────

        [Header("Playback")]
        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        [SerializeField, Range(0.5f, 2f)]
        [Tooltip("Minimum pitch. Set equal to PitchMax for no randomisation.")]
        private float pitchMin = 1f;

        [SerializeField, Range(0.5f, 2f)]
        [Tooltip("Maximum pitch. Set equal to PitchMin for no randomisation.")]
        private float pitchMax = 1f;

        [SerializeField]
        [Tooltip("Loop the clip. Enable for BGM; leave off for SFX.")]
        private bool loop = false;

        // ── BGM Fade ──────────────────────────────────────────────────────────────

        [Header("BGM Cross-Fade  (seconds, 0 = instant switch)")]
        [SerializeField, Min(0f)]
        [Tooltip("Only used for Music channel cues. Controls cross-fade duration when switching BGM.")]
        private float fadeDuration = 0f;

        // ── Channel ───────────────────────────────────────────────────────────────

        [Header("Channel")]
        [Tooltip("Music = BGM looping source.  Sfx = SFX round-robin pool.")]
        [SerializeField] private AudioChannel channel = AudioChannel.Sfx;

        // ── Properties ────────────────────────────────────────────────────────────

        /// <summary>True if at least one clip is assigned.</summary>
        public bool HasClips => clips != null && clips.Length > 0;

        /// <summary>Returns a random clip from the list (or the single clip if only one).</summary>
        public AudioClip GetClip()
        {
            if (clips == null || clips.Length == 0) return null;
            return clips.Length == 1 ? clips[0] : clips[Random.Range(0, clips.Length)];
        }

        public float Volume       => volume;
        public float Pitch        => Random.Range(pitchMin, pitchMax);
        public bool  Loop         => loop;
        public float FadeDuration => fadeDuration;
        public AudioChannel Channel => channel;
    }
}

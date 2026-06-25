using System;
using System.Collections.Generic;
using Managers.Audio;
using UnityEngine;

namespace ScriptableObjects
{
    /// <summary>
    /// Editor-assignable mapping of every SoundEvent to its AudioCueSO.
    ///
    /// Usage:
    ///   1. Create one instance: Assets → Create → Memesploding → Audio → Sound Map
    ///   2. Place it at: Assets/Resources/Audio/SoundMap.asset  (auto-loaded by SoundManager)
    ///      OR assign it directly to SoundManager in the Inspector.
    ///   3. For each row, pick a SoundEvent from the dropdown and drag in an AudioCueSO.
    ///
    /// Entries with no AudioCueSO assigned are silently skipped at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "SoundMap", menuName = "Memesploding/Audio/Sound Map")]
    public class SoundMapSO : ScriptableObject
    {
        // ── Data ──────────────────────────────────────────────────────────────────

        [Serializable]
        public struct Entry
        {
            [Tooltip("The game moment that triggers this cue.")]
            public SoundEvent soundEvent;

            [Tooltip("The audio cue to play when that moment fires.")]
            public AudioCueSO cue;
        }

        [SerializeField]
        [Tooltip("Map every SoundEvent to an AudioCueSO. Order doesn't matter.")]
        private Entry[] entries = Array.Empty<Entry>();

        // ── Runtime Lookup ────────────────────────────────────────────────────────

        private Dictionary<SoundEvent, AudioCueSO> _lookup;

        // Rebuild on load and in-editor on validation
        private void OnEnable()  => BuildLookup();

#if UNITY_EDITOR
        private void OnValidate() => BuildLookup();
#endif

        private void BuildLookup()
        {
            _lookup = new Dictionary<SoundEvent, AudioCueSO>();
            if (entries == null) return;
            foreach (var e in entries)
            {
                if (e.cue != null)
                    _lookup[e.soundEvent] = e.cue;
            }
        }

        /// <summary>Returns true and outputs the cue if the event has a mapping.</summary>
        public bool TryGetCue(SoundEvent soundEvent, out AudioCueSO cue)
        {
            if (_lookup == null) BuildLookup();
            return _lookup.TryGetValue(soundEvent, out cue);
        }

        /// <summary>Returns the cue for a sound event, or null if not mapped.</summary>
        public AudioCueSO GetCue(SoundEvent soundEvent)
        {
            TryGetCue(soundEvent, out var cue);
            return cue;
        }
    }
}

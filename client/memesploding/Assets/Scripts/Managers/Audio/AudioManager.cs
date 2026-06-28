using System;
using System.Collections.Generic;
using UnityEngine;

namespace Managers.Audio
{
    public enum AudioChannel
    {
        Music,
        Sfx
    }

    public class AudioManager : MonoBehaviour
    {
        private const string MusicVolumeKey = "memesploding.audio.music";
        private const string SfxVolumeKey   = "memesploding.audio.sfx";
        private const float  DefaultVolume  = 1f;

        private static AudioManager _instance;

        private readonly Dictionary<AudioChannel, HashSet<AudioChannelSource>> _sources = new()
        {
            { AudioChannel.Music, new HashSet<AudioChannelSource>() },
            { AudioChannel.Sfx,   new HashSet<AudioChannelSource>() },
        };

        public static AudioManager Instance => EnsureInstance();

        public float MusicVolume { get; private set; } = DefaultVolume;
        public float SfxVolume   { get; private set; } = DefaultVolume;

        /// <summary>Raised whenever any channel's volume changes. Arg = the changed channel.</summary>
        public event Action<AudioChannel> OnVolumeChanged;

        public static AudioManager EnsureInstance()
        {
            if (_instance != null)
                return _instance;

            _instance = FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);
            if (_instance != null)
            {
                DontDestroyOnLoad(_instance.gameObject);
                _instance.LoadSettings();
                return _instance;
            }

            var gameObject = new GameObject(nameof(AudioManager));
            _instance = gameObject.AddComponent<AudioManager>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }

        public void RegisterSource(AudioChannelSource source)
        {
            if (source == null) return;
            _sources[source.Channel].Add(source);
            ApplyVolume(source);
        }

        public void UnregisterSource(AudioChannelSource source)
        {
            if (source == null) return;
            _sources[source.Channel].Remove(source);
        }

        public void SetMusicVolume(float volume, bool persist = true)
        {
            MusicVolume = Mathf.Clamp01(volume);
            ApplyVolumes(AudioChannel.Music);
            if (persist) PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
            OnVolumeChanged?.Invoke(AudioChannel.Music);
        }

        public void SetSfxVolume(float volume, bool persist = true)
        {
            SfxVolume = Mathf.Clamp01(volume);
            ApplyVolumes(AudioChannel.Sfx);
            if (persist) PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
            OnVolumeChanged?.Invoke(AudioChannel.Sfx);
        }

        public float GetVolume(AudioChannel channel) => channel switch
        {
            AudioChannel.Music => MusicVolume,
            AudioChannel.Sfx   => SfxVolume,
            _                  => DefaultVolume,
        };

        internal void ApplyVolume(AudioChannelSource source)
        {
            if (source == null || source.Source == null) return;
            source.Source.volume = source.BaseVolume * GetVolume(source.Channel);
            source.Source.mute   = GetVolume(source.Channel) <= 0.001f;
        }

        private void LoadSettings()
        {
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultVolume));
            SfxVolume   = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey,   DefaultVolume));
            ApplyVolumes(AudioChannel.Music);
            ApplyVolumes(AudioChannel.Sfx);
        }

        private void ApplyVolumes(AudioChannel channel)
        {
            _sources[channel].RemoveWhere(source => source == null || source.Source == null);
            foreach (var source in _sources[channel])
                ApplyVolume(source);
        }
    }
}

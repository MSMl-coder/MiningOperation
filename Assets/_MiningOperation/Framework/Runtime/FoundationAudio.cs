using System;
using UnityEngine;

namespace MiningOperation.Framework
{
    public enum FoundationAudioBus { Music, Sfx, UI, Ambient, Voice }

    /// <summary>Foundation per-bus audio playback. Master applied by existing v0.4 settings service.</summary>
    public sealed class FoundationAudioService : MonoBehaviour
    {
        public static FoundationAudioService Instance { get; private set; }
        private AudioSource _source;
        private AudioClip _testClick;
        private float[] _busVolumes = { 0.7f, 0.8f, 0.8f, 0.7f, 0.8f };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // Menus in Foundation have no camera, so provide a 2D listener if none exists.
            // Later 3D scenes should transfer listener ownership to their active camera.
            if (FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 0)
                gameObject.AddComponent<AudioListener>();
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0;
            _source.ignoreListenerPause = true;
            _testClick = BuildTestTone();
            ApplyPreferences();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            if (_testClick != null) Destroy(_testClick);
        }

        public void ApplyPreferences()
        {
            var settings = FoundationSettingsServices.Settings.Current;
            _busVolumes[(int)FoundationAudioBus.Music] = Mathf.Clamp01(settings.musicVolume);
            _busVolumes[(int)FoundationAudioBus.Sfx] = Mathf.Clamp01(settings.sfxVolume);
            _busVolumes[(int)FoundationAudioBus.UI] = Mathf.Clamp01(settings.uiVolume);
            _busVolumes[(int)FoundationAudioBus.Ambient] = Mathf.Clamp01(settings.ambientVolume);
            _busVolumes[(int)FoundationAudioBus.Voice] = Mathf.Clamp01(settings.voiceVolume);
        }

        public void PlayOneShot(AudioClip clip, FoundationAudioBus bus, float gain = 1f)
        {
            if (_source == null || clip == null) return;
            _source.PlayOneShot(clip, Mathf.Clamp01(gain) * _busVolumes[(int)bus]);
        }

        public void PlayTestClick()
        {
            PlayOneShot(_testClick, FoundationAudioBus.UI, 0.35f);
        }

        private static AudioClip BuildTestTone()
        {
            const int rate = 22050;
            const int frames = 1600;
            var samples = new float[frames];
            for (int i = 0; i < frames; i++)
            {
                float fade = 1f - i / (float)frames;
                samples[i] = Mathf.Sin(2f * Mathf.PI * 660f * i / rate) * fade * fade * 0.16f;
            }
            var clip = AudioClip.Create("MO_UI_Test_Click", frames, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}

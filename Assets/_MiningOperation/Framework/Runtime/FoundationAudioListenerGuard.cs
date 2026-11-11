using System;
using UnityEngine;

namespace MiningOperation.Framework
{
    /// <summary>
    /// Optional transition guard: if a game camera owns a listener, disable the temporary
    /// listener on the FoundationAudioService. If no camera listener exists, restore it.
    /// Attached automatically to the existing audio service. Does not create new listeners.
    /// </summary>
    public sealed class FoundationAudioListenerGuard : MonoBehaviour
    {
        private AudioListener _foundationListener;
        private bool _warnedAboutExtra;
        private float _nextCheck;

        // RuntimeInitializeOnLoadMethod ordering across assemblies is not guaranteed.
        // Bootstrap retries until the existing audio service has actually been created.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            var go = new GameObject("MO_AudioGuardInstaller_v09");
            go.AddComponent<FoundationAudioGuardInstaller>();
            DontDestroyOnLoad(go);
        }


        private void Awake()
        {
            _foundationListener = GetComponent<AudioListener>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 0.5f;
            CheckNow();
        }

        public void CheckNow()
        {
            if (_foundationListener == null) _foundationListener = GetComponent<AudioListener>();
            var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int externalActive = 0;
            foreach (var listener in listeners)
            {
                if (listener == null || listener == _foundationListener) continue;
                if (listener.enabled && listener.gameObject.activeInHierarchy) externalActive++;
            }
            if (_foundationListener != null)
            {
                bool shouldEnable = externalActive == 0;
                if (_foundationListener.enabled != shouldEnable)
                {
                    _foundationListener.enabled = shouldEnable;
                    Debug.Log("[MO/Audio] Foundation listener " + (shouldEnable ? "enabled" : "disabled") + " (scene listener handoff).");
                }
            }
            if (externalActive > 1 && !_warnedAboutExtra)
            {
                _warnedAboutExtra = true;
                Debug.LogWarning("[MO/Audio] Multiple SCENE AudioListeners detected (" + externalActive + "). Disable extras manually.");
            }
            else if (externalActive <= 1) _warnedAboutExtra = false;
        }
    }

    public sealed class FoundationAudioGuardInstaller : MonoBehaviour
    {
        private void Update()
        {
            var service = FoundationAudioService.Instance;
            if (service == null) return;
            if (service.GetComponent<FoundationAudioListenerGuard>() == null)
                service.gameObject.AddComponent<FoundationAudioListenerGuard>();
            Destroy(gameObject);
        }
    }
}

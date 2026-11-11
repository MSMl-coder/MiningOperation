using System;
using UnityEngine;

namespace MiningOperation.Framework
{
    /// <summary>Extension root: does not modify the verified Bootstrap and session lifecycle.</summary>
    public sealed class FoundationFeatureRuntime : MonoBehaviour
    {
        private static FoundationFeatureRuntime _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (_instance != null) return;
            var go = new GameObject("MO_Features_v06");
            _instance = go.AddComponent<FoundationFeatureRuntime>();
            go.AddComponent<FoundationAudioService>();
            go.AddComponent<FoundationInputService>();
            DontDestroyOnLoad(go);
        }

        private async void Start()
        {
            try
            {
                await FoundationSettingsServices.EnsureLoadedAsync();
                if (this != null) FoundationAudioService.Instance?.ApplyPreferences();
                Debug.Log("[MO/FEATURES] v0.6 ready");
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}

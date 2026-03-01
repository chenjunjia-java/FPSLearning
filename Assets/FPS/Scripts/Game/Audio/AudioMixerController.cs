using UnityEngine;

namespace Unity.FPS.Game
{
    /// <summary>
    /// Centralized API for exposed audio parameters (volume). Ducking 由 Mixer 的 Duck Volume 处理。
    /// </summary>
    public sealed class AudioMixerController : MonoBehaviour
    {
        public const string MasterVolumeParameter = "MasterVolume";
        public const string MusicVolumeParameter = "MusicVolume";
        public const string SfxVolumeParameter = "SfxVolume";
        public const string UiVolumeParameter = "UiVolume";
        public const string AmbienceVolumeParameter = "AmbienceVolume";

        private static AudioMixerController s_Instance;
        private AudioManager m_AudioManager;

        public static AudioMixerController Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = FindObjectOfType<AudioMixerController>();
                    if (s_Instance == null)
                    {
                        var root = new GameObject("AudioMixerController");
                        s_Instance = root.AddComponent<AudioMixerController>();
                    }
                }

                return s_Instance;
            }
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            DontDestroyOnLoad(gameObject);
            ResolveAudioManager();
        }

        public static void SetLinearVolume(string parameterName, float linearValue)
        {
            if (string.IsNullOrEmpty(parameterName))
            {
                return;
            }

            Instance.ResolveAudioManager();
            if (Instance.m_AudioManager == null)
            {
                return;
            }

            float safeLinear = Mathf.Max(0.001f, linearValue);
            float valueInDb = Mathf.Log10(safeLinear) * 20f;
            Instance.m_AudioManager.SetFloat(parameterName, valueInDb);
        }

        public static float GetLinearVolume(string parameterName, float defaultValue = 1f)
        {
            if (string.IsNullOrEmpty(parameterName))
            {
                return defaultValue;
            }

            Instance.ResolveAudioManager();
            if (Instance.m_AudioManager == null)
            {
                return defaultValue;
            }

            Instance.m_AudioManager.GetFloat(parameterName, out float valueInDb);
            return Mathf.Pow(10f, valueInDb / 20f);
        }

        private void ResolveAudioManager()
        {
            if (m_AudioManager == null)
            {
                m_AudioManager = FindObjectOfType<AudioManager>();
            }
        }
    }
}

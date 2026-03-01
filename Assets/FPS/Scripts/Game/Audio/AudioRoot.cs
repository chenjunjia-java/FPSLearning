using UnityEngine;

namespace Unity.FPS.Game
{
    /// <summary>
    /// Persistent entrypoint that keeps audio services alive across scene loads.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioRoot : MonoBehaviour
    {
        [SerializeField] private bool m_BootstrapOnAwake = true;

        private static AudioRoot s_Instance;

        public static AudioRoot Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = FindObjectOfType<AudioRoot>();
                    if (s_Instance == null)
                    {
                        GameObject root = new GameObject("AudioRoot");
                        s_Instance = root.AddComponent<AudioRoot>();
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

            if (m_BootstrapOnAwake)
            {
                BootstrapServices();
            }
        }

        public void BootstrapServices()
        {
            _ = AudioMixerController.Instance;
            _ = MusicPlayer.Instance;
            _ = SfxService.Instance;
        }
    }
}

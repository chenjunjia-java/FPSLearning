using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Unity.FPS.Game
{
    /// <summary>
    /// Persistent layered music player with crossfade between two banks.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        private const int LayerCount = 4;
        private const double ScheduleLeadTimeSeconds = 0.05d;

        [SerializeField] private string m_MusicGroupName = "Music";
        [SerializeField] private float m_DefaultFadeSeconds = 1.2f;

        private static MusicPlayer s_Instance;

        private AudioSource[] m_BankA;
        private AudioSource[] m_BankB;
        private bool m_BankAIsActive = true;
        private Coroutine m_CrossfadeRoutine;
        private MusicSetSO m_CurrentSet;
        private AudioMixerGroup m_MusicGroup;

        public static MusicPlayer Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = FindObjectOfType<MusicPlayer>();
                    if (s_Instance == null)
                    {
                        GameObject root = new GameObject("MusicPlayer");
                        s_Instance = root.AddComponent<MusicPlayer>();
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

            InitializeBanksIfNeeded();
            ResolveMusicGroupIfNeeded();
        }

        public static void PlaySet(MusicSetSO set, float fadeSeconds = -1f)
        {
            if (set == null)
            {
                return;
            }

            Instance.InternalPlaySet(set, fadeSeconds);
        }

        public static void StopMusic(float fadeSeconds = -1f)
        {
            Instance.InternalStopMusic(fadeSeconds);
        }

        private void InternalPlaySet(MusicSetSO set, float fadeSeconds)
        {
            InitializeBanksIfNeeded();
            ResolveMusicGroupIfNeeded();

            AudioSource[] activeBank = m_BankAIsActive ? m_BankA : m_BankB;
            AudioSource[] inactiveBank = m_BankAIsActive ? m_BankB : m_BankA;
            bool hasActivePlayback = HasAnyPlayingSource(activeBank);

            if (m_CurrentSet == set && hasActivePlayback)
            {
                return;
            }

            AssignSetToBank(inactiveBank, set);
            ScheduleBankPlay(inactiveBank, set.Loop);

            if (m_CrossfadeRoutine != null)
            {
                StopCoroutine(m_CrossfadeRoutine);
            }

            float duration = fadeSeconds >= 0f ? fadeSeconds : m_DefaultFadeSeconds;

            if (!hasActivePlayback)
            {
                SetBankVolumes(inactiveBank, set, 1f);
                StopBank(activeBank);
                m_BankAIsActive = !m_BankAIsActive;
            }
            else
            {
                m_CrossfadeRoutine = StartCoroutine(CrossfadeRoutine(activeBank, inactiveBank, set, duration));
            }

            m_CurrentSet = set;
        }

        private void InternalStopMusic(float fadeSeconds)
        {
            InitializeBanksIfNeeded();

            AudioSource[] activeBank = m_BankAIsActive ? m_BankA : m_BankB;
            AudioSource[] inactiveBank = m_BankAIsActive ? m_BankB : m_BankA;

            if (m_CrossfadeRoutine != null)
            {
                StopCoroutine(m_CrossfadeRoutine);
                m_CrossfadeRoutine = null;
            }

            float duration = fadeSeconds >= 0f ? fadeSeconds : m_DefaultFadeSeconds;
            if (duration <= 0f)
            {
                StopBank(activeBank);
                StopBank(inactiveBank);
                m_CurrentSet = null;
                return;
            }

            StartCoroutine(FadeOutAndStopRoutine(activeBank, inactiveBank, duration));
        }

        private IEnumerator FadeOutAndStopRoutine(AudioSource[] activeBank, AudioSource[] inactiveBank, float duration)
        {
            float[] initialVolumes = new float[LayerCount];
            for (int i = 0; i < LayerCount; i++)
            {
                if (activeBank[i] != null)
                {
                    initialVolumes[i] = activeBank[i].volume;
                }
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float fade = 1f - t;
                for (int i = 0; i < LayerCount; i++)
                {
                    if (activeBank[i] != null)
                    {
                        activeBank[i].volume = initialVolumes[i] * fade;
                    }
                }

                yield return null;
            }

            StopBank(activeBank);
            StopBank(inactiveBank);
            m_CurrentSet = null;
        }

        private IEnumerator CrossfadeRoutine(AudioSource[] fromBank, AudioSource[] toBank, MusicSetSO targetSet, float duration)
        {
            duration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetBankVolumes(fromBank, m_CurrentSet, 1f - t);
                SetBankVolumes(toBank, targetSet, t);
                yield return null;
            }

            SetBankVolumes(toBank, targetSet, 1f);
            StopBank(fromBank);

            m_BankAIsActive = !m_BankAIsActive;
            m_CrossfadeRoutine = null;
        }

        private void InitializeBanksIfNeeded()
        {
            if (m_BankA != null && m_BankB != null)
            {
                return;
            }

            m_BankA = CreateBank("BankA");
            m_BankB = CreateBank("BankB");
        }

        private AudioSource[] CreateBank(string bankName)
        {
            var bank = new AudioSource[LayerCount];
            Transform bankRoot = new GameObject(bankName).transform;
            bankRoot.SetParent(transform, false);

            for (int i = 0; i < LayerCount; i++)
            {
                GameObject sourceObject = new GameObject("Layer" + i);
                sourceObject.transform.SetParent(bankRoot, false);
                AudioSource source = sourceObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.volume = 0f;
                source.priority = 64;
                bank[i] = source;
            }

            return bank;
        }

        private void ResolveMusicGroupIfNeeded()
        {
            if (m_MusicGroup != null)
            {
                return;
            }

            AudioMixerGroup[] groups = null;
            if (!string.IsNullOrEmpty(m_MusicGroupName))
            {
                AudioManager manager = FindObjectOfType<AudioManager>();
                if (manager != null)
                {
                    groups = manager.FindMatchingGroups(m_MusicGroupName);
                }
            }

            if (groups != null && groups.Length > 0)
            {
                m_MusicGroup = groups[0];
            }
            else
            {
                // Fallback for old mixer setups where background music still routes through Ambient.
                AudioManager manager = FindObjectOfType<AudioManager>();
                if (manager != null)
                {
                    AudioMixerGroup[] ambientGroups = manager.FindMatchingGroups("Ambient");
                    if (ambientGroups != null && ambientGroups.Length > 0)
                    {
                        m_MusicGroup = ambientGroups[0];
                    }
                }
            }

            ApplyOutputGroup(m_BankA, m_MusicGroup);
            ApplyOutputGroup(m_BankB, m_MusicGroup);
        }

        private static void ApplyOutputGroup(AudioSource[] bank, AudioMixerGroup outputGroup)
        {
            if (bank == null)
            {
                return;
            }

            for (int i = 0; i < bank.Length; i++)
            {
                if (bank[i] != null)
                {
                    bank[i].outputAudioMixerGroup = outputGroup;
                }
            }
        }

        private static void AssignSetToBank(AudioSource[] bank, MusicSetSO set)
        {
            if (bank == null || set == null)
            {
                return;
            }

            bank[0].clip = set.Wind;
            bank[1].clip = set.Noise;
            bank[2].clip = set.Environment;
            bank[3].clip = set.Main;

            for (int i = 0; i < LayerCount; i++)
            {
                bank[i].volume = 0f;
                bank[i].loop = set.Loop;
            }
        }

        private static void ScheduleBankPlay(AudioSource[] bank, bool loop)
        {
            double startDspTime = AudioSettings.dspTime + ScheduleLeadTimeSeconds;
            for (int i = 0; i < LayerCount; i++)
            {
                AudioSource source = bank[i];
                if (source == null)
                {
                    continue;
                }

                source.Stop();
                source.loop = loop;
                if (source.clip == null)
                {
                    continue;
                }

                source.PlayScheduled(startDspTime);
            }
        }

        private static bool HasAnyPlayingSource(AudioSource[] bank)
        {
            if (bank == null)
            {
                return false;
            }

            for (int i = 0; i < bank.Length; i++)
            {
                AudioSource source = bank[i];
                if (source != null && source.isPlaying)
                {
                    return true;
                }
            }

            return false;
        }

        private static void StopBank(AudioSource[] bank)
        {
            if (bank == null)
            {
                return;
            }

            for (int i = 0; i < bank.Length; i++)
            {
                AudioSource source = bank[i];
                if (source == null)
                {
                    continue;
                }

                source.Stop();
                source.clip = null;
                source.volume = 0f;
            }
        }

        private static float GetLayerVolume(MusicSetSO set, int layerIndex)
        {
            if (set == null)
            {
                return 0f;
            }

            switch (layerIndex)
            {
                case 0:
                    return set.WindVolume;
                case 1:
                    return set.NoiseVolume;
                case 2:
                    return set.EnvironmentVolume;
                case 3:
                    return set.MainVolume;
                default:
                    return 0f;
            }
        }

        private static void SetBankVolumes(AudioSource[] bank, MusicSetSO set, float bankWeight)
        {
            if (bank == null)
            {
                return;
            }

            for (int i = 0; i < LayerCount; i++)
            {
                if (bank[i] == null)
                {
                    continue;
                }

                bank[i].volume = GetLayerVolume(set, i) * bankWeight;
            }
        }
    }
}

using System.Collections.Generic;
using Unity.FPS.GameFramework;
using UnityEngine;
using UnityEngine.Audio;

namespace Unity.FPS.Game
{
    /// <summary>
    /// Unified SFX entry that routes through pooled emitters.
    /// </summary>
    public sealed class SfxService : MonoBehaviour
    {
        [Header("Catalog")]
        [SerializeField] private SfxCatalogSO m_Catalog;

        [SerializeField] private SfxEmitter m_EmitterPrefab;
        [SerializeField] [Min(1)] private int m_DefaultMaxPoolSize = 128;
        [SerializeField] [Min(0)] private int m_DefaultPrewarmCount = 12;
        [SerializeField] [Min(0f)] private float m_DefaultGroupCooldownSeconds = 0f;

        private static SfxService s_Instance;

        private readonly Dictionary<AudioUtility.AudioGroups, float> m_LastPlayTimeByGroup =
            new Dictionary<AudioUtility.AudioGroups, float>();

        private readonly Dictionary<AudioUtility.AudioGroups, float> m_CustomCooldownByGroup =
            new Dictionary<AudioUtility.AudioGroups, float>();

        private readonly Dictionary<SfxKey, float> m_LastPlayTimeByKey = new Dictionary<SfxKey, float>(128);

        private SfxEmitter m_RuntimeEmitterTemplate;

        public static SfxService Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = FindObjectOfType<SfxService>();
                    if (s_Instance == null)
                    {
                        var root = new GameObject("SfxService");
                        s_Instance = root.AddComponent<SfxService>();
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

            if (m_Catalog == null)
            {
                var audioManager = FindObjectOfType<AudioManager>();
                if (audioManager != null)
                {
                    m_Catalog = audioManager.SfxCatalog;
                }
            }

            EnsureEmitterTemplate();

            // A conservative default for potential warning-like sounds.
            SetGroupCooldown(AudioUtility.AudioGroups.EnemyDetection, 0.08f);
        }

        public static void SetCatalog(SfxCatalogSO catalog)
        {
            Instance.m_Catalog = catalog;
        }

        public static void SetGroupCooldown(AudioUtility.AudioGroups group, float cooldownSeconds)
        {
            Instance.m_CustomCooldownByGroup[group] = Mathf.Max(0f, cooldownSeconds);
        }

        public static bool Play(SfxKey key, Vector3 position)
        {
            if (key == SfxKey.None)
            {
                return false;
            }

            return Instance.InternalPlayFromCatalog(key, position, 1f, null);
        }

        /// <param name="parent">挂载音源的父物体，可为 null 表示在世界坐标播放。</param>
        public static bool Play(SfxKey key, Vector3 position, Transform parent)
        {
            if (key == SfxKey.None)
            {
                return false;
            }

            return Instance.InternalPlayFromCatalog(key, position, 1f, parent);
        }

        public static bool Play(SfxKey key, Vector3 position, float volumeMultiplier)
        {
            if (key == SfxKey.None)
            {
                return false;
            }

            return Instance.InternalPlayFromCatalog(key, position, volumeMultiplier, null);
        }

        public static bool Play(SfxKey key, Vector3 position, float volumeMultiplier, Transform parent)
        {
            if (key == SfxKey.None)
            {
                return false;
            }

            return Instance.InternalPlayFromCatalog(key, position, volumeMultiplier, parent);
        }

        public static bool Play(string legacyKey, Vector3 position)
        {
            return SfxKeys.TryParse(legacyKey, out SfxKey key) && Play(key, position);
        }

        public static bool Play(string legacyKey, Vector3 position, float volumeMultiplier)
        {
            return SfxKeys.TryParse(legacyKey, out SfxKey key) && Play(key, position, volumeMultiplier);
        }

        public static bool TryGetCatalogEntry(SfxKey key, out SfxCatalogSO.Entry entry)
        {
            return Instance.TryGetCatalogEntryInternal(key, out entry);
        }

        public static void Play(AudioClip clip, Vector3 position, AudioUtility.AudioGroups audioGroup, float spatialBlend,
            float rolloffDistanceMin = 1f, float volume = 1f, float pitch = 1f)
        {
            if (clip == null)
            {
                return;
            }

            Instance.InternalPlay(clip, position, audioGroup, spatialBlend, rolloffDistanceMin, volume, pitch);
        }

        private bool InternalPlayFromCatalog(SfxKey key, Vector3 position, float volumeMultiplier = 1f, Transform parent = null)
        {
            if (m_Catalog == null)
            {
                return false;
            }

            if (!m_Catalog.TryGet(key, out SfxCatalogSO.Entry entry))
            {
                return false;
            }

            if (!PassCooldownByKey(key, entry.CooldownSeconds))
            {
                return true;
            }

            float pitch = entry.PitchRange.x == entry.PitchRange.y
                ? entry.PitchRange.x
                : Random.Range(entry.PitchRange.x, entry.PitchRange.y);

            float volume = Mathf.Clamp01(entry.Volume * Mathf.Clamp01(volumeMultiplier));
            InternalPlay(entry.Clip, position, entry.Group, entry.SpatialBlend, entry.MinDistance, volume, pitch, parent);
            return true;
        }

        private bool TryGetCatalogEntryInternal(SfxKey key, out SfxCatalogSO.Entry entry)
        {
            if (m_Catalog == null)
            {
                entry = default;
                return false;
            }

            return m_Catalog.TryGet(key, out entry);
        }

        private void InternalPlay(AudioClip clip, Vector3 position, AudioUtility.AudioGroups audioGroup, float spatialBlend,
            float rolloffDistanceMin, float volume, float pitch, Transform parent = null)
        {
            if (!PassCooldown(audioGroup))
            {
                return;
            }

            AudioMixerGroup mixerGroup = AudioUtility.GetAudioGroup(audioGroup);
            EnsureEmitterTemplate();

            ObjPrefabManager prefabManager = ObjPrefabManager.Instance;
            if (prefabManager != null && m_RuntimeEmitterTemplate != null)
            {
                SfxEmitter emitter = prefabManager.Spawn(m_RuntimeEmitterTemplate, position, Quaternion.identity, null,
                    m_DefaultMaxPoolSize);
                if (emitter != null)
                {
                    emitter.Play(clip, mixerGroup, position, spatialBlend, rolloffDistanceMin, volume, pitch, false, parent);
                    return;
                }
            }

            PlayFallback(clip, mixerGroup, position, spatialBlend, rolloffDistanceMin, volume, pitch);
        }

        private void EnsureEmitterTemplate()
        {
            if (m_EmitterPrefab != null)
            {
                ObjPrefabManager manager = ObjPrefabManager.Instance;
                if (manager != null)
                {
                    manager.Load(m_EmitterPrefab, m_DefaultPrewarmCount, m_DefaultMaxPoolSize);
                }

                m_RuntimeEmitterTemplate = m_EmitterPrefab;
                return;
            }

            if (m_RuntimeEmitterTemplate != null)
            {
                return;
            }

            var templateRoot = new GameObject("RuntimeSfxEmitterTemplate");
            templateRoot.SetActive(false);
            DontDestroyOnLoad(templateRoot);

            var source = templateRoot.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;

            templateRoot.AddComponent<TimedSelfDestruct>();
            m_RuntimeEmitterTemplate = templateRoot.AddComponent<SfxEmitter>();

            ObjPrefabManager managerInstance = ObjPrefabManager.Instance;
            if (managerInstance != null)
            {
                managerInstance.Load(m_RuntimeEmitterTemplate, m_DefaultPrewarmCount, m_DefaultMaxPoolSize);
            }
        }

        private bool PassCooldown(AudioUtility.AudioGroups group)
        {
            float now = Time.unscaledTime;
            float cooldown = m_DefaultGroupCooldownSeconds;
            if (m_CustomCooldownByGroup.TryGetValue(group, out float customCooldown))
            {
                cooldown = customCooldown;
            }

            if (cooldown <= 0f)
            {
                m_LastPlayTimeByGroup[group] = now;
                return true;
            }

            if (m_LastPlayTimeByGroup.TryGetValue(group, out float lastTime))
            {
                if (now - lastTime < cooldown)
                {
                    return false;
                }
            }

            m_LastPlayTimeByGroup[group] = now;
            return true;
        }

        private bool PassCooldownByKey(SfxKey key, float cooldownSeconds)
        {
            float cooldown = Mathf.Max(0f, cooldownSeconds);
            if (cooldown <= 0f)
            {
                m_LastPlayTimeByKey[key] = Time.unscaledTime;
                return true;
            }

            float now = Time.unscaledTime;
            if (m_LastPlayTimeByKey.TryGetValue(key, out float lastTime))
            {
                if (now - lastTime < cooldown)
                {
                    return false;
                }
            }

            m_LastPlayTimeByKey[key] = now;
            return true;
        }

        private static void PlayFallback(AudioClip clip, AudioMixerGroup mixerGroup, Vector3 position, float spatialBlend,
            float rolloffDistanceMin, float volume, float pitch)
        {
            var go = new GameObject("FallbackSfx");
            go.transform.position = position;

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = mixerGroup;
            source.clip = clip;
            source.spatialBlend = Mathf.Clamp01(spatialBlend);
            source.minDistance = Mathf.Max(0.01f, rolloffDistanceMin);
            source.volume = Mathf.Clamp01(volume);
            source.pitch = pitch;
            source.Play();

            TimedSelfDestruct selfDestruct = go.AddComponent<TimedSelfDestruct>();
            selfDestruct.ResetLifetime(clip.length + 0.05f);
        }
    }
}

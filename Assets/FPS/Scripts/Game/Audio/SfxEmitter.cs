using Unity.FPS.GameFramework;
using UnityEngine;
using UnityEngine.Audio;

namespace Unity.FPS.Game
{
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(TimedSelfDestruct))]
    public sealed class SfxEmitter : MonoBehaviour, IPoolable
    {
        private AudioSource m_AudioSource;
        private TimedSelfDestruct m_TimedSelfDestruct;
        private PooledInstance m_PooledInstance;

        private void Awake()
        {
            CacheComponents();
        }

        /// <param name="parent">挂载音源的父物体，音源会设为其子节点并随其移动；null 则在世界坐标 position 处播放。</param>
        public void Play(AudioClip clip, AudioMixerGroup outputGroup, Vector3 position, float spatialBlend, float minDistance,
            float volume = 1f, float pitch = 1f, bool loop = false, Transform parent = null)
        {
            if (clip == null)
            {
                DespawnImmediately();
                return;
            }

            CacheComponents();

            transform.position = position;
            if (parent != null)
            {
                transform.SetParent(parent);
                transform.localPosition = Vector3.zero;
            }
            else
            {
                transform.SetParent(null);
            }

            m_AudioSource.Stop();
            m_AudioSource.outputAudioMixerGroup = outputGroup;
            m_AudioSource.clip = clip;
            m_AudioSource.spatialBlend = Mathf.Clamp01(spatialBlend);
            m_AudioSource.minDistance = Mathf.Max(0.01f, minDistance);
            m_AudioSource.volume = Mathf.Clamp01(volume);
            m_AudioSource.pitch = pitch;
            m_AudioSource.loop = loop;
            m_AudioSource.Play();

            float lifeTime = loop ? 9999f : clip.length + 0.05f;
            m_TimedSelfDestruct.ResetLifetime(lifeTime);
        }

        public void OnSpawned()
        {
            CacheComponents();
        }

        public void OnDespawned()
        {
            transform.SetParent(null);
            if (m_AudioSource != null)
            {
                m_AudioSource.Stop();
                m_AudioSource.clip = null;
                m_AudioSource.loop = false;
            }
        }

        private void CacheComponents()
        {
            if (m_AudioSource == null)
            {
                m_AudioSource = GetComponent<AudioSource>();
            }

            if (m_TimedSelfDestruct == null)
            {
                m_TimedSelfDestruct = GetComponent<TimedSelfDestruct>();
            }

            if (m_PooledInstance == null)
            {
                m_PooledInstance = GetComponent<PooledInstance>();
            }
        }

        private void DespawnImmediately()
        {
            if (m_PooledInstance == null)
            {
                m_PooledInstance = GetComponent<PooledInstance>();
            }

            if (m_PooledInstance != null)
            {
                m_PooledInstance.Despawn();
                return;
            }

            Destroy(gameObject);
        }
    }
}

using UnityEngine;
using Unity.FPS.GameFramework;

namespace Unity.FPS.Game
{
    public class TimedSelfDestruct : MonoBehaviour
    {
        public float LifeTime = 1f;

        float m_SpawnTime;

        void Awake()
        {
            m_SpawnTime = Time.time;
        }

        void OnEnable()
        {
            m_SpawnTime = Time.time;
        }

        public void ResetLifetime(float lifeTime)
        {
            LifeTime = lifeTime;
            m_SpawnTime = Time.time;
        }

        void Update()
        {
            if (Time.time > m_SpawnTime + LifeTime)
            {
                PooledInstance pooledInstance = GetComponent<PooledInstance>();
                if (pooledInstance != null)
                {
                    pooledInstance.Despawn();
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
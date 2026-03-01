using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.GameFramework
{
    public class ObjPrefabManager : SceneSingleton<ObjPrefabManager>
    {
        [SerializeField] private int m_DefaultMaxSize = 64;
        [SerializeField] private Transform m_PoolRoot;

        private readonly Dictionary<int, PrefabPool> m_PoolsByPrefabKey = new Dictionary<int, PrefabPool>();
        private readonly List<MonoBehaviour> m_TempMonoBehaviours = new List<MonoBehaviour>(32);

        private class PrefabPool
        {
            public readonly int PrefabKey;
            public readonly GameObject Prefab;
            public readonly Stack<GameObject> Inactive;
            public readonly Transform Root;
            public int MaxSize;
            public int CountAll;

            public PrefabPool(int prefabKey, GameObject prefab, int maxSize, Transform parentRoot)
            {
                PrefabKey = prefabKey;
                Prefab = prefab;
                MaxSize = Mathf.Max(1, maxSize);
                Inactive = new Stack<GameObject>(MaxSize);

                GameObject rootObject = new GameObject(prefab.name + "_Pool");
                Root = rootObject.transform;
                Root.SetParent(parentRoot, false);
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this)
            {
                return;
            }

            if (m_PoolRoot == null)
            {
                GameObject root = new GameObject("ObjPrefabPools");
                m_PoolRoot = root.transform;
                m_PoolRoot.SetParent(transform, false);
            }
        }

        public T Load<T>(T prefab, int prewarm = 0, int maxSize = -1) where T : Component
        {
            if (prefab == null)
            {
                return null;
            }

            PrefabPool pool = GetOrCreatePool(prefab.gameObject, maxSize);
            if (prewarm > 0)
            {
                Prewarm(pool, prewarm);
            }

            return prefab;
        }

        public T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null, int maxSize = -1)
            where T : Component
        {
            if (prefab == null)
            {
                return null;
            }

            PrefabPool pool = GetOrCreatePool(prefab.gameObject, maxSize);
            GameObject instance = GetOrCreateInstance(pool);

            Transform instanceTransform = instance.transform;
            if (parent != null)
            {
                instanceTransform.SetParent(parent, false);
            }
            else
            {
                instanceTransform.SetParent(null, false);
            }

            instanceTransform.SetPositionAndRotation(position, rotation);

            PooledInstance pooledInstance = instance.GetComponent<PooledInstance>();
            pooledInstance.MarkSpawned();

            InvokeOnSpawned(instance);
            instance.SetActive(true);

            return instance.GetComponent<T>();
        }

        public void Despawn(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            PooledInstance pooledInstance = instance.GetComponent<PooledInstance>();
            if (pooledInstance == null || pooledInstance.Owner != this)
            {
                Destroy(instance);
                return;
            }

            if (pooledInstance.IsDespawned)
            {
                return;
            }

            if (!m_PoolsByPrefabKey.TryGetValue(pooledInstance.PrefabKey, out PrefabPool pool))
            {
                Destroy(instance);
                return;
            }

            pooledInstance.MarkDespawned();
            InvokeOnDespawned(instance);

            Transform instanceTransform = instance.transform;
            instanceTransform.SetParent(pool.Root, false);
            instance.SetActive(false);

            if (pool.Inactive.Count >= pool.MaxSize)
            {
                pool.CountAll = Mathf.Max(0, pool.CountAll - 1);
                Destroy(instance);
                return;
            }

            pool.Inactive.Push(instance);
        }

        private PrefabPool GetOrCreatePool(GameObject prefab, int maxSize)
        {
            int prefabKey = prefab.GetInstanceID();
            if (!m_PoolsByPrefabKey.TryGetValue(prefabKey, out PrefabPool pool))
            {
                int finalMaxSize = maxSize > 0 ? maxSize : m_DefaultMaxSize;
                pool = new PrefabPool(prefabKey, prefab, finalMaxSize, m_PoolRoot);
                m_PoolsByPrefabKey.Add(prefabKey, pool);
            }
            else if (maxSize > 0)
            {
                pool.MaxSize = Mathf.Max(1, maxSize);
            }

            return pool;
        }

        private void Prewarm(PrefabPool pool, int prewarmCount)
        {
            int targetCount = Mathf.Min(prewarmCount, pool.MaxSize);
            int currentInactive = pool.Inactive.Count;
            int needToCreate = targetCount - currentInactive;
            for (int i = 0; i < needToCreate; i++)
            {
                GameObject instance = CreateInstance(pool);
                PooledInstance pooledInstance = instance.GetComponent<PooledInstance>();
                pooledInstance.MarkDespawned();
                instance.SetActive(false);
                pool.Inactive.Push(instance);
            }
        }

        private GameObject GetOrCreateInstance(PrefabPool pool)
        {
            if (pool.Inactive.Count > 0)
            {
                return pool.Inactive.Pop();
            }

            return CreateInstance(pool);
        }

        private GameObject CreateInstance(PrefabPool pool)
        {
            GameObject instance = Instantiate(pool.Prefab, pool.Root);
            pool.CountAll++;

            PooledInstance pooledInstance = instance.GetComponent<PooledInstance>();
            if (pooledInstance == null)
            {
                pooledInstance = instance.AddComponent<PooledInstance>();
            }

            pooledInstance.Initialize(this, pool.PrefabKey);
            instance.SetActive(false);
            return instance;
        }

        private void InvokeOnSpawned(GameObject instance)
        {
            m_TempMonoBehaviours.Clear();
            instance.GetComponents(m_TempMonoBehaviours);
            for (int i = 0; i < m_TempMonoBehaviours.Count; i++)
            {
                MonoBehaviour behaviour = m_TempMonoBehaviours[i];
                if (behaviour is IPoolable poolable)
                {
                    poolable.OnSpawned();
                }
            }
        }

        private void InvokeOnDespawned(GameObject instance)
        {
            m_TempMonoBehaviours.Clear();
            instance.GetComponents(m_TempMonoBehaviours);
            for (int i = 0; i < m_TempMonoBehaviours.Count; i++)
            {
                MonoBehaviour behaviour = m_TempMonoBehaviours[i];
                if (behaviour is IPoolable poolable)
                {
                    poolable.OnDespawned();
                }
            }
        }
    }
}

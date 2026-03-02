using UnityEngine;

namespace Unity.FPS.GameFramework
{
    [DisallowMultipleComponent]
    public sealed class DebrisRoot : MonoBehaviour
    {
        [SerializeField] private Transform m_Root;
        [Header("Debris Lifetime")]
        [SerializeField] [Min(0f)] float m_DebrisHoldDuration = 3f;
        [SerializeField] [Min(0.01f)] float m_DebrisFadeDuration = 1.5f;

        public Transform Root => EnsureRoot();

        /// <summary>
        /// 当前关卡/段的激活碎片根。由关卡流程在切段时设置。
        /// </summary>
        public static DebrisRoot Active { get; private set; }

        public static void SetActive(DebrisRoot root)
        {
            Active = root;
            if (Active != null)
            {
                _ = Active.Root;
            }
        }

        public static Transform ResolveParentFor(Transform sourceTransform)
        {
            DebrisRoot inHierarchy = sourceTransform != null ? sourceTransform.GetComponentInParent<DebrisRoot>() : null;
            if (inHierarchy != null)
            {
                return inHierarchy.Root;
            }

            if (Active != null)
            {
                return Active.Root;
            }

            return null;
        }

        public static void RegisterSpawnedDebris(Transform sourceTransform, GameObject debrisInstance)
        {
            if (debrisInstance == null)
            {
                return;
            }

            DebrisRoot inHierarchy = sourceTransform != null ? sourceTransform.GetComponentInParent<DebrisRoot>() : null;
            DebrisRoot targetRoot = inHierarchy != null ? inHierarchy : Active;
            if (targetRoot != null)
            {
                targetRoot.ConfigureDebris(debrisInstance);
                return;
            }

            DebrisAutoRecycle.ConfigureOn(
                debrisInstance,
                DebrisAutoRecycle.DefaultHoldDuration,
                DebrisAutoRecycle.DefaultFadeDuration);
        }

        public void ClearAll()
        {
            Transform root = EnsureRoot();
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                PooledInstance pooledInstance = child.GetComponent<PooledInstance>();
                if (pooledInstance != null)
                {
                    pooledInstance.Despawn();
                }
                else
                {
                    Destroy(child.gameObject);
                }
            }
        }

        void ConfigureDebris(GameObject debrisInstance)
        {
            DebrisAutoRecycle.ConfigureOn(
                debrisInstance,
                Mathf.Max(0f, m_DebrisHoldDuration),
                Mathf.Max(0.01f, m_DebrisFadeDuration));
        }

        Transform EnsureRoot()
        {
            if (m_Root != null)
            {
                return m_Root;
            }

            Transform existing = transform.Find("Debris");
            if (existing != null)
            {
                m_Root = existing;
                return m_Root;
            }

            GameObject rootObject = new GameObject("Debris");
            m_Root = rootObject.transform;
            m_Root.SetParent(transform, false);
            return m_Root;
        }
    }
}

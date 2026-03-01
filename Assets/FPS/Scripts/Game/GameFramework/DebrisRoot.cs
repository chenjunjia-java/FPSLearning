using UnityEngine;

namespace Unity.FPS.GameFramework
{
    [DisallowMultipleComponent]
    public sealed class DebrisRoot : MonoBehaviour
    {
        [SerializeField] private Transform m_Root;

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

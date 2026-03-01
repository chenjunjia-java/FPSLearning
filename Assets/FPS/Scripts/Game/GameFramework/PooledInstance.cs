using UnityEngine;

namespace Unity.FPS.GameFramework
{
    public class PooledInstance : MonoBehaviour
    {
        public int PrefabKey { get; private set; }
        public ObjPrefabManager Owner { get; private set; }
        public bool IsDespawned { get; private set; }

        public void Initialize(ObjPrefabManager owner, int prefabKey)
        {
            Owner = owner;
            PrefabKey = prefabKey;
            IsDespawned = false;
        }

        public void MarkSpawned()
        {
            IsDespawned = false;
        }

        public void MarkDespawned()
        {
            IsDespawned = true;
        }

        public void Despawn()
        {
            if (Owner != null)
            {
                Owner.Despawn(gameObject);
                return;
            }

            Destroy(gameObject);
        }
    }
}

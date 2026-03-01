using UnityEngine;

namespace Unity.FPS.GameFramework
{
    /// <summary>
    /// Scene-scoped singleton. One instance is allowed per loaded scene.
    /// </summary>
    public abstract class SceneSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this as T;
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}

using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class ChargedProjectileEffectsHandler : MonoBehaviour
    {
        [Tooltip("Object that will be affected by charging scale & color changes")]
        public GameObject ChargingObject;

        [Tooltip("Scale of the charged object based on charge")]
        public MinMaxVector3 Scale;

        [Tooltip("Color of the charged object based on charge")]
        public MinMaxColor Color;

        MeshRenderer[] m_AffectedRenderers;
        ProjectileBase m_ProjectileBase;
        bool m_Initialized;

        void Awake()
        {
            m_ProjectileBase = GetComponent<ProjectileBase>();
            DebugUtility.HandleErrorIfNullGetComponent<ProjectileBase, ChargedProjectileEffectsHandler>(
                m_ProjectileBase, this, gameObject);

            m_AffectedRenderers = ChargingObject.GetComponentsInChildren<MeshRenderer>();
            foreach (var ren in m_AffectedRenderers)
            {
                if (ren != null && ren.sharedMaterial != null)
                {
                    ren.sharedMaterial = new Material(ren.sharedMaterial);
                }
            }

            m_Initialized = true;
        }

        void OnEnable()
        {
            if (!m_Initialized)
            {
                Awake();
            }

            if (m_ProjectileBase != null)
            {
                m_ProjectileBase.OnShoot -= OnShoot;
                m_ProjectileBase.OnShoot += OnShoot;
            }
        }

        void OnDisable()
        {
            if (m_ProjectileBase != null)
            {
                m_ProjectileBase.OnShoot -= OnShoot;
            }
        }

        void OnShoot()
        {
            ChargingObject.transform.localScale = Scale.GetValueFromRatio(m_ProjectileBase.InitialCharge);

            foreach (var ren in m_AffectedRenderers)
            {
                ren.sharedMaterial.SetColor("_Color", Color.GetValueFromRatio(m_ProjectileBase.InitialCharge));
            }
        }
    }
}
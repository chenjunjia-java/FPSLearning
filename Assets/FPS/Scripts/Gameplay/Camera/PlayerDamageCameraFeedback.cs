using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(FpsCameraRig))]
    [RequireComponent(typeof(HitStopController))]
    public class PlayerDamageCameraFeedback : MonoBehaviour
    {
        [SerializeField] float m_DamageShakeIntensity = 0.14f;
        [SerializeField] float m_DamageShakeDuration = 0.08f;
        [SerializeField] float m_DamageUpImpulse = 0.04f;
        [SerializeField] float m_DamageImpulseReturnSharpness = 42f;
        [SerializeField] float m_DamageHitStopTimeScale = 0.1f;
        [SerializeField] float m_DamageHitStopDuration = 0.06f;

        Health m_Health;
        FpsCameraRig m_CameraRig;
        HitStopController m_HitStopController;

        void Awake()
        {
            m_Health = GetComponent<Health>();
            m_CameraRig = GetComponent<FpsCameraRig>();
            m_HitStopController = GetComponent<HitStopController>();
        }

        void OnEnable()
        {
            if (m_Health != null)
            {
                m_Health.OnDamaged += OnDamaged;
            }
        }

        void OnDisable()
        {
            if (m_Health != null)
            {
                m_Health.OnDamaged -= OnDamaged;
            }
        }

        void OnDamaged(float damage, GameObject damageSource)
        {
            if (m_CameraRig != null)
            {
                m_CameraRig.AddShake(m_DamageShakeIntensity, m_DamageShakeDuration);
                if (m_CameraRig.CameraEffects != null)
                {
                    m_CameraRig.CameraEffects.AddPositionImpulse(
                        new Vector3(0f, Mathf.Abs(m_DamageUpImpulse), 0f),
                        Mathf.Max(0.01f, m_DamageImpulseReturnSharpness));
                }
            }

            if (m_HitStopController != null)
            {
                m_HitStopController.Request(m_DamageHitStopTimeScale, m_DamageHitStopDuration);
            }
        }
    }
}

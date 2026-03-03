using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public enum ShakeEnvelopeProfile
    {
        Constant = 0,
        BigToSmall = 1,
        SmallToBig = 2,
        Normal = 3,
    }

    public class FpsCameraEffects : MonoBehaviour
    {
        [Header("Breathing")]
        [Tooltip("Base breathing frequency in Hz when idle")]
        [SerializeField] float m_BaseBreathFrequency = 0.3f;

        [Tooltip("Additional breathing frequency in Hz at full move factor")]
        [SerializeField] float m_MoveBreathFrequencyBoost = 0.1f;

        [Tooltip("Vertical breathing amplitude in meters")]
        [SerializeField] float m_BreathPositionAmplitude = 0.02f;

        [Header("Shake")]
        [Tooltip("Noise frequency for shake")]
        [SerializeField] float m_ShakeFrequency = 80f;

        [Tooltip("Shake position multiplier in meters")]
        [SerializeField] float m_PositionShakeMultiplier = 0.05f;

        [Tooltip("Shake rotation multiplier in degrees")]
        [SerializeField] float m_RotationShakeMultiplier = 1.8f;

        [Tooltip("How fast shake intensity decays when duration mode is used")]
        [SerializeField] float m_ShakeDecay = 2.2f;

        [Header("Shake Envelope Profiles")]
        [Tooltip("恒定强度包络（0~1），默认全程 1")]
        [SerializeField] AnimationCurve m_ShakeEnvelopeConstant = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 1f)
        );

        [Tooltip("从大到小包络（0~1），默认 1→0")]
        [SerializeField] AnimationCurve m_ShakeEnvelopeBigToSmall = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0f)
        );

        [Tooltip("从小到大包络（0~1），默认 0→1")]
        [SerializeField] AnimationCurve m_ShakeEnvelopeSmallToBig = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(1f, 1f)
        );

        [Tooltip("正态分布/钟形包络（0~1），默认 0→1→0")]
        [SerializeField] AnimationCurve m_ShakeEnvelopeNormal = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 0f)
        );

        float m_MoveFactor;
        float m_BreathPhase;
        float m_ShakeIntensity;
        float m_ShakeTimer;
        float m_SetShakeIntensity;
        bool m_UseSetShake;

        float m_EnvelopeShakeCurrentIntensity;
        float m_EnvelopeShakePeakIntensity;
        float m_EnvelopeShakeDuration;
        float m_EnvelopeShakeElapsed;
        bool m_EnvelopeShakeActive;
        ShakeEnvelopeProfile m_EnvelopeShakeProfile;
        Object m_EnvelopeShakeOwner;

        Vector3 m_PositionImpulseLocal;
        float m_PositionImpulseReturnSharpness = 18f;

        Vector3 m_LocalBasePosition;
        Vector3 m_LocalBaseEuler;
        float m_NoiseSeedX;
        float m_NoiseSeedY;
        float m_NoiseSeedZ;

        public float CurrentShakeIntensity =>
            Mathf.Max(m_ShakeIntensity, Mathf.Max(m_UseSetShake ? m_SetShakeIntensity : 0f, m_EnvelopeShakeCurrentIntensity));

        void Awake()
        {
            m_LocalBasePosition = transform.localPosition;
            m_LocalBaseEuler = transform.localEulerAngles;

            // Fixed seeds prevent correlated noise axes.
            m_NoiseSeedX = 17.113f;
            m_NoiseSeedY = 83.731f;
            m_NoiseSeedZ = 41.227f;
        }

        public void SetMoveFactor(float moveFactor)
        {
            m_MoveFactor = Mathf.Clamp01(moveFactor);
        }

        // Continuous shake controlled externally.
        public void SetShake(float intensity)
        {
            m_SetShakeIntensity = Mathf.Max(0f, intensity);
            m_UseSetShake = true;
        }

        public void ClearSetShake()
        {
            m_UseSetShake = false;
            m_SetShakeIntensity = 0f;
        }

        // Impulse-like shake with finite duration (defaults to BigToSmall envelope).
        public void AddShake(float intensity, float duration)
        {
            PlayEnvelopeShake(null, peakIntensity: intensity, duration: duration, profile: ShakeEnvelopeProfile.BigToSmall);
        }

        public void ClearShake()
        {
            m_UseSetShake = false;
            m_SetShakeIntensity = 0f;
            m_ShakeIntensity = 0f;
            m_ShakeTimer = 0f;
            m_EnvelopeShakeActive = false;
            m_EnvelopeShakeCurrentIntensity = 0f;
            m_EnvelopeShakePeakIntensity = 0f;
            m_EnvelopeShakeDuration = 0f;
            m_EnvelopeShakeElapsed = 0f;
            m_EnvelopeShakeOwner = null;
        }

        public void PlayEnvelopeShake(Object owner, float peakIntensity, float duration, ShakeEnvelopeProfile profile)
        {
            if (duration <= 0.001f || peakIntensity <= 0f)
            {
                return;
            }

            m_EnvelopeShakeOwner = owner;
            m_EnvelopeShakeProfile = profile;
            m_EnvelopeShakeDuration = duration;
            m_EnvelopeShakeElapsed = 0f;
            m_EnvelopeShakePeakIntensity = peakIntensity;
            m_EnvelopeShakeActive = true;

            float envelope01 = EvaluateEnvelope(profile, 0f);
            m_EnvelopeShakeCurrentIntensity = m_EnvelopeShakePeakIntensity * envelope01;
        }

        public void StopEnvelopeShake(Object owner)
        {
            if (!m_EnvelopeShakeActive)
            {
                return;
            }

            if (owner != null && m_EnvelopeShakeOwner != owner)
            {
                return;
            }

            m_EnvelopeShakeActive = false;
            m_EnvelopeShakeCurrentIntensity = 0f;
            m_EnvelopeShakePeakIntensity = 0f;
            m_EnvelopeShakeDuration = 0f;
            m_EnvelopeShakeElapsed = 0f;
            m_EnvelopeShakeOwner = null;
        }

        // Adds a local position impulse that decays back to origin over time.
        public void AddPositionImpulse(Vector3 localOffset, float returnSharpness)
        {
            m_PositionImpulseLocal += localOffset;
            m_PositionImpulseReturnSharpness = Mathf.Max(0.01f, returnSharpness);
        }

        public void ClearPositionImpulse()
        {
            m_PositionImpulseLocal = Vector3.zero;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            UpdateShake(dt);
            UpdateEnvelopeShake(dt);
            UpdatePositionImpulse(dt);
            ApplyEffects();
        }

        void UpdateShake(float dt)
        {
            if (m_ShakeTimer > 0f)
            {
                m_ShakeTimer -= dt;
                m_ShakeIntensity = Mathf.Max(0f, m_ShakeIntensity - (m_ShakeDecay * dt));
            }
            else
            {
                m_ShakeIntensity = Mathf.Max(0f, m_ShakeIntensity - (m_ShakeDecay * dt));
            }
        }

        void UpdateEnvelopeShake(float dt)
        {
            if (!m_EnvelopeShakeActive)
            {
                m_EnvelopeShakeCurrentIntensity = 0f;
                return;
            }

            m_EnvelopeShakeElapsed += dt;
            float duration = Mathf.Max(0.001f, m_EnvelopeShakeDuration);
            float t01 = Mathf.Clamp01(m_EnvelopeShakeElapsed / duration);

            float envelope01 = EvaluateEnvelope(m_EnvelopeShakeProfile, t01);
            m_EnvelopeShakeCurrentIntensity = m_EnvelopeShakePeakIntensity * envelope01;

            if (m_EnvelopeShakeElapsed >= m_EnvelopeShakeDuration)
            {
                m_EnvelopeShakeActive = false;
                m_EnvelopeShakeCurrentIntensity = 0f;
                m_EnvelopeShakeOwner = null;
            }
        }

        float EvaluateEnvelope(ShakeEnvelopeProfile profile, float t01)
        {
            t01 = Mathf.Clamp01(t01);

            AnimationCurve curve = profile switch
            {
                ShakeEnvelopeProfile.Constant => m_ShakeEnvelopeConstant,
                ShakeEnvelopeProfile.BigToSmall => m_ShakeEnvelopeBigToSmall,
                ShakeEnvelopeProfile.SmallToBig => m_ShakeEnvelopeSmallToBig,
                ShakeEnvelopeProfile.Normal => m_ShakeEnvelopeNormal,
                _ => m_ShakeEnvelopeNormal,
            };

            if (curve == null || curve.length == 0)
            {
                // 兜底：用 sin(pi*t) 得到平滑的 0→1→0
                return Mathf.Sin(Mathf.PI * t01);
            }

            return Mathf.Clamp01(curve.Evaluate(t01));
        }

        void UpdatePositionImpulse(float dt)
        {
            if (m_PositionImpulseLocal.sqrMagnitude <= 0.000001f)
            {
                m_PositionImpulseLocal = Vector3.zero;
                return;
            }

            float t = 1f - Mathf.Exp(-m_PositionImpulseReturnSharpness * dt);
            m_PositionImpulseLocal = Vector3.LerpUnclamped(m_PositionImpulseLocal, Vector3.zero, t);
        }

        void ApplyEffects()
        {
            float t = Time.time;
            float breathFreq = m_BaseBreathFrequency + (m_MoveBreathFrequencyBoost * m_MoveFactor);
            m_BreathPhase += breathFreq * Time.deltaTime * Mathf.PI * 2f;
            if (m_BreathPhase > Mathf.PI * 2f)
            {
                m_BreathPhase -= Mathf.PI * 2f;
            }

            float breathe = Mathf.Sin(m_BreathPhase);

            float liveShakeIntensity = Mathf.Max(
                m_ShakeIntensity,
                Mathf.Max(m_UseSetShake ? m_SetShakeIntensity : 0f, m_EnvelopeShakeCurrentIntensity)
            );
            float shakePosX = (Mathf.PerlinNoise(m_NoiseSeedX, t * m_ShakeFrequency) - 0.5f) * 2f;
            float shakePosY = (Mathf.PerlinNoise(m_NoiseSeedY, t * m_ShakeFrequency) - 0.5f) * 2f;
            float shakeRotZ = (Mathf.PerlinNoise(m_NoiseSeedZ, t * m_ShakeFrequency) - 0.5f) * 2f;

            Vector3 finalLocalPos = m_LocalBasePosition;
            finalLocalPos.y += breathe * m_BreathPositionAmplitude;
            finalLocalPos.x += shakePosX * m_PositionShakeMultiplier * liveShakeIntensity;
            finalLocalPos.y += shakePosY * m_PositionShakeMultiplier * liveShakeIntensity;
            finalLocalPos += m_PositionImpulseLocal;
            transform.localPosition = finalLocalPos;

            Vector3 finalEuler = m_LocalBaseEuler;
            finalEuler.z += shakeRotZ * m_RotationShakeMultiplier * liveShakeIntensity;
            transform.localEulerAngles = finalEuler;
        }
    }
}

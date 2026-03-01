using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class HitStopController : MonoBehaviour
    {
        [SerializeField] float m_DefaultTimeScale = 0.1f;
        [SerializeField] float m_DefaultDuration = 0.06f;
        [SerializeField] float m_PauseThreshold = 0.001f;

        float m_RemainingDurationUnscaled;
        float m_ActiveTimeScale = 1f;
        float m_BaselineTimeScale = 1f;
        float m_BaselineFixedDeltaTime = 0.02f;
        bool m_IsActive;

        void Awake()
        {
            enabled = false;
        }

        public void RequestDefault()
        {
            Request(m_DefaultTimeScale, m_DefaultDuration);
        }

        public void Request(float timeScale, float durationUnscaled)
        {
            float requestedScale = Mathf.Clamp(timeScale, 0f, 1f);
            float requestedDuration = Mathf.Max(0f, durationUnscaled);
            if (requestedDuration <= 0f)
            {
                return;
            }

            // Do not override a paused game state.
            if (!m_IsActive && Time.timeScale <= m_PauseThreshold)
            {
                return;
            }

            if (!m_IsActive)
            {
                m_BaselineTimeScale = Time.timeScale;
                m_BaselineFixedDeltaTime = Time.fixedDeltaTime;
                m_IsActive = true;
            }

            m_ActiveTimeScale = Mathf.Min(m_ActiveTimeScale, requestedScale);
            if (!m_IsActive || m_ActiveTimeScale <= 0f)
            {
                m_ActiveTimeScale = requestedScale;
            }

            m_RemainingDurationUnscaled = Mathf.Max(m_RemainingDurationUnscaled, requestedDuration);
            ApplyTimeScale(m_ActiveTimeScale);
            enabled = true;
        }

        void Update()
        {
            if (!m_IsActive)
            {
                enabled = false;
                return;
            }

            m_RemainingDurationUnscaled -= Time.unscaledDeltaTime;
            if (m_RemainingDurationUnscaled <= 0f)
            {
                RestoreBaseline();
            }
        }

        void OnDisable()
        {
            if (m_IsActive)
            {
                RestoreBaseline();
            }
        }

        void ApplyTimeScale(float scale)
        {
            float clampedScale = Mathf.Clamp(scale, 0f, 1f);
            Time.timeScale = clampedScale;
            Time.fixedDeltaTime = m_BaselineFixedDeltaTime * clampedScale;
        }

        void RestoreBaseline()
        {
            Time.timeScale = m_BaselineTimeScale;
            Time.fixedDeltaTime = m_BaselineFixedDeltaTime;
            m_RemainingDurationUnscaled = 0f;
            m_ActiveTimeScale = 1f;
            m_IsActive = false;
            enabled = false;
        }
    }
}

using System.Collections;
using UnityEngine;

namespace Unity.FPS.Roguelike.Level
{
    /// <summary>
    /// 轻量“果冻感”出生动画：不依赖外部 tween 库，支持对象池复用与 OnDisable 自动收敛。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JellySpawnTweenRunner : MonoBehaviour
    {
        [System.Serializable]
        public struct Settings
        {
            [Min(0.01f)] public float Duration;
            [Range(0.01f, 1f)] public float StartScale;
            [Min(0f)] public float JellyAmplitude;
            [Min(0f)] public float JellyFrequency;
            public Vector3 JellyNonUniform;
        }

        Coroutine m_Routine;
        int m_PlayVersion;

        public void Play(Transform target, Settings settings)
        {
            if (target == null)
            {
                return;
            }

            settings.Duration = Mathf.Max(0.01f, settings.Duration);
            settings.StartScale = Mathf.Clamp(settings.StartScale, 0.01f, 1f);
            settings.JellyAmplitude = Mathf.Max(0f, settings.JellyAmplitude);
            settings.JellyFrequency = Mathf.Max(0f, settings.JellyFrequency);

            m_PlayVersion++;
            if (m_Routine != null)
            {
                StopCoroutine(m_Routine);
            }

            m_Routine = StartCoroutine(PlayRoutine(target, settings, m_PlayVersion));
        }

        void OnDisable()
        {
            if (m_Routine != null)
            {
                StopCoroutine(m_Routine);
                m_Routine = null;
            }
        }

        IEnumerator PlayRoutine(Transform target, Settings settings, int version)
        {
            Vector3 originalScale = target.localScale;
            float startScale = settings.StartScale;

            float t = 0f;
            float invDuration = 1f / settings.Duration;

            // 初始缩放
            target.localScale = originalScale * startScale;

            while (target != null && version == m_PlayVersion && t < settings.Duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t * invDuration);

                // 平滑推进到 1，同时叠加一个随时间衰减的振荡（果冻感）
                float settle = p * p * (3f - 2f * p); // SmoothStep(0,1,p) 无额外调用
                float baseScale = Mathf.LerpUnclamped(startScale, 1f, settle);

                float wobble = 0f;
                if (settings.JellyAmplitude > 0f && settings.JellyFrequency > 0f)
                {
                    float decay = 1f - p;
                    wobble = Mathf.Sin(p * settings.JellyFrequency * Mathf.PI * 2f) * decay * settings.JellyAmplitude;
                }

                float scalar = baseScale * (1f + wobble);
                Vector3 nonUniform = Vector3.one;
                if (settings.JellyNonUniform.sqrMagnitude > 0f)
                {
                    // 非等比果冻：同样随时间衰减
                    float decay = 1f - p;
                    float nu = Mathf.Sin(p * (settings.JellyFrequency + 0.75f) * Mathf.PI * 2f) * decay;
                    nonUniform += settings.JellyNonUniform * nu;
                }

                target.localScale = Vector3.Scale(originalScale, nonUniform) * scalar;
                yield return null;
            }

            if (target != null && version == m_PlayVersion)
            {
                target.localScale = originalScale;
            }

            if (version == m_PlayVersion)
            {
                m_Routine = null;
            }
        }
    }
}


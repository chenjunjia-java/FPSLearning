using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Unity.FPS.Game
{
    /// <summary>
    /// 播放一整段音频并在播放过程中线性提高 pitch，使听感上“越来越快、越来越尖”。
    /// 适用于整段警报 clip。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class PitchRampPlayer : MonoBehaviour
    {
        [Header("Playback")]
        [SerializeField] private AudioClip m_Clip;
        [SerializeField] private AudioMixerGroup m_OutputGroup;
        [SerializeField] [Min(0.01f)] private float m_StartPitch = 1f;
        [SerializeField] [Min(0.01f)] private float m_EndPitch = 2f;
        [Tooltip("Pitch 从 Start 到 End 的过渡时间（秒）。≤0 则用 clip 长度。")]
        [SerializeField] [Min(0f)] private float m_RampDurationSeconds = 0f;
        [SerializeField] private bool m_StopOnComplete = true;

        private AudioSource m_Source;
        private Coroutine m_RampRoutine;

        private void Awake()
        {
            m_Source = GetComponent<AudioSource>();
            m_Source.playOnAwake = false;
            m_Source.loop = false;
        }

        /// <summary>
        /// 使用默认 clip 播放并做 pitch 爬升。
        /// </summary>
        public void Play()
        {
            Play(m_Clip, m_OutputGroup);
        }

        /// <summary>
        /// 指定 clip 和混音组（如 Ducker），播放并做 pitch 爬升。
        /// </summary>
        public void Play(AudioClip clip, AudioUtility.AudioGroups audioGroup)
        {
            AudioMixerGroup group = AudioUtility.GetAudioGroup(audioGroup);
            Play(clip, group);
        }

        /// <summary>
        /// 指定 clip 和输出组，播放并做 pitch 爬升。
        /// </summary>
        public void Play(AudioClip clip, AudioMixerGroup outputGroup = null)
        {
            if (clip == null)
            {
                return;
            }

            if (m_RampRoutine != null)
            {
                StopCoroutine(m_RampRoutine);
                m_RampRoutine = null;
            }

            m_Source.Stop();
            m_Source.clip = clip;
            m_Source.outputAudioMixerGroup = outputGroup != null ? outputGroup : m_OutputGroup;
            m_Source.pitch = m_StartPitch;
            m_Source.Play();

            float duration = m_RampDurationSeconds > 0f ? m_RampDurationSeconds : clip.length;
            m_RampRoutine = StartCoroutine(RampPitchRoutine(duration));
        }

        /// <summary>
        /// 停止播放并重置 pitch。
        /// </summary>
        public void Stop()
        {
            if (m_RampRoutine != null)
            {
                StopCoroutine(m_RampRoutine);
                m_RampRoutine = null;
            }

            if (m_Source != null)
            {
                m_Source.Stop();
                m_Source.pitch = m_StartPitch;
            }
        }

        private IEnumerator RampPitchRoutine(float duration)
        {
            duration = Mathf.Max(0.001f, duration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                m_Source.pitch = Mathf.Lerp(m_StartPitch, m_EndPitch, t);
                yield return null;
            }

            m_Source.pitch = m_EndPitch;
            m_RampRoutine = null;

            if (m_StopOnComplete && m_Source != null && m_Source.isPlaying)
            {
                m_Source.Stop();
                m_Source.pitch = m_StartPitch;
            }
        }
    }
}

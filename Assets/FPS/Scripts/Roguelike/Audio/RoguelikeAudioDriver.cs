using Unity.FPS.Game;
using Unity.FPS.Roguelike.Level;
using UnityEngine;

namespace Unity.FPS.Roguelike.Audio
{
    [DisallowMultipleComponent]
    public sealed class RoguelikeAudioDriver : MonoBehaviour
    {
        [SerializeField] private LevelSegment m_Segment;
        [SerializeField] private MusicSetSO m_NonCombatMusicSet;
        [SerializeField] private MusicSetSO m_CombatMusicSet;
        [SerializeField] private MusicSetSO m_BossMusicSet;
        [SerializeField] [Min(0f)] private float m_DefaultCrossfadeSeconds = 1.2f;
        [SerializeField] private bool m_PlayNonCombatOnEnable = true;

        private void Awake()
        {
            if (m_Segment == null)
            {
                m_Segment = GetComponent<LevelSegment>();
                if (m_Segment == null)
                {
                    m_Segment = GetComponentInParent<LevelSegment>();
                }
            }
        }

        private void OnEnable()
        {
            // 确保在玩家开始游戏时就完成音频系统与 BGM 的预热，
            // 避免首次进入关卡门时才触发大块解压造成的卡顿。
            AudioRoot.Instance.BootstrapServices();
            PreloadMusicSet(m_NonCombatMusicSet);
            PreloadMusicSet(m_CombatMusicSet);
            PreloadMusicSet(m_BossMusicSet);

            if (!m_PlayNonCombatOnEnable)
            {
                return;
            }

            TryPlaySet(m_NonCombatMusicSet);
        }

        /// <summary>
        /// 玩家进入关卡门时调用，统一切换为战斗 BGM。
        /// Boss 段也在进门时先播战斗曲，等 Boss 实际生成时再由 HandleBossSpawned 切到 Boss 曲。
        /// </summary>
        public void HandleSegmentEnterGateTriggered()
        {
            TryPlaySet(m_CombatMusicSet);
        }

        /// <summary>
        /// Boss 实际生成时调用，切换为 Boss BGM。
        /// </summary>
        public void HandleBossSpawned()
        {
            TryPlaySet(m_BossMusicSet);
        }

        public void HandleSegmentCleared()
        {
            TryPlaySet(m_NonCombatMusicSet);
        }

        public void PlayNonCombatNow()
        {
            TryPlaySet(m_NonCombatMusicSet);
        }

        private void TryPlaySet(MusicSetSO musicSet)
        {
            if (musicSet == null)
            {
                return;
            }

            MusicPlayer.PlaySet(musicSet, m_DefaultCrossfadeSeconds);
        }

        private static void PreloadMusicSet(MusicSetSO musicSet)
        {
            if (musicSet == null)
            {
                return;
            }

            PreloadClip(musicSet.Wind);
            PreloadClip(musicSet.Noise);
            PreloadClip(musicSet.Environment);
            PreloadClip(musicSet.Main);
        }

        private static void PreloadClip(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            if (clip.loadState == AudioDataLoadState.Unloaded)
            {
                clip.LoadAudioData();
            }
        }
    }
}

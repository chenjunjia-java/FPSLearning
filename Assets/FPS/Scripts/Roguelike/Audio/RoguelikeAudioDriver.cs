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
            if (!m_PlayNonCombatOnEnable)
            {
                return;
            }

            AudioRoot.Instance.BootstrapServices();
            TryPlaySet(m_NonCombatMusicSet);
        }

        public void HandleSegmentEnterGateTriggered()
        {
            if (m_Segment != null && m_Segment.IsBossSegment)
            {
                TryPlaySet(m_BossMusicSet);
                return;
            }

            TryPlaySet(m_CombatMusicSet);
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
    }
}

using UnityEngine;

namespace Unity.FPS.Roguelike.Level
{
    public class DebugOpenDoorTrigger : MonoBehaviour
    {
        [SerializeField] private RoguelikeLevelGenerator m_LevelGenerator;
        [SerializeField] private KeyCode m_OpenDoorKey = KeyCode.O;

        private void Awake()
        {
            if (m_LevelGenerator == null)
            {
                m_LevelGenerator = FindObjectOfType<RoguelikeLevelGenerator>();
            }
        }

        private void Update()
        {
            if (!Input.GetKeyDown(m_OpenDoorKey) || m_LevelGenerator == null)
            {
                return;
            }

            m_LevelGenerator.AdvanceToNextSegment();
        }
    }
}

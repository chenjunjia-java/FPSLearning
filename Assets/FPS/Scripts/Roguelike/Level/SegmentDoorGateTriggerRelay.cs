using UnityEngine;

namespace Unity.FPS.Roguelike.Level
{
    [DisallowMultipleComponent]
    public sealed class SegmentDoorGateTriggerRelay : MonoBehaviour
    {
        [SerializeField] private SegmentDoorGate m_Gate;

        public void Bind(SegmentDoorGate gate)
        {
            m_Gate = gate;
        }

        private void Reset()
        {
            var c = GetComponent<Collider>();
            if (c != null)
            {
                c.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (m_Gate == null)
            {
                return;
            }

            m_Gate.HandleTriggerEnter(other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (m_Gate == null)
            {
                return;
            }

            m_Gate.HandleTriggerExit(other);
        }
    }
}


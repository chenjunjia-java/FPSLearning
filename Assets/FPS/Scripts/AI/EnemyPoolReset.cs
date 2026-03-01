using Unity.FPS.Game;
using Unity.FPS.GameFramework;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.FPS.AI
{
    [RequireComponent(typeof(Health))]
    public class EnemyPoolReset : MonoBehaviour, IPoolable
    {
        private Health m_Health;
        private NavMeshAgent m_NavMeshAgent;
        private EnemyController m_EnemyController;
        private DetectionModule m_DetectionModule;
        private Rigidbody[] m_Rigidbodies;
        private bool m_PendingNavAgentReset;

        private const float k_NavMeshSampleRadius = 2.0f;

        private void Awake()
        {
            m_Health = GetComponent<Health>();
            m_NavMeshAgent = GetComponent<NavMeshAgent>();
            m_EnemyController = GetComponent<EnemyController>();
            m_DetectionModule = GetComponentInChildren<DetectionModule>();
            m_Rigidbodies = GetComponentsInChildren<Rigidbody>(true);
        }

        private void OnEnable()
        {
            if (m_PendingNavAgentReset)
            {
                TryResetNavAgent();
            }
        }

        public void OnSpawned()
        {
            if (m_Health != null)
            {
                m_Health.ResetHealth();
            }

            if (m_DetectionModule != null)
            {
                m_DetectionModule.ResetDetectionState();
            }

            if (m_EnemyController != null)
            {
                m_EnemyController.ResetPathDestination();
            }

            if (m_NavMeshAgent != null)
            {
                // OnSpawned is invoked while the instance can still be inactive (pool implementation).
                // NavMeshAgent APIs like ResetPath require the agent to be active and placed on a NavMesh.
                m_PendingNavAgentReset = true;
                TryResetNavAgent();
            }

            if (m_Rigidbodies != null)
            {
                for (int i = 0; i < m_Rigidbodies.Length; i++)
                {
                    m_Rigidbodies[i].velocity = Vector3.zero;
                    m_Rigidbodies[i].angularVelocity = Vector3.zero;
                }
            }
        }

        public void OnDespawned()
        {
            if (m_NavMeshAgent != null)
            {
                if (m_NavMeshAgent.enabled && m_NavMeshAgent.isOnNavMesh)
                {
                    m_NavMeshAgent.ResetPath();
                    m_NavMeshAgent.velocity = Vector3.zero;
                    m_NavMeshAgent.isStopped = true;
                }
            }
        }

        private void TryResetNavAgent()
        {
            if (m_NavMeshAgent == null || !isActiveAndEnabled || !m_NavMeshAgent.enabled)
            {
                return;
            }

            // Ensure agent is on NavMesh even if spawn position has a height offset.
            Vector3 current = transform.position;
            if (NavMesh.SamplePosition(current, out NavMeshHit hit, k_NavMeshSampleRadius, NavMesh.AllAreas))
            {
                m_NavMeshAgent.Warp(hit.position);
            }

            if (!m_NavMeshAgent.isOnNavMesh)
            {
                return;
            }

            m_NavMeshAgent.ResetPath();
            m_NavMeshAgent.velocity = Vector3.zero;
            m_NavMeshAgent.isStopped = false;
            m_PendingNavAgentReset = false;
        }
    }
}

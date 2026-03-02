using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.FPS.AI
{
    [RequireComponent(typeof(EnemyController))]
    public class EnemyMobile : MonoBehaviour
    {
        public enum AIState
        {
            Patrol,
            Follow,
            Attack,
        }

        public Animator Animator;

        [Tooltip("Fraction of the enemy's attack range at which it will stop moving towards target while attacking")]
        [Range(0f, 1f)]
        public float AttackStopDistanceRatio = 0.5f;

        [Tooltip("The random hit damage effects")]
        public ParticleSystem[] RandomHitSparks;

        public ParticleSystem[] OnDetectVfx;
        [Tooltip("SFX key in SfxCatalog.")]
        [SerializeField] private SfxKey m_OnDetectSfxKey = SfxKey.EnemyDetect;

        [Header("Sound")]
        [Tooltip("SFX key in SfxCatalog for movement loop")]
        [SerializeField] private SfxKey m_MovementSfxKey = SfxKey.None;
        [Tooltip("Movement loop starts playing only when navmesh velocity exceeds this threshold.")]
        [SerializeField] [Min(0f)] private float m_MovementSfxMinSpeedToPlay = 0.2f;
        public MinMaxFloat PitchDistortionMovementSpeed;
        
        [Header("Idle Wander")]
        public bool CanMoveWhenIdle = true;
        public float IdleWanderRadius = 6f;
        public MinMaxFloat IdleWanderInterval;
        public float IdleStopDistance = 0.5f;
        public float IdleNavMeshSampleRadius = 2f;

        public AIState AiState { get; private set; }
        EnemyController m_EnemyController;
        AudioSource m_AudioSource;
        Vector3 m_IdleAnchorPosition;
        Vector3 m_IdleDestination;
        float m_NextIdleWanderTime;
        bool m_HasIdleDestination;
        bool m_IsAlerted;
        float m_StartupTime;

        const string k_AnimMoveSpeedParameter = "MoveSpeed";
        const string k_AnimAttackParameter = "Attack";
        const string k_AnimAlertedParameter = "Alerted";
        const string k_AnimOnDamagedParameter = "OnDamaged";

        Vector3 GetPositionAtDistanceFromTarget(Vector3 targetPosition, float desiredDistance)
        {
            Vector3 fromTarget = transform.position - targetPosition;
            float sqrMagnitude = fromTarget.sqrMagnitude;
            if (sqrMagnitude < 0.0001f)
            {
                fromTarget = -transform.forward;
            }
            else
            {
                fromTarget *= 1f / Mathf.Sqrt(sqrMagnitude);
            }

            return targetPosition + fromTarget * desiredDistance;
        }

        void Start()
        {
            m_EnemyController = GetComponent<EnemyController>();
            DebugUtility.HandleErrorIfNullGetComponent<EnemyController, EnemyMobile>(m_EnemyController, this,
                gameObject);

            m_EnemyController.onAttack += OnAttack;
            m_EnemyController.onDetectedTarget += OnDetectedTarget;
            m_EnemyController.onLostTarget += OnLostTarget;
            m_EnemyController.SetPathDestinationToClosestNode();
            m_EnemyController.onDamaged += OnDamaged;

            // Start patrolling
            AiState = AIState.Patrol;
            m_IdleAnchorPosition = transform.position;
            m_NextIdleWanderTime = Time.time;
            m_StartupTime = Time.time;

            m_AudioSource = GetComponent<AudioSource>();
            DebugUtility.HandleErrorIfNullGetComponent<AudioSource, EnemyMobile>(m_AudioSource, this, gameObject);
            if (m_MovementSfxKey != SfxKey.None && SfxService.TryGetCatalogEntry(m_MovementSfxKey, out SfxCatalogSO.Entry entry) && entry.Clip != null)
            {
                m_AudioSource.clip = entry.Clip;
                m_AudioSource.playOnAwake = false;
                m_AudioSource.loop = true;
                if (m_AudioSource.isPlaying)
                {
                    m_AudioSource.Stop();
                }
            }
        }

        private void OnDisable()
        {
            if (m_AudioSource != null && m_AudioSource.isPlaying)
            {
                m_AudioSource.Stop();
            }
        }

        void Update()
        {
            UpdateAiStateTransitions();
            UpdateCurrentAiState();

            float moveSpeed = m_EnemyController.NavMeshAgent.velocity.magnitude;

            // Update animator speed parameter
            Animator.SetFloat(k_AnimMoveSpeedParameter, moveSpeed);

            // changing the pitch of the movement sound depending on the movement speed
            if (m_AudioSource != null)
            {
                m_AudioSource.pitch = Mathf.Lerp(PitchDistortionMovementSpeed.Min, PitchDistortionMovementSpeed.Max,
                    moveSpeed / m_EnemyController.NavMeshAgent.speed);

                bool shouldPlayMovementLoop = m_IsAlerted && m_AudioSource.clip != null && moveSpeed > m_MovementSfxMinSpeedToPlay;
                if (shouldPlayMovementLoop)
                {
                    if (!m_AudioSource.isPlaying)
                    {
                        m_AudioSource.Play();
                    }
                }
                else
                {
                    if (m_AudioSource.isPlaying)
                    {
                        m_AudioSource.Stop();
                    }
                }
            }
        }

        void UpdateAiStateTransitions()
        {
            // 目标丢失时从 Follow/Attack 回到 Patrol，避免后续访问 KnownDetectedTarget 为 null
            if ((AiState == AIState.Follow || AiState == AIState.Attack) && m_EnemyController.KnownDetectedTarget == null)
            {
                AiState = AIState.Patrol;
            }

            // Handle transitions 
            switch (AiState)
            {
                case AIState.Follow:
                    // Obstacle LOS can be disabled in EnemyController/DetectionModule,
                    // so entering attack relies on target range here.
                    if (m_EnemyController.IsTargetInAttackRange)
                    {
                        AiState = AIState.Attack;
                        m_EnemyController.SetNavDestination(transform.position);
                    }

                    break;
                case AIState.Attack:
                    // Transition to follow when no longer a target in attack range
                    if (!m_EnemyController.IsTargetInAttackRange)
                    {
                        AiState = AIState.Follow;
                    }

                    break;
            }
        }

        void UpdateCurrentAiState()
        {
            // Handle logic 
            switch (AiState)
            {
                case AIState.Patrol:
                    if (m_EnemyController.PatrolPath != null && m_EnemyController.PatrolPath.PathNodes.Count > 0)
                    {
                        m_EnemyController.UpdatePathDestination();
                        m_EnemyController.SetNavDestination(m_EnemyController.GetDestinationOnPath());
                    }
                    else
                    {
                        UpdateIdleWander();
                    }
                    break;
                case AIState.Follow:
                    if (m_EnemyController.KnownDetectedTarget == null) break;
                    Vector3 followTargetPos = m_EnemyController.KnownDetectedTarget.transform.position;
                    var detectionModule = m_EnemyController.DetectionModule;
                    float followMinDistance = detectionModule != null
                        ? Mathf.Max(0f, detectionModule.MinDistanceFromTarget)
                        : 0f;
                    if (followMinDistance > 0f)
                    {
                        Vector3 desiredPos = GetPositionAtDistanceFromTarget(followTargetPos, followMinDistance);
                        m_EnemyController.SetNavDestination(m_EnemyController.GetDestinationTowards(desiredPos));
                    }
                    else
                    {
                        m_EnemyController.SetNavDestination(m_EnemyController.GetDestinationTowards(followTargetPos));
                    }
                    m_EnemyController.OrientTowards(followTargetPos);
                    m_EnemyController.OrientWeaponsTowards(followTargetPos);
                    break;
                case AIState.Attack:
                    if (m_EnemyController.KnownDetectedTarget == null) break;
                    Vector3 attackTargetPos = m_EnemyController.KnownDetectedTarget.transform.position;
                    var detectionModuleAttack = m_EnemyController.DetectionModule;
                    if (detectionModuleAttack == null || detectionModuleAttack.DetectionSourcePoint == null)
                    {
                        m_EnemyController.SetNavDestination(transform.position);
                        m_EnemyController.OrientTowards(attackTargetPos);
                        m_EnemyController.TryAtack(attackTargetPos);
                        break;
                    }

                    float attackMinDistance = Mathf.Max(0f, detectionModuleAttack.MinDistanceFromTarget);
                    float attackStopDistance = (AttackStopDistanceRatio * detectionModuleAttack.AttackRange);
                    float desiredAttackDistance = Mathf.Max(attackStopDistance, attackMinDistance);
                    float distanceToTarget = Vector3.Distance(attackTargetPos, detectionModuleAttack.DetectionSourcePoint.position);
                    const float distanceTolerance = 0.05f;

                    if (attackMinDistance > 0f)
                    {
                        if (distanceToTarget > desiredAttackDistance + distanceTolerance ||
                            distanceToTarget < desiredAttackDistance - distanceTolerance)
                        {
                            Vector3 desiredPos = GetPositionAtDistanceFromTarget(attackTargetPos, desiredAttackDistance);
                            m_EnemyController.SetNavDestination(m_EnemyController.GetDestinationTowards(desiredPos));
                        }
                        else
                        {
                            m_EnemyController.SetNavDestination(transform.position);
                        }
                    }
                    else
                    {
                        if (distanceToTarget >= desiredAttackDistance)
                        {
                            m_EnemyController.SetNavDestination(m_EnemyController.GetDestinationTowards(attackTargetPos));
                        }
                        else
                        {
                            m_EnemyController.SetNavDestination(transform.position);
                        }
                    }

                    m_EnemyController.OrientTowards(attackTargetPos);
                    m_EnemyController.TryAtack(attackTargetPos);
                    break;
            }
        }

        void OnAttack()
        {
            Animator.SetTrigger(k_AnimAttackParameter);
        }

        void UpdateIdleWander()
        {
            if (!CanMoveWhenIdle)
            {
                m_EnemyController.SetNavDestination(transform.position);
                return;
            }

            bool shouldPickNewDestination = !m_HasIdleDestination || Time.time >= m_NextIdleWanderTime;
            if (!shouldPickNewDestination)
            {
                float sqrDistance = (transform.position - m_IdleDestination).sqrMagnitude;
                shouldPickNewDestination = sqrDistance <= IdleStopDistance * IdleStopDistance;
            }

            if (shouldPickNewDestination)
            {
                PickIdleDestination();
            }

            if (m_HasIdleDestination)
            {
                m_EnemyController.SetNavDestination(m_IdleDestination);
            }
        }

        void PickIdleDestination()
        {
            const int maxPickAttempts = 4;
            float radius = Mathf.Max(0f, IdleWanderRadius);
            float sampleRadius = Mathf.Max(0.1f, IdleNavMeshSampleRadius);

            for (int attempt = 0; attempt < maxPickAttempts; attempt++)
            {
                Vector2 offset2D = Random.insideUnitCircle * radius;
                Vector3 candidate = m_IdleAnchorPosition;
                candidate.x += offset2D.x;
                candidate.z += offset2D.y;

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
                {
                    m_IdleDestination = hit.position;
                    m_HasIdleDestination = true;
                    ScheduleNextIdleMove();
                    return;
                }
            }

            m_IdleDestination = transform.position;
            m_HasIdleDestination = true;
            ScheduleNextIdleMove();
        }

        void ScheduleNextIdleMove()
        {
            float minInterval = Mathf.Max(0.1f, IdleWanderInterval.Min);
            float maxInterval = Mathf.Max(minInterval, IdleWanderInterval.Max);
            m_NextIdleWanderTime = Time.time + Random.Range(minInterval, maxInterval);
        }

        void OnDetectedTarget()
        {
            if (AiState == AIState.Patrol)
            {
                AiState = AIState.Follow;
            }

            for (int i = 0; i < OnDetectVfx.Length; i++)
            {
                OnDetectVfx[i].Play();
            }

            m_IsAlerted = true;

            // Avoid "instant" SFX when the scene just loaded and the player spawns inside detection range.
            if (m_OnDetectSfxKey != SfxKey.None && Time.time - m_StartupTime > 0.75f)
            {
                Unity.FPS.Game.AudioUtility.PlaySfx(m_OnDetectSfxKey, transform.position);
            }

            Animator.SetBool(k_AnimAlertedParameter, true);
        }

        void OnLostTarget()
        {
            if (AiState == AIState.Follow || AiState == AIState.Attack)
            {
                AiState = AIState.Patrol;
            }

            for (int i = 0; i < OnDetectVfx.Length; i++)
            {
                OnDetectVfx[i].Stop();
            }

            m_IsAlerted = false;
            Animator.SetBool(k_AnimAlertedParameter, false);
        }

        void OnDamaged()
        {
            if (RandomHitSparks != null && RandomHitSparks.Length > 0)
            {
                int n = Random.Range(0, RandomHitSparks.Length);
                if (RandomHitSparks[n] != null)
                {
                    RandomHitSparks[n].Play();
                }
            }

            if (Animator != null)
            {
                Animator.SetTrigger(k_AnimOnDamagedParameter);
            }
        }
    }
}
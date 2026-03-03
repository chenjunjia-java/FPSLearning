using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.GameFramework;
using Unity.FPS.Gameplay;
using Unity.FPS.Roguelike.Stats;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Unity.FPS.AI
{
    [RequireComponent(typeof(Health), typeof(Actor), typeof(NavMeshAgent))]
    public class EnemyController : MonoBehaviour
    {
        [System.Serializable]
        public struct RendererIndexData
        {
            public Renderer Renderer;
            public int MaterialIndex;

            public RendererIndexData(Renderer renderer, int index)
            {
                Renderer = renderer;
                MaterialIndex = index;
            }
        }

        [Header("Parameters")]
        [Tooltip("The Y height at which the enemy will be automatically killed (if it falls off of the level)")]
        public float SelfDestructYHeight = -20f;

        [Tooltip("The distance at which the enemy considers that it has reached its current path destination point")]
        public float PathReachingRadius = 2f;

        [Tooltip("The speed at which the enemy rotates")]
        public float OrientationSpeed = 10f;

        [Tooltip("Delay after death where the GameObject is destroyed (to allow for animation)")]
        public float DeathDuration = 0f;


        [Header("Weapons Parameters")] [Tooltip("Allow weapon swapping for this enemy")]
        public bool SwapToNextWeapon = false;

        [Tooltip("Time delay between a weapon swap and the next attack")]
        public float DelayAfterWeaponSwap = 0f;

        [Header("Eye color")] [Tooltip("Material for the eye color")]
        public Material EyeColorMaterial;

        [Tooltip("The default color of the bot's eye")] [ColorUsageAttribute(true, true)]
        public Color DefaultEyeColor;

        [Tooltip("The attack color of the bot's eye")] [ColorUsageAttribute(true, true)]
        public Color AttackEyeColor;

        [Header("Flash on hit")] [Tooltip("The material used for the body of the hoverbot")]
        public Material BodyMaterial;

        [Tooltip("The gradient representing the color of the flash on hit")] [GradientUsageAttribute(true)]
        public Gradient OnHitBodyGradient;

        [Tooltip("The duration of the flash on hit")]
        public float FlashOnHitDuration = 0.5f;

        [Header("Hit Knockback")]
        [Tooltip("If enabled, this enemy will be pushed back when taking damage.")]
        [SerializeField] private bool m_CanBeKnockedBack = true;

        [Tooltip("How long the knockback lasts.")]
        [SerializeField] [Min(0f)] private float m_KnockbackDuration = 0.1f;

        [Tooltip("Initial knockback momentum (units/second).")]
        [SerializeField] [Min(0f)] private float m_KnockbackMomentum = 6f;

        [Header("Sounds")]
        [Tooltip("SFX key in SfxCatalog.")]
        [SerializeField] private SfxKey m_DamageTickSfxKey = SfxKey.DamageTick;

        [Header("VFX")] [Tooltip("The VFX prefab spawned when the enemy dies")]
        public GameObject DeathVfx;

        [Tooltip("The point at which the death VFX is spawned")]
        public Transform DeathVfxSpawnPoint;

        [Header("Death Gibs")]
        [SerializeField] private GameObject m_DeathGibsPrefab;
        [SerializeField] private Transform m_GibSpawnPoint;
        [SerializeField] [Min(0f)] private float m_GibImpulse = 2.5f;
        [SerializeField] [Min(0f)] private float m_GibRandomImpulse = 0.8f;
        [SerializeField] [Range(0f, 1f)] private float m_GibUpBias = 0.2f;
        [SerializeField] [Min(0.01f)] private float m_GibPieceMass = 0.12f;
        [SerializeField] [Min(0f)] private float m_GibLinearDrag = 0.1f;
        [SerializeField] [Min(0f)] private float m_GibAngularDrag = 0.05f;

        [Header("Death Camera Shake")]
        [Tooltip("普通敌人死亡时相机震动强度，0 表示不震动（爆炸怪由 ExploderDeathSequence 控制）")]
        [SerializeField] [Min(0f)] private float m_DeathCameraShakeIntensity = 0f;

        [Tooltip("死亡相机震动持续时间")]
        [SerializeField] [Min(0.01f)] private float m_DeathCameraShakeDuration = 0.2f;

        [Tooltip("超过此距离不触发震动，0 表示不限制")]
        [SerializeField] [Min(0f)] private float m_DeathCameraShakeMaxDistance = 0f;

        [Header("Loot")] [Tooltip("The object this enemy can drop when dying")]
        public GameObject LootPrefab;

        [Tooltip("The chance the object has to drop")] [Range(0, 1)]
        public float DropRate = 1f;

        [Header("Debug Display")] [Tooltip("Color of the sphere gizmo representing the path reaching range")]
        public Color PathReachingRangeColor = Color.yellow;

        [Tooltip("Color of the sphere gizmo representing the attack range")]
        public Color AttackRangeColor = Color.red;

        [Tooltip("Color of the sphere gizmo representing the detection range")]
        public Color DetectionRangeColor = Color.blue;

        public UnityAction onAttack;
        public UnityAction onDetectedTarget;
        public UnityAction onLostTarget;
        public UnityAction onDamaged;

        List<RendererIndexData> m_BodyRenderers = new List<RendererIndexData>();
        MaterialPropertyBlock m_BodyFlashMaterialPropertyBlock;
        float m_LastTimeDamaged = float.NegativeInfinity;

        RendererIndexData m_EyeRendererData;
        MaterialPropertyBlock m_EyeColorMaterialPropertyBlock;

        public PatrolPath PatrolPath { get; set; }
        public GameObject KnownDetectedTarget => DetectionModule.KnownDetectedTarget;
        public bool IsTargetInAttackRange => DetectionModule.IsTargetInAttackRange;
        public bool IsSeeingTarget => DetectionModule.IsSeeingTarget;
        public bool HadKnownTarget => DetectionModule.HadKnownTarget;
        public NavMeshAgent NavMeshAgent { get; private set; }
        public DetectionModule DetectionModule { get; private set; }

        int m_PathDestinationNodeIndex;
        EnemyManager m_EnemyManager;
        ActorsManager m_ActorsManager;
        Health m_Health;
        Actor m_Actor;
        Collider[] m_SelfColliders;
        GameFlowManager m_GameFlowManager;
        bool m_WasDamagedThisFrame;
        float m_LastTimeWeaponSwapped = Mathf.NegativeInfinity;
        int m_CurrentWeaponIndex;
        WeaponController m_CurrentWeapon;
        WeaponController[] m_Weapons;
        NavigationModule m_NavigationModule;
        PooledInstance m_PooledInstance;
        RoguelikeEnemyStats m_RoguelikeEnemyStats;
        float m_BaseNavSpeed;
        float m_BaseNavAngularSpeed;
        float m_BaseNavAcceleration;
        bool m_IsRegistered;
        bool m_HasDied;
        bool m_IsInKnockback;
        float m_KnockbackRemainingTime;
        Vector3 m_KnockbackVelocity;

        void Awake()
        {
            m_EnemyManager = FindObjectOfType<EnemyManager>();
            DebugUtility.HandleErrorIfNullFindObject<EnemyManager, EnemyController>(m_EnemyManager, this);

            m_ActorsManager = FindObjectOfType<ActorsManager>();
            DebugUtility.HandleErrorIfNullFindObject<ActorsManager, EnemyController>(m_ActorsManager, this);

            m_Health = GetComponent<Health>();
            DebugUtility.HandleErrorIfNullGetComponent<Health, EnemyController>(m_Health, this, gameObject);
            m_PooledInstance = GetComponent<PooledInstance>();
            m_RoguelikeEnemyStats = GetComponent<RoguelikeEnemyStats>();
            if (m_RoguelikeEnemyStats == null)
            {
                m_RoguelikeEnemyStats = gameObject.AddComponent<RoguelikeEnemyStats>();
            }

            m_Actor = GetComponent<Actor>();
            DebugUtility.HandleErrorIfNullGetComponent<Actor, EnemyController>(m_Actor, this, gameObject);

            NavMeshAgent = GetComponent<NavMeshAgent>();
            m_SelfColliders = GetComponentsInChildren<Collider>();

            m_BaseNavSpeed = NavMeshAgent != null ? NavMeshAgent.speed : 0f;
            m_BaseNavAngularSpeed = NavMeshAgent != null ? NavMeshAgent.angularSpeed : 0f;
            m_BaseNavAcceleration = NavMeshAgent != null ? NavMeshAgent.acceleration : 0f;

            m_GameFlowManager = FindObjectOfType<GameFlowManager>();
            DebugUtility.HandleErrorIfNullFindObject<GameFlowManager, EnemyController>(m_GameFlowManager, this);
        }

        void OnEnable()
        {
            m_HasDied = false;
            StopKnockback(false);
            RestoreAliveBodyState();
            RestoreWorldspaceHealthBarImmediate();
            if (m_EnemyManager != null && !m_IsRegistered)
            {
                m_EnemyManager.RegisterEnemy(this);
                m_IsRegistered = true;
            }

            ApplyNavigationBase();
            ApplyMoveSpeedMultiplierFromStats();

            LastHitInfo lastHitInfo = m_Health != null ? m_Health.GetComponent<LastHitInfo>() : null;
            if (lastHitInfo != null)
            {
                lastHitInfo.ResetInfo();
            }
        }

        void Start()
        {
            if (m_Health == null)
            {
                return;
            }

            // Subscribe to damage & death actions
            m_Health.OnDie += OnDie;
            m_Health.OnDamaged += OnDamaged;

            // Find and initialize all weapons
            FindAndInitializeAllWeapons();
            var weapon = GetCurrentWeapon();
            weapon.ShowWeapon(true);

            var detectionModules = GetComponentsInChildren<DetectionModule>();
            DebugUtility.HandleErrorIfNoComponentFound<DetectionModule, EnemyController>(detectionModules.Length, this,
                gameObject);
            DebugUtility.HandleWarningIfDuplicateObjects<DetectionModule, EnemyController>(detectionModules.Length,
                this, gameObject);
            // Initialize detection module
            DetectionModule = detectionModules[0];
            DetectionModule.onDetectedTarget += OnDetectedTarget;
            DetectionModule.onLostTarget += OnLostTarget;
            onAttack += DetectionModule.OnAttack;

            var navigationModules = GetComponentsInChildren<NavigationModule>();
            DebugUtility.HandleWarningIfDuplicateObjects<DetectionModule, EnemyController>(detectionModules.Length,
                this, gameObject);
            // Override navmesh agent data
            if (navigationModules.Length > 0)
            {
                m_NavigationModule = navigationModules[0];
                m_BaseNavSpeed = m_NavigationModule.MoveSpeed;
                m_BaseNavAngularSpeed = m_NavigationModule.AngularSpeed;
                m_BaseNavAcceleration = m_NavigationModule.Acceleration;
            }

            ApplyNavigationBase();
            ApplyMoveSpeedMultiplierFromStats();

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    if (renderer.sharedMaterials[i] == EyeColorMaterial)
                    {
                        m_EyeRendererData = new RendererIndexData(renderer, i);
                    }

                    if (renderer.sharedMaterials[i] == BodyMaterial)
                    {
                        m_BodyRenderers.Add(new RendererIndexData(renderer, i));
                    }
                }
            }

            m_BodyFlashMaterialPropertyBlock = new MaterialPropertyBlock();

            // Check if we have an eye renderer for this enemy
            if (m_EyeRendererData.Renderer != null)
            {
                m_EyeColorMaterialPropertyBlock = new MaterialPropertyBlock();
                m_EyeColorMaterialPropertyBlock.SetColor("_EmissionColor", DefaultEyeColor);
                m_EyeRendererData.Renderer.SetPropertyBlock(m_EyeColorMaterialPropertyBlock,
                    m_EyeRendererData.MaterialIndex);
            }
        }

        void Update()
        {
            EnsureIsWithinLevelBounds();
            TickKnockback(Time.deltaTime);

            DetectionModule.HandleTargetDetection(m_Actor, m_SelfColliders);

            Color currentColor = OnHitBodyGradient.Evaluate((Time.time - m_LastTimeDamaged) / FlashOnHitDuration);
            m_BodyFlashMaterialPropertyBlock.SetColor("_EmissionColor", currentColor);
            foreach (var data in m_BodyRenderers)
            {
                data.Renderer.SetPropertyBlock(m_BodyFlashMaterialPropertyBlock, data.MaterialIndex);
            }

            m_WasDamagedThisFrame = false;
        }

        void EnsureIsWithinLevelBounds()
        {
            // at every frame, this tests for conditions to kill the enemy
            if (transform.position.y < SelfDestructYHeight)
            {
                UnregisterSilentlyIfNeeded();
                if (m_PooledInstance != null)
                {
                    m_PooledInstance.Despawn();
                }
                else
                {
                    Destroy(gameObject);
                }
                return;
            }
        }

        void OnLostTarget()
        {
            onLostTarget.Invoke();

            // Set the eye attack color and property block if the eye renderer is set
            if (m_EyeRendererData.Renderer != null)
            {
                m_EyeColorMaterialPropertyBlock.SetColor("_EmissionColor", DefaultEyeColor);
                m_EyeRendererData.Renderer.SetPropertyBlock(m_EyeColorMaterialPropertyBlock,
                    m_EyeRendererData.MaterialIndex);
            }
        }

        void OnDetectedTarget()
        {
            onDetectedTarget.Invoke();

            // Set the eye default color and property block if the eye renderer is set
            if (m_EyeRendererData.Renderer != null)
            {
                m_EyeColorMaterialPropertyBlock.SetColor("_EmissionColor", AttackEyeColor);
                m_EyeRendererData.Renderer.SetPropertyBlock(m_EyeColorMaterialPropertyBlock,
                    m_EyeRendererData.MaterialIndex);
            }
        }

        public void OrientTowards(Vector3 lookPosition)
        {
            Vector3 lookDirection = Vector3.ProjectOnPlane(lookPosition - transform.position, Vector3.up).normalized;
            if (lookDirection.sqrMagnitude != 0f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation =
                    Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * OrientationSpeed);
            }
        }

        bool IsPathValid()
        {
            return PatrolPath && PatrolPath.PathNodes.Count > 0;
        }

        public void ResetPathDestination()
        {
            m_PathDestinationNodeIndex = 0;
        }

        public void SetPathDestinationToClosestNode()
        {
            if (IsPathValid())
            {
                int closestPathNodeIndex = 0;
                for (int i = 0; i < PatrolPath.PathNodes.Count; i++)
                {
                    float distanceToPathNode = PatrolPath.GetDistanceToNode(transform.position, i);
                    if (distanceToPathNode < PatrolPath.GetDistanceToNode(transform.position, closestPathNodeIndex))
                    {
                        closestPathNodeIndex = i;
                    }
                }

                m_PathDestinationNodeIndex = closestPathNodeIndex;
            }
            else
            {
                m_PathDestinationNodeIndex = 0;
            }
        }

        public Vector3 GetDestinationOnPath()
        {
            if (IsPathValid())
            {
                return PatrolPath.GetPositionOfPathNode(m_PathDestinationNodeIndex);
            }
            else
            {
                return transform.position;
            }
        }

        public void SetNavDestination(Vector3 destination)
        {
            if (NavMeshAgent != null && NavMeshAgent.enabled && NavMeshAgent.isOnNavMesh)
            {
                NavMeshAgent.SetDestination(destination);
            }
        }

        public Vector3 GetDestinationTowards(Vector3 targetPosition)
        {
            if (NavMeshAgent == null || !NavMeshAgent.isOnNavMesh)
            {
                return transform.position;
            }

            if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, PathReachingRadius, NavMesh.AllAreas))
            {
                return hit.position;
            }

            return targetPosition;
        }

        public void UpdatePathDestination(bool inverseOrder = false)
        {
            if (IsPathValid())
            {
                // Check if reached the path destination
                if ((transform.position - GetDestinationOnPath()).magnitude <= PathReachingRadius)
                {
                    // increment path destination index
                    m_PathDestinationNodeIndex =
                        inverseOrder ? (m_PathDestinationNodeIndex - 1) : (m_PathDestinationNodeIndex + 1);
                    if (m_PathDestinationNodeIndex < 0)
                    {
                        m_PathDestinationNodeIndex += PatrolPath.PathNodes.Count;
                    }

                    if (m_PathDestinationNodeIndex >= PatrolPath.PathNodes.Count)
                    {
                        m_PathDestinationNodeIndex -= PatrolPath.PathNodes.Count;
                    }
                }
            }
        }

        void OnDamaged(float damage, GameObject damageSource)
        {
            // test if the damage source is the player
            if (damageSource && !damageSource.GetComponent<EnemyController>())
            {
                // pursue the player
                DetectionModule.OnDamaged(damageSource);
                TryApplyKnockbackFromPosition(damageSource.transform.position);
                
                onDamaged?.Invoke();
                m_LastTimeDamaged = Time.time;
            
                // play the damage tick sound
                if (!m_WasDamagedThisFrame && m_DamageTickSfxKey != SfxKey.None)
                    Unity.FPS.Game.AudioUtility.PlaySfx(m_DamageTickSfxKey, transform.position);
            
                m_WasDamagedThisFrame = true;
            }
        }

        void OnDie()
        {
            if (m_HasDied)
            {
                return;
            }

            m_HasDied = true;
            StopKnockback(false);
            if (TryGetComponent<ExploderDeathSequence>(out var exploder) && exploder != null &&
                exploder.isActiveAndEnabled)
            {
                // Exploder 会负责：闪烁→爆炸→生成爆炸VFX→自我销毁/回收
                // 这里不要立即隐藏渲染器/生成死亡VFX/掉落/销毁，否则看不到闪烁且序列无法完成。
                HideWorldspaceHealthBarImmediate();
                exploder.Begin();
                return;
            }

            SpawnDeathGibs();
            HideDeadBodyImmediate();
            TriggerDeathCameraShake();

            // spawn a particle system when dying
            if (DeathVfx != null)
            {
                GameObject vfx;
                if (ObjPrefabManager.Instance != null)
                {
                    vfx = ObjPrefabManager.Instance
                        .Spawn(DeathVfx.transform, DeathVfxSpawnPoint.position, Quaternion.identity)
                        .gameObject;
                }
                else
                {
                    vfx = Instantiate(DeathVfx, DeathVfxSpawnPoint.position, Quaternion.identity);
                }

                TimedSelfDestruct timedSelfDestruct = vfx.GetComponent<TimedSelfDestruct>();
                if (timedSelfDestruct != null)
                {
                    timedSelfDestruct.ResetLifetime(5f);
                }
                else
                {
                    Destroy(vfx, 5f);
                }
            }

            // tells the game flow manager to handle the enemy destuction
            m_EnemyManager.UnregisterEnemy(this);
            m_IsRegistered = false;

            // loot an object
            if (TryDropItem())
            {
                if (ObjPrefabManager.Instance != null)
                {
                    ObjPrefabManager.Instance.Spawn(LootPrefab.transform, transform.position, Quaternion.identity);
                }
                else
                {
                    Instantiate(LootPrefab, transform.position, Quaternion.identity);
                }
            }

            // this will call despawn on pooled instances and fallback to destroy otherwise
            if (DeathDuration <= 0f)
            {
                DespawnOrDestroy();
            }
            else
            {
                CancelInvoke(nameof(DespawnOrDestroy));
                Invoke(nameof(DespawnOrDestroy), DeathDuration);
            }
        }

        void TriggerDeathCameraShake()
        {
            if (m_DeathCameraShakeIntensity <= 0f || m_DeathCameraShakeDuration <= 0f)
            {
                return;
            }

            if (m_ActorsManager == null)
            {
                return;
            }

            GameObject player = m_ActorsManager.Player;
            if (player == null)
            {
                return;
            }

            if (m_DeathCameraShakeMaxDistance > 0.001f)
            {
                float sqrDist = (player.transform.position - transform.position).sqrMagnitude;
                if (sqrDist > m_DeathCameraShakeMaxDistance * m_DeathCameraShakeMaxDistance)
                {
                    return;
                }
            }

            var cameraRig = player.GetComponent<FpsCameraRig>();
            if (cameraRig == null)
            {
                return;
            }

            cameraRig.AddShake(m_DeathCameraShakeIntensity, m_DeathCameraShakeDuration);
        }

        void HideWorldspaceHealthBarImmediate()
        {
            // 进入爆炸序列后，血条需要隐藏且不能在 Update 中被重新激活
            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour mb = behaviours[i];
                if (mb == null)
                {
                    continue;
                }

                // 避免跨 asmdef 直接引用 UI 程序集类型，这里用类型名匹配
                if (mb.GetType().Name == "WorldspaceHealthBar")
                {
                    mb.enabled = false;
                }
            }

            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null)
                {
                    continue;
                }

                // 兼容不同 prefab 命名
                if (t.name == "HealthBarPivot" || t.name == "HealthBar Pivot")
                {
                    t.gameObject.SetActive(false);
                }
            }
        }

        void RestoreWorldspaceHealthBarImmediate()
        {
            // 与 HideWorldspaceHealthBarImmediate 对称：池化复用时恢复血条脚本运行
            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour mb = behaviours[i];
                if (mb == null)
                {
                    continue;
                }

                if (mb.GetType().Name == "WorldspaceHealthBar")
                {
                    mb.enabled = true;
                }
            }
        }

        public void UnregisterEnemyFromManager()
        {
            if (m_EnemyManager != null)
            {
                m_EnemyManager.UnregisterEnemy(this);
            }
            m_IsRegistered = false;
        }

        void OnDisable()
        {
            StopKnockback(false);
            CancelInvoke(nameof(DespawnOrDestroy));
            UnregisterSilentlyIfNeeded();
        }

        void UnregisterSilentlyIfNeeded()
        {
            if (!m_IsRegistered || m_EnemyManager == null || m_HasDied)
            {
                return;
            }

            m_EnemyManager.UnregisterEnemySilently(this);
            m_IsRegistered = false;
        }

        void DespawnOrDestroy()
        {
            if (m_PooledInstance != null)
            {
                m_PooledInstance.Despawn();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void SpawnDeathGibs()
        {
            if (m_DeathGibsPrefab == null)
            {
                return;
            }

            Transform spawnPoint = m_GibSpawnPoint != null
                ? m_GibSpawnPoint
                : (DeathVfxSpawnPoint != null ? DeathVfxSpawnPoint : transform);
            Vector3 spawnPosition = spawnPoint.position;
            Quaternion spawnRotation = spawnPoint.rotation;

            Transform parent = DebrisRoot.ResolveParentFor(transform);
            GameObject gibsInstance;
            if (ObjPrefabManager.Instance != null)
            {
                gibsInstance = ObjPrefabManager.Instance
                    .Spawn(m_DeathGibsPrefab.transform, spawnPosition, spawnRotation, parent)
                    .gameObject;
            }
            else
            {
                gibsInstance = Instantiate(m_DeathGibsPrefab, spawnPosition, spawnRotation, parent);
            }

            Vector3 impulseDirection = GetDeathImpulseDirection();
            ApplyImpulseToGibs(gibsInstance, impulseDirection);
            IgnoreGibCollisionWithPlayer(gibsInstance);
            DebrisRoot.RegisterSpawnedDebris(transform, gibsInstance);
        }

        Vector3 GetDeathImpulseDirection()
        {
            LastHitInfo lastHitInfo = m_Health != null ? m_Health.GetComponent<LastHitInfo>() : null;
            if (lastHitInfo != null && lastHitInfo.HasValue && lastHitInfo.LastDirection.sqrMagnitude > 0.0001f)
            {
                return lastHitInfo.LastDirection;
            }

            return -transform.forward;
        }


        void ApplyImpulseToGibs(GameObject gibsRoot, Vector3 baseDirection)
        {
            if (gibsRoot == null)
            {
                return;
            }

            if (baseDirection.sqrMagnitude <= 0.0001f)
            {
                baseDirection = Vector3.forward;
            }

            MeshRenderer[] meshRenderers = gibsRoot.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                MeshRenderer meshRenderer = meshRenderers[i];
                if (meshRenderer == null)
                {
                    continue;
                }

                GameObject piece = meshRenderer.gameObject;
                Rigidbody rigidbody = piece.GetComponent<Rigidbody>();
                if (rigidbody == null)
                {
                    rigidbody = piece.AddComponent<Rigidbody>();
                }

                rigidbody.mass = m_GibPieceMass;
                rigidbody.drag = m_GibLinearDrag;
                rigidbody.angularDrag = m_GibAngularDrag;

                BoxCollider boxCollider = piece.GetComponent<BoxCollider>();
                if (boxCollider == null)
                {
                    boxCollider = piece.AddComponent<BoxCollider>();
                }

                MeshFilter meshFilter = piece.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    Bounds bounds = meshFilter.sharedMesh.bounds;
                    boxCollider.center = bounds.center;
                    boxCollider.size = bounds.size;
                }

                Vector3 randomDirection = Random.insideUnitSphere;
                randomDirection.y = Mathf.Abs(randomDirection.y);
                Vector3 impulseDirection =
                    (baseDirection + (Vector3.up * m_GibUpBias) + (randomDirection * m_GibRandomImpulse)).normalized;
                float impulse = m_GibImpulse + Random.Range(0f, m_GibRandomImpulse);
                rigidbody.AddForce(impulseDirection * impulse, ForceMode.Impulse);
            }
        }

        void IgnoreGibCollisionWithPlayer(GameObject gibsRoot)
        {
            if (gibsRoot == null || m_ActorsManager == null)
            {
                return;
            }

            GameObject player = m_ActorsManager.Player;
            if (player == null)
            {
                return;
            }

            Collider[] gibColliders = gibsRoot.GetComponentsInChildren<Collider>(true);
            Collider[] playerColliders = player.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < gibColliders.Length; i++)
            {
                Collider gc = gibColliders[i];
                if (gc == null || !gc.enabled)
                {
                    continue;
                }
                for (int j = 0; j < playerColliders.Length; j++)
                {
                    Collider pc = playerColliders[j];
                    if (pc == null || !pc.enabled)
                    {
                        continue;
                    }
                    Physics.IgnoreCollision(gc, pc, true);
                }
            }
        }

        void HideDeadBodyImmediate()
        {
            if (NavMeshAgent != null && NavMeshAgent.enabled)
            {
                NavMeshAgent.isStopped = true;
                NavMeshAgent.enabled = false;
            }

            if (m_SelfColliders != null)
            {
                for (int i = 0; i < m_SelfColliders.Length; i++)
                {
                    Collider collider = m_SelfColliders[i];
                    if (collider != null)
                    {
                        collider.enabled = false;
                    }
                }
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }
        }

        void RestoreAliveBodyState()
        {
            if (NavMeshAgent != null && !NavMeshAgent.enabled)
            {
                NavMeshAgent.enabled = true;
            }

            // 仅当 Agent 已放置在 NavMesh 上时才 Resume，避免 Prewarm/池化实例在非 NavMesh 位置时报错
            if (NavMeshAgent != null && NavMeshAgent.enabled && NavMeshAgent.isOnNavMesh)
            {
                NavMeshAgent.isStopped = false;
            }

            if (m_SelfColliders != null)
            {
                for (int i = 0; i < m_SelfColliders.Length; i++)
                {
                    Collider collider = m_SelfColliders[i];
                    if (collider != null)
                    {
                        collider.enabled = true;
                    }
                }
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            // Path reaching range
            Gizmos.color = PathReachingRangeColor;
            Gizmos.DrawWireSphere(transform.position, PathReachingRadius);

            if (DetectionModule != null)
            {
                // Detection range
                Gizmos.color = DetectionRangeColor;
                Gizmos.DrawWireSphere(transform.position, DetectionModule.DetectionRange);

                // Attack range
                Gizmos.color = AttackRangeColor;
                Gizmos.DrawWireSphere(transform.position, DetectionModule.AttackRange);
            }
        }

        public void OrientWeaponsTowards(Vector3 lookPosition)
        {
            for (int i = 0; i < m_Weapons.Length; i++)
            {
                // orient weapon towards player
                Vector3 weaponForward = (lookPosition - m_Weapons[i].WeaponRoot.transform.position).normalized;
                m_Weapons[i].transform.forward = weaponForward;
            }
        }

        public bool TryAtack(Vector3 enemyPosition)
        {
            if (m_HasDied)
                return false;

            if (m_IsInKnockback)
                return false;

            if (m_GameFlowManager.GameIsEnding)
                return false;

            OrientWeaponsTowards(enemyPosition);

            if ((m_LastTimeWeaponSwapped + DelayAfterWeaponSwap) >= Time.time)
                return false;

            // Shoot the weapon
            bool didFire = GetCurrentWeapon().HandleShootInputs(false, true, false);

            if (didFire && onAttack != null)
            {
                onAttack.Invoke();

                if (SwapToNextWeapon && m_Weapons.Length > 1)
                {
                    int nextWeaponIndex = (m_CurrentWeaponIndex + 1) % m_Weapons.Length;
                    SetCurrentWeapon(nextWeaponIndex);
                }
            }

            return didFire;
        }

        public bool TryDropItem()
        {
            if (DropRate == 0 || LootPrefab == null)
                return false;
            else if (DropRate == 1)
                return true;
            else
                return (Random.value <= DropRate);
        }

        void FindAndInitializeAllWeapons()
        {
            // Check if we already found and initialized the weapons
            if (m_Weapons == null)
            {
                m_Weapons = GetComponentsInChildren<WeaponController>();
                DebugUtility.HandleErrorIfNoComponentFound<WeaponController, EnemyController>(m_Weapons.Length, this,
                    gameObject);

                for (int i = 0; i < m_Weapons.Length; i++)
                {
                    m_Weapons[i].Owner = gameObject;
                }
            }
        }

        public WeaponController GetCurrentWeapon()
        {
            FindAndInitializeAllWeapons();
            // Check if no weapon is currently selected
            if (m_CurrentWeapon == null)
            {
                // Set the first weapon of the weapons list as the current weapon
                SetCurrentWeapon(0);
            }

            DebugUtility.HandleErrorIfNullGetComponent<WeaponController, EnemyController>(m_CurrentWeapon, this,
                gameObject);

            return m_CurrentWeapon;
        }

        void SetCurrentWeapon(int index)
        {
            m_CurrentWeaponIndex = index;
            m_CurrentWeapon = m_Weapons[m_CurrentWeaponIndex];
            if (SwapToNextWeapon)
            {
                m_LastTimeWeaponSwapped = Time.time;
            }
            else
            {
                m_LastTimeWeaponSwapped = Mathf.NegativeInfinity;
            }
        }

        /// <summary>
        /// Applies random eye colors. Call after spawn to vary appearance between enemies.
        /// </summary>
        public void SetRandomColors()
        {
            float hue = Random.Range(0f, 1f);
            DefaultEyeColor = Color.HSVToRGB(hue, 0.9f, 1.2f);
            AttackEyeColor = Color.HSVToRGB((hue + 0.08f) % 1f, 1f, 2f);

            if (m_EyeColorMaterialPropertyBlock != null && m_EyeRendererData.Renderer != null)
            {
                m_EyeColorMaterialPropertyBlock.SetColor("_EmissionColor", DefaultEyeColor);
                m_EyeRendererData.Renderer.SetPropertyBlock(m_EyeColorMaterialPropertyBlock, m_EyeRendererData.MaterialIndex);
            }
        }

        void ApplyNavigationBase()
        {
            if (NavMeshAgent == null)
            {
                return;
            }

            NavMeshAgent.speed = m_BaseNavSpeed;
            NavMeshAgent.angularSpeed = m_BaseNavAngularSpeed;
            NavMeshAgent.acceleration = m_BaseNavAcceleration;
        }

        public void ApplyMoveSpeedMultiplierFromStats()
        {
            if (NavMeshAgent == null || m_RoguelikeEnemyStats == null)
            {
                return;
            }

            NavMeshAgent.speed = m_BaseNavSpeed * m_RoguelikeEnemyStats.MoveSpeedMultiplierFinal;
        }

        public bool TryApplyKnockbackFromPosition(Vector3 damageSourcePosition)
        {
            return TryStartKnockback(damageSourcePosition);
        }

        bool TryStartKnockback(Vector3 damageSourcePosition)
        {
            if (!m_CanBeKnockedBack || m_KnockbackDuration <= 0f || m_KnockbackMomentum <= 0f || m_HasDied)
            {
                return false;
            }

            Vector3 awayDirection = transform.position - damageSourcePosition;
            awayDirection = Vector3.ProjectOnPlane(awayDirection, Vector3.up);
            if (awayDirection.sqrMagnitude <= 0.0001f)
            {
                awayDirection = -transform.forward;
                awayDirection = Vector3.ProjectOnPlane(awayDirection, Vector3.up);
            }

            if (awayDirection.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            awayDirection.Normalize();
            m_IsInKnockback = true;
            m_KnockbackRemainingTime = m_KnockbackDuration;
            m_KnockbackVelocity = awayDirection * m_KnockbackMomentum;

            if (NavMeshAgent != null && NavMeshAgent.enabled && NavMeshAgent.isOnNavMesh)
            {
                NavMeshAgent.isStopped = true;
            }

            return true;
        }

        void TickKnockback(float deltaTime)
        {
            if (!m_IsInKnockback)
            {
                return;
            }

            if (m_HasDied || m_KnockbackDuration <= 0f)
            {
                StopKnockback(true);
                return;
            }

            float normalizedRemaining = m_KnockbackRemainingTime / m_KnockbackDuration;
            normalizedRemaining = Mathf.Clamp01(normalizedRemaining);
            Vector3 frameOffset = m_KnockbackVelocity * normalizedRemaining * deltaTime;
            if (frameOffset.sqrMagnitude > 0f)
            {
                if (NavMeshAgent != null && NavMeshAgent.enabled && NavMeshAgent.isOnNavMesh)
                {
                    NavMeshAgent.Move(frameOffset);
                }
                else
                {
                    transform.position += frameOffset;
                }
            }

            m_KnockbackRemainingTime -= deltaTime;
            if (m_KnockbackRemainingTime <= 0f)
            {
                StopKnockback(true);
            }
        }

        void StopKnockback(bool restoreAgentMove)
        {
            m_IsInKnockback = false;
            m_KnockbackRemainingTime = 0f;
            m_KnockbackVelocity = Vector3.zero;

            if (!restoreAgentMove || m_HasDied)
            {
                return;
            }

            if (NavMeshAgent != null && NavMeshAgent.enabled && NavMeshAgent.isOnNavMesh)
            {
                NavMeshAgent.isStopped = false;
            }
        }
    }
}
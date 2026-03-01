using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.GameFramework;
using Unity.FPS.Roguelike.Stats;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class ProjectileStandard : ProjectileBase, IPoolable
    {
        [Header("General")] [Tooltip("Radius of this projectile's collision detection")]
        public float Radius = 0.01f;

        [Tooltip("Transform representing the root of the projectile (used for accurate collision detection)")]
        public Transform Root;

        [Tooltip("Transform representing the tip of the projectile (used for accurate collision detection)")]
        public Transform Tip;

        [Tooltip("LifeTime of the projectile")]
        public float MaxLifeTime = 5f;

        [Tooltip("VFX prefab to spawn upon impact")]
        public GameObject ImpactVfx;

        [Tooltip("LifeTime of the VFX before being destroyed")]
        public float ImpactVfxLifetime = 5f;

        [Tooltip("Offset along the hit normal where the VFX will be spawned")]
        public float ImpactVfxSpawnOffset = 0.1f;

        [Tooltip("Clip to play on impact")] 
        public AudioClip ImpactSfxClip;

        [Tooltip("Layers this projectile can collide with")]
        public LayerMask HittableLayers = -1;

        [Header("Movement")] [Tooltip("Speed of the projectile")]
        public float Speed = 20f;

        [Tooltip("Downward acceleration from gravity")]
        public float GravityDownAcceleration = 0f;

        [Tooltip(
            "Distance over which the projectile will correct its course to fit the intended trajectory (used to drift projectiles towards center of screen in First Person view). At values under 0, there is no correction")]
        public float TrajectoryCorrectionDistance = -1;

        [Tooltip("Determines if the projectile inherits the velocity that the weapon's muzzle had when firing")]
        public bool InheritWeaponVelocity = false;

        [Header("Damage")] [Tooltip("Damage of the projectile")]
        public float Damage = 40f;

        [Tooltip("Area of damage. Keep empty if you don<t want area damage")]
        public DamageArea AreaOfDamage;

        [Header("Game Feel")]
        [SerializeField] float m_PlayerHitEnemyShakeIntensity = 0.12f;
        [SerializeField] float m_PlayerHitEnemyShakeDuration = 0.1f;
        [SerializeField] float m_PlayerHitEnemyHitStopTimeScale = 0.1f;
        [SerializeField] float m_PlayerHitEnemyHitStopDuration = 0.07f;

        [Header("Debug")] [Tooltip("Color of the projectile radius debug view")]
        public Color RadiusColor = Color.cyan * 0.2f;

        ProjectileBase m_ProjectileBase;
        Vector3 m_LastRootPosition;
        Vector3 m_Velocity;
        bool m_HasTrajectoryOverride;
        float m_ShootTime;
        Vector3 m_TrajectoryCorrectionVector;
        Vector3 m_ConsumedTrajectoryCorrectionVector;
        List<Collider> m_IgnoredColliders;
        PooledInstance m_PooledInstance;
        float m_RuntimeDamage;
        int m_RemainingBounces;
        int m_RemainingPierces;
        TrailRenderer[] m_TrailRenderers;
        ParticleSystem[] m_ParticleSystems;
        Renderer[] m_Renderers;

        const QueryTriggerInteraction k_TriggerInteraction = QueryTriggerInteraction.Collide;

        void Awake()
        {
            m_ProjectileBase = GetComponent<ProjectileBase>();
            DebugUtility.HandleErrorIfNullGetComponent<ProjectileBase, ProjectileStandard>(m_ProjectileBase, this,
                gameObject);
            m_PooledInstance = GetComponent<PooledInstance>();
            m_IgnoredColliders = new List<Collider>(32);

            CacheVisualComponents();
            DisableTrailAutoDestruct();
        }

        void OnEnable()
        {
            m_ProjectileBase.OnShoot -= OnShoot;
            m_ProjectileBase.OnShoot += OnShoot;
            ResetVisualsForReuse();
        }

        void OnDisable()
        {
            CancelInvoke(nameof(DespawnProjectile));
            if (m_ProjectileBase != null)
            {
                m_ProjectileBase.OnShoot -= OnShoot;
            }
        }

        public void OnSpawned()
        {
            // Pool calls this while the instance can still be inactive.
            // We do the actual reset in OnEnable to ensure components are active.
            CacheVisualComponents();
            DisableTrailAutoDestruct();
        }

        public void OnDespawned()
        {
            // Ensure lifetime invoke does not survive a despawn.
            CancelInvoke(nameof(DespawnProjectile));

            if (m_TrailRenderers != null)
            {
                for (int i = 0; i < m_TrailRenderers.Length; i++)
                {
                    var tr = m_TrailRenderers[i];
                    if (!tr) continue;
                    tr.emitting = false;
                }
            }
        }

        void DespawnProjectile()
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

        new void OnShoot()
        {
            m_ShootTime = Time.time;
            m_LastRootPosition = Root.position;
            m_Velocity = transform.forward * Speed;
            m_RuntimeDamage = Damage;
            m_RemainingBounces = 0;
            m_IgnoredColliders.Clear();
            transform.position += m_ProjectileBase.InheritedMuzzleVelocity * Time.deltaTime;

            float ownerAttackMultiplier = 1f;
            bool useEnemyAttackPower = false;
            if (m_ProjectileBase.Owner != null)
            {
                if (m_ProjectileBase.Owner.TryGetComponent<RoguelikePlayerStats>(out var playerStats))
                {
                    ownerAttackMultiplier = playerStats.AttackMultiplierFinal;

                    if (Random.value < playerStats.CritChanceFinal)
                    {
                        m_RuntimeDamage *= playerStats.CritDamageMultiplierFinal;
                    }
                }
                else if (m_ProjectileBase.Owner.TryGetComponent<RoguelikeEnemyStats>(out var enemyStats))
                {
                    if (enemyStats.AttackPowerFinal > 0f)
                    {
                        m_RuntimeDamage = enemyStats.AttackPowerFinal;
                        ownerAttackMultiplier = 1f;
                        useEnemyAttackPower = true;
                    }
                    else
                    {
                        ownerAttackMultiplier = enemyStats.AttackMultiplierFinal;
                    }
                }
            }

            float weaponDamageMultiplier = 1f;
            m_RemainingPierces = 0;
            var sourceWeapon = m_ProjectileBase.SourceWeapon;
            if (sourceWeapon != null && sourceWeapon.TryGetComponent<RoguelikeWeaponStatsRuntime>(out var weaponStats))
            {
                weaponDamageMultiplier = weaponStats.DamageMultiplierFinal;
                if (sourceWeapon.ShootType == WeaponShootType.Automatic)
                {
                    m_RemainingBounces = weaponStats.ProjectileBouncesFinal;
                    m_RemainingPierces = weaponStats.PierceCountFinal;
                }
                else
                {
                    m_RemainingBounces = 0;
                }
            }

            if (useEnemyAttackPower)
            {
                m_RuntimeDamage *= weaponDamageMultiplier;
            }
            else
            {
                m_RuntimeDamage *= ownerAttackMultiplier * weaponDamageMultiplier;
            }

            // Ignore colliders of owner
            if (m_ProjectileBase.Owner != null)
            {
                m_ProjectileBase.Owner.GetComponentsInChildren(true, m_IgnoredColliders);
            }

            // Handle case of player shooting (make projectiles not go through walls, and remember center-of-screen trajectory)
            PlayerWeaponsManager playerWeaponsManager = m_ProjectileBase.Owner != null
                ? m_ProjectileBase.Owner.GetComponent<PlayerWeaponsManager>()
                : null;
            if (playerWeaponsManager)
            {
                m_HasTrajectoryOverride = true;

                Vector3 cameraToMuzzle = (m_ProjectileBase.InitialPosition -
                                          playerWeaponsManager.WeaponCamera.transform.position);

                m_TrajectoryCorrectionVector = Vector3.ProjectOnPlane(-cameraToMuzzle,
                    playerWeaponsManager.WeaponCamera.transform.forward);
                if (TrajectoryCorrectionDistance == 0)
                {
                    transform.position += m_TrajectoryCorrectionVector;
                    m_ConsumedTrajectoryCorrectionVector = m_TrajectoryCorrectionVector;
                }
                else if (TrajectoryCorrectionDistance < 0)
                {
                    m_HasTrajectoryOverride = false;
                }

                if (Physics.Raycast(playerWeaponsManager.WeaponCamera.transform.position, cameraToMuzzle.normalized,
                    out RaycastHit hit, cameraToMuzzle.magnitude, HittableLayers, k_TriggerInteraction))
                {
                    if (IsHitValid(hit))
                    {
                        OnHit(hit.point, hit.normal, hit.collider);
                    }
                }
            }

            if (MaxLifeTime > 0f)
            {
                CancelInvoke(nameof(DespawnProjectile));
                Invoke(nameof(DespawnProjectile), MaxLifeTime);
            }
        }

        void CacheVisualComponents()
        {
            if (m_TrailRenderers == null)
            {
                m_TrailRenderers = GetComponentsInChildren<TrailRenderer>(true);
            }

            if (m_ParticleSystems == null)
            {
                m_ParticleSystems = GetComponentsInChildren<ParticleSystem>(true);
            }

            if (m_Renderers == null)
            {
                m_Renderers = GetComponentsInChildren<Renderer>(true);
            }
        }

        void DisableTrailAutoDestruct()
        {
            if (m_TrailRenderers == null) return;

            for (int i = 0; i < m_TrailRenderers.Length; i++)
            {
                var tr = m_TrailRenderers[i];
                if (!tr) continue;
                tr.autodestruct = false;
            }
        }

        void ResetVisualsForReuse()
        {
            CacheVisualComponents();
            DisableTrailAutoDestruct();

            if (m_Renderers != null)
            {
                for (int i = 0; i < m_Renderers.Length; i++)
                {
                    var r = m_Renderers[i];
                    if (!r) continue;
                    r.enabled = true;
                }
            }

            if (m_TrailRenderers != null)
            {
                for (int i = 0; i < m_TrailRenderers.Length; i++)
                {
                    var tr = m_TrailRenderers[i];
                    if (!tr) continue;
                    if (!tr.gameObject.activeSelf) tr.gameObject.SetActive(true);
                    tr.enabled = true;
                    tr.emitting = true;
                    tr.Clear();
                }
            }

            if (m_ParticleSystems != null)
            {
                for (int i = 0; i < m_ParticleSystems.Length; i++)
                {
                    var ps = m_ParticleSystems[i];
                    if (!ps) continue;
                    if (!ps.gameObject.activeSelf) ps.gameObject.SetActive(true);

                    if (ps.main.playOnAwake)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        ps.Clear(true);
                        ps.Play(true);
                    }
                }
            }
        }

        void Update()
        {
            // Move
            transform.position += m_Velocity * Time.deltaTime;
            if (InheritWeaponVelocity)
            {
                transform.position += m_ProjectileBase.InheritedMuzzleVelocity * Time.deltaTime;
            }

            // Drift towards trajectory override (this is so that projectiles can be centered 
            // with the camera center even though the actual weapon is offset)
            if (m_HasTrajectoryOverride && m_ConsumedTrajectoryCorrectionVector.sqrMagnitude <
                m_TrajectoryCorrectionVector.sqrMagnitude)
            {
                Vector3 correctionLeft = m_TrajectoryCorrectionVector - m_ConsumedTrajectoryCorrectionVector;
                float distanceThisFrame = (Root.position - m_LastRootPosition).magnitude;
                Vector3 correctionThisFrame =
                    (distanceThisFrame / TrajectoryCorrectionDistance) * m_TrajectoryCorrectionVector;
                correctionThisFrame = Vector3.ClampMagnitude(correctionThisFrame, correctionLeft.magnitude);
                m_ConsumedTrajectoryCorrectionVector += correctionThisFrame;

                // Detect end of correction
                if (m_ConsumedTrajectoryCorrectionVector.sqrMagnitude == m_TrajectoryCorrectionVector.sqrMagnitude)
                {
                    m_HasTrajectoryOverride = false;
                }

                transform.position += correctionThisFrame;
            }

            // Orient towards velocity
            transform.forward = m_Velocity.normalized;

            // Gravity
            if (GravityDownAcceleration > 0)
            {
                // add gravity to the projectile velocity for ballistic effect
                m_Velocity += Vector3.down * GravityDownAcceleration * Time.deltaTime;
            }

            // Hit detection
            {
                RaycastHit closestHit = new RaycastHit();
                closestHit.distance = Mathf.Infinity;
                bool foundHit = false;

                // Sphere cast
                Vector3 displacementSinceLastFrame = Tip.position - m_LastRootPosition;
                RaycastHit[] hits = Physics.SphereCastAll(m_LastRootPosition, Radius,
                    displacementSinceLastFrame.normalized, displacementSinceLastFrame.magnitude, HittableLayers,
                    k_TriggerInteraction);
                foreach (var hit in hits)
                {
                    if (IsHitValid(hit) && hit.distance < closestHit.distance)
                    {
                        foundHit = true;
                        closestHit = hit;
                    }
                }

                if (foundHit)
                {
                    // Handle case of casting while already inside a collider
                    if (closestHit.distance <= 0f)
                    {
                        closestHit.point = Root.position;
                        closestHit.normal = -transform.forward;
                    }

                    OnHit(closestHit.point, closestHit.normal, closestHit.collider);
                }
            }

            m_LastRootPosition = Root.position;
        }

        bool IsHitValid(RaycastHit hit)
        {
            // ignore hits with an ignore component
            if (hit.collider.GetComponent<IgnoreHitDetection>())
            {
                return false;
            }

            // ignore hits with triggers that don't have a Damageable component
            if (hit.collider.isTrigger && hit.collider.GetComponent<Damageable>() == null)
            {
                return false;
            }

            // ignore hits with specific ignored colliders (self colliders, by default)
            if (m_IgnoredColliders != null && m_IgnoredColliders.Contains(hit.collider))
            {
                return false;
            }

            return true;
        }

        void OnHit(Vector3 point, Vector3 normal, Collider collider)
        {
            Damageable damageable = collider.GetComponent<Damageable>();

            if (m_RemainingBounces > 0 && damageable == null)
            {
                Bounce(point, normal, collider);
                return;
            }

            // damage
            if (AreaOfDamage)
            {
                AreaOfDamage.InflictDamageInArea(m_RuntimeDamage, point, HittableLayers, k_TriggerInteraction,
                    m_ProjectileBase.Owner);
            }
            else
            {
                if (damageable)
                {
                    RecordLastHitInfo(damageable, point);
                    damageable.InflictDamage(m_RuntimeDamage, false, m_ProjectileBase.Owner);
                    ApplyPlayerHitEnemyFeedback(collider);
                }
            }

            // penetration: Auto weapon with pierce count can pass through damageables
            if (damageable != null && m_RemainingPierces > 0)
            {
                if (m_IgnoredColliders != null && collider != null)
                {
                    m_IgnoredColliders.Add(collider);
                }
                m_RemainingPierces--;
                if (ImpactVfx)
                {
                    SpawnImpactVfx(point, normal);
                }
                if (ImpactSfxClip)
                {
                    AudioUtility.CreateSFX(ImpactSfxClip, point, AudioUtility.AudioGroups.Impact, 1f, 3f);
                }
                return;
            }

            // impact vfx
            if (ImpactVfx)
            {
                SpawnImpactVfx(point, normal);
            }

            if (ImpactSfxClip)
            {
                AudioUtility.CreateSFX(ImpactSfxClip, point, AudioUtility.AudioGroups.Impact, 1f, 3f);
            }

            DespawnProjectile();
        }

        void RecordLastHitInfo(Damageable damageable, Vector3 point)
        {
            if (damageable == null)
            {
                return;
            }

            Health targetHealth = damageable.Health;
            if (targetHealth == null)
            {
                return;
            }

            LastHitInfo hitInfo = targetHealth.GetComponent<LastHitInfo>();
            if (hitInfo == null)
            {
                hitInfo = targetHealth.gameObject.AddComponent<LastHitInfo>();
            }

            Vector3 direction = m_Velocity.sqrMagnitude > 0.0001f ? m_Velocity.normalized : transform.forward;
            hitInfo.Set(direction, point);
        }

        void ApplyPlayerHitEnemyFeedback(Collider collider)
        {
            if (collider == null || m_ProjectileBase == null || m_ProjectileBase.Owner == null)
            {
                return;
            }

            // Only apply when a player projectile hits an enemy.
            if (collider.GetComponentInParent<RoguelikeEnemyStats>() == null)
            {
                return;
            }

            var owner = m_ProjectileBase.Owner;
            if (owner.GetComponent<PlayerCharacterController>() == null)
            {
                return;
            }

            var cameraRig = owner.GetComponent<FpsCameraRig>();
            if (cameraRig != null)
            {
                cameraRig.AddShake(m_PlayerHitEnemyShakeIntensity, m_PlayerHitEnemyShakeDuration);
            }

            var hitStop = owner.GetComponent<HitStopController>();
            if (hitStop != null)
            {
                hitStop.Request(m_PlayerHitEnemyHitStopTimeScale, m_PlayerHitEnemyHitStopDuration);
            }
        }

        void SpawnImpactVfx(Vector3 point, Vector3 normal)
        {
            if (ImpactVfx == null) return;
            GameObject impactVfxInstance;
            if (ObjPrefabManager.Instance != null)
            {
                impactVfxInstance = ObjPrefabManager.Instance
                    .Spawn(ImpactVfx.transform, point + (normal * ImpactVfxSpawnOffset), Quaternion.LookRotation(normal))
                    .gameObject;
            }
            else
            {
                impactVfxInstance = Instantiate(ImpactVfx, point + (normal * ImpactVfxSpawnOffset),
                    Quaternion.LookRotation(normal));
            }
            if (ImpactVfxLifetime > 0)
            {
                var timedSelfDestruct = impactVfxInstance.GetComponent<TimedSelfDestruct>();
                if (timedSelfDestruct != null)
                    timedSelfDestruct.ResetLifetime(ImpactVfxLifetime);
                else
                    Destroy(impactVfxInstance.gameObject, ImpactVfxLifetime);
            }
        }

        void Bounce(Vector3 point, Vector3 normal, Collider collider)
        {
            m_RemainingBounces--;
            m_Velocity = Vector3.Reflect(m_Velocity, normal);
            transform.position = point + (normal * (Radius + 0.01f));
            transform.forward = m_Velocity.sqrMagnitude > 0.001f ? m_Velocity.normalized : transform.forward;
            m_LastRootPosition = Root.position;

            if (m_IgnoredColliders != null && collider != null)
            {
                m_IgnoredColliders.Add(collider);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = RadiusColor;
            Gizmos.DrawSphere(transform.position, Radius);
        }
    }
}
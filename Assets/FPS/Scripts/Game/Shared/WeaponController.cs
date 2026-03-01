using System;
using System.Collections.Generic;
using DG.Tweening;
using Unity.FPS.GameFramework;
using Unity.FPS.Roguelike.Stats;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.Game
{
    public enum WeaponShootType
    {
        Manual,
        Automatic,
        Charge,
    }

    [System.Serializable]
    public struct CrosshairData
    {
        [Tooltip("The image that will be used for this weapon's crosshair")]
        public Sprite CrosshairSprite;

        [Tooltip("The size of the crosshair image")]
        public int CrosshairSize;

        [Tooltip("The color of the crosshair image")]
        public Color CrosshairColor;
    }

    [RequireComponent(typeof(AudioSource))]
    public class WeaponController : MonoBehaviour
    {
        [Header("Information")] [Tooltip("The name that will be displayed in the UI for this weapon")]
        public string WeaponName;

        [Tooltip("The image that will be displayed in the UI for this weapon")]
        public Sprite WeaponIcon;

        [Tooltip("Default data for the crosshair")]
        public CrosshairData CrosshairDataDefault;

        [Tooltip("Data for the crosshair when targeting an enemy")]
        public CrosshairData CrosshairDataTargetInSight;

        [Header("Internal References")]
        [Tooltip("The root object for the weapon, this is what will be deactivated when the weapon isn't active")]
        public GameObject WeaponRoot;

        [Tooltip("Tip of the weapon, where the projectiles are shot")]
        public Transform WeaponMuzzle;

        [Header("Shoot Parameters")] [Tooltip("The type of weapon wil affect how it shoots")]
        public WeaponShootType ShootType;

        [Tooltip("The projectile prefab")] public ProjectileBase ProjectilePrefab;

        [Tooltip("Minimum duration between two shots")]
        public float DelayBetweenShots = 0.5f;

        [Tooltip("Angle for the cone in which the bullets will be shot randomly (0 means no spread at all)")]
        public float BulletSpreadAngle = 0f;

        [Tooltip("Amount of bullets per shot")]
        public int BulletsPerShot = 1;

        [Tooltip("Enable triple trajectory mode for rifles (center, left, right)")]
        public bool UseTripleShot = false;

        [Tooltip("Left and right trajectory angle offset when triple trajectory is enabled")]
        public float TripleShotAngleOffset = 20f;

        [Tooltip("Force that will push back the weapon after each shot")] [Range(0f, 2f)]
        public float RecoilForce = 1;

        [Tooltip("Ratio of the default FOV that this weapon applies while aiming")] [Range(0f, 1f)]
        public float AimZoomRatio = 1f;

        [Tooltip("Translation to apply to weapon arm when aiming with this weapon")]
        public Vector3 AimOffset;

        [Header("Ammo Parameters")]
        [Tooltip("Should the player manually reload")]
        public bool AutomaticReload = true;
        [Tooltip("Has physical clip on the weapon and ammo shells are ejected when firing")]
        public bool HasPhysicalBullets = false;
        [Tooltip("Number of bullets in a clip")]
        public int ClipSize = 30;
        [Tooltip("Bullet Shell Casing")]
        public GameObject ShellCasing;
        [Tooltip("Weapon Ejection Port for physical ammo")]
        public Transform EjectionPort;
        [Tooltip("Force applied on the shell")]
        [Range(0.0f, 5.0f)] public float ShellCasingEjectionForce = 2.0f;
        [Tooltip("Maximum number of shell that can be spawned before reuse")]
        [Range(1, 30)] public int ShellPoolSize = 1;
        [Tooltip("Amount of ammo reloaded per second")]
        public float AmmoReloadRate = 1f;

        [Tooltip("Delay after the last shot before starting to reload")]
        public float AmmoReloadDelay = 2f;

        [Tooltip("Maximum amount of ammo in the gun")]
        public int MaxAmmo = 8;

        [Header("Charging parameters (charging weapons only)")]
        [Tooltip("Trigger a shot when maximum charge is reached")]
        public bool AutomaticReleaseOnCharged;

        [Tooltip("Duration to reach maximum charge")]
        public float MaxChargeDuration = 2f;

        [Tooltip("Initial ammo used when starting to charge")]
        public float AmmoUsedOnStartCharge = 1f;

        [Tooltip("Additional ammo used when charge reaches its maximum")]
        public float AmmoUsageRateWhileCharging = 1f;

        [Header("Audio & Visual")] 
        [Tooltip("Optional weapon animator for OnShoot animations")]
        public Animator WeaponAnimator;

        [Tooltip("Prefab of the muzzle flash")]
        public GameObject MuzzleFlashPrefab;

        [Tooltip("Unparent the muzzle flash instance on spawn")]
        public bool UnparentMuzzleFlash;

        [Tooltip("sound played when shooting")]
        public AudioClip ShootSfx;

        [Tooltip("Sound played when changing to this weapon")]
        public AudioClip ChangeWeaponSfx;

        [Tooltip("Continuous Shooting Sound")] public bool UseContinuousShootSound = false;
        public AudioClip ContinuousShootStartSfx;
        public AudioClip ContinuousShootLoopSfx;
        public AudioClip ContinuousShootEndSfx;

        AudioSource m_ContinuousShootAudioSource = null;
        bool m_WantsToShoot = false;

        public UnityAction OnShoot;
        public event Action OnShootProcessed;

        int m_CarriedPhysicalBullets;
        float m_CurrentAmmo;
        float m_LastTimeShot = Mathf.NegativeInfinity;
        public float LastChargeTriggerTimestamp { get; private set; }
        Vector3 m_LastMuzzlePosition;
        bool m_IsBurstFiring;
        int m_BurstShotsRemaining;
        float m_NextBurstShotTime;
        float m_BurstChargeRatio = 1f;

        GameObject m_Owner;
        RoguelikeEnemyStats m_OwnerEnemyStats;

        public GameObject Owner
        {
            get => m_Owner;
            set
            {
                m_Owner = value;
                m_OwnerEnemyStats = value != null && value.TryGetComponent<RoguelikeEnemyStats>(out var s) ? s : null;
            }
        }

        public GameObject SourcePrefab { get; set; }

        /// <summary> 考虑敌人攻击速度乘数后的开火间隔；玩家或未配置时等于 DelayBetweenShots。 </summary>
        float GetEffectiveDelayBetweenShots()
        {
            if (m_OwnerEnemyStats == null)
            {
                return DelayBetweenShots;
            }
            return DelayBetweenShots / Mathf.Max(0.01f, m_OwnerEnemyStats.AttackSpeedMultiplierFinal);
        }
        public bool IsCharging { get; private set; }
        public float CurrentAmmoRatio { get; private set; }
        public bool IsWeaponActive { get; private set; }
        public bool IsCooling { get; private set; }
        public float CurrentCharge { get; private set; }
        public Vector3 MuzzleWorldVelocity { get; private set; }

        public float GetAmmoNeededToShoot() =>
            (ShootType != WeaponShootType.Charge ? 1f : Mathf.Max(1f, AmmoUsedOnStartCharge)) /
            (MaxAmmo * BulletsPerShot);

        public int GetCarriedPhysicalBullets() => m_CarriedPhysicalBullets;
        public int GetCurrentAmmo() => Mathf.FloorToInt(m_CurrentAmmo);

        AudioSource m_ShootAudioSource;
        RoguelikeWeaponStatsRuntime m_WeaponStatsRuntime;

        public bool IsReloading { get; private set; }

        const string k_AnimAttackParameter = "Attack";

        private Queue<Rigidbody> m_PhysicalAmmoPool;
        void Awake()
        {
            m_CurrentAmmo = MaxAmmo;
            m_CarriedPhysicalBullets = HasPhysicalBullets ? ClipSize : 0;
            m_LastMuzzlePosition = WeaponMuzzle.position;

            m_ShootAudioSource = GetComponent<AudioSource>();
            DebugUtility.HandleErrorIfNullGetComponent<AudioSource, WeaponController>(m_ShootAudioSource, this,
                gameObject);
            m_WeaponStatsRuntime = GetComponent<RoguelikeWeaponStatsRuntime>();

            if (UseContinuousShootSound)
            {
                m_ContinuousShootAudioSource = gameObject.AddComponent<AudioSource>();
                m_ContinuousShootAudioSource.playOnAwake = false;
                m_ContinuousShootAudioSource.clip = ContinuousShootLoopSfx;
                m_ContinuousShootAudioSource.outputAudioMixerGroup =
                    AudioUtility.GetAudioGroup(AudioUtility.AudioGroups.WeaponShoot);
                m_ContinuousShootAudioSource.loop = true;
            }

            if (HasPhysicalBullets)
            {
                m_PhysicalAmmoPool = new Queue<Rigidbody>(ShellPoolSize);

                for (int i = 0; i < ShellPoolSize; i++)
                {
                    GameObject shell = Instantiate(ShellCasing, transform);
                    shell.SetActive(false);
                    m_PhysicalAmmoPool.Enqueue(shell.GetComponent<Rigidbody>());
                }
            }
        }

        public void AddCarriablePhysicalBullets(int count) => m_CarriedPhysicalBullets = Mathf.Max(m_CarriedPhysicalBullets + count, MaxAmmo);

        void ShootShell()
        {
            Rigidbody nextShell = m_PhysicalAmmoPool.Dequeue();

            nextShell.transform.position = EjectionPort.transform.position;
            nextShell.transform.rotation = EjectionPort.transform.rotation;
            nextShell.gameObject.SetActive(true);
            nextShell.transform.SetParent(null);
            nextShell.collisionDetectionMode = CollisionDetectionMode.Continuous;
            nextShell.AddForce(nextShell.transform.up * ShellCasingEjectionForce, ForceMode.Impulse);

            m_PhysicalAmmoPool.Enqueue(nextShell);
        }

        void PlaySFX(AudioClip sfx) => AudioUtility.CreateSFX(sfx, transform.position, AudioUtility.AudioGroups.WeaponShoot, 0.0f);


        void Reload()
        {
            if (m_CarriedPhysicalBullets > 0)
            {
                m_CurrentAmmo = Mathf.Min(m_CarriedPhysicalBullets, ClipSize);
            }

            IsReloading = false;
        }

        public void StartReloadAnimation()
        {
            if (m_CurrentAmmo < m_CarriedPhysicalBullets)
            {
                GetComponent<Animator>().SetTrigger("Reload");
                IsReloading = true;
            }
        }

        void Update()
        {
            UpdateAmmo();
            UpdateCharge();
            UpdateBurstFire();
            UpdateContinuousShootSound();

            if (Time.deltaTime > 0)
            {
                MuzzleWorldVelocity = (WeaponMuzzle.position - m_LastMuzzlePosition) / Time.deltaTime;
                m_LastMuzzlePosition = WeaponMuzzle.position;
            }
        }

        void UpdateAmmo()
        {
            if (AutomaticReload && m_LastTimeShot + AmmoReloadDelay < Time.time && m_CurrentAmmo < MaxAmmo && !IsCharging)
            {
                // reloads weapon over time
                m_CurrentAmmo += AmmoReloadRate * Time.deltaTime;

                // limits ammo to max value
                m_CurrentAmmo = Mathf.Clamp(m_CurrentAmmo, 0, MaxAmmo);

                IsCooling = true;
            }
            else
            {
                IsCooling = false;
            }

            if (MaxAmmo == Mathf.Infinity)
            {
                CurrentAmmoRatio = 1f;
            }
            else
            {
                CurrentAmmoRatio = m_CurrentAmmo / MaxAmmo;
            }
        }

        void UpdateCharge()
        {
            if (IsCharging)
            {
                if (CurrentCharge < 1f)
                {
                    float chargeLeft = 1f - CurrentCharge;
                    float effectiveMaxChargeDuration = MaxChargeDuration;
                    if (ShootType == WeaponShootType.Charge && m_WeaponStatsRuntime != null)
                    {
                        effectiveMaxChargeDuration *= m_WeaponStatsRuntime.ChargeDurationMulFinal;
                    }

                    // Calculate how much charge ratio to add this frame
                    float chargeAdded = 0f;
                    if (effectiveMaxChargeDuration <= 0f)
                    {
                        chargeAdded = chargeLeft;
                    }
                    else
                    {
                        chargeAdded = (1f / effectiveMaxChargeDuration) * Time.deltaTime;
                    }

                    chargeAdded = Mathf.Clamp(chargeAdded, 0f, chargeLeft);

                    // See if we can actually add this charge
                    float ammoThisChargeWouldRequire = chargeAdded * AmmoUsageRateWhileCharging;
                    if (ammoThisChargeWouldRequire <= m_CurrentAmmo)
                    {
                        // Use ammo based on charge added
                        UseAmmo(ammoThisChargeWouldRequire);

                        // set current charge ratio
                        CurrentCharge = Mathf.Clamp01(CurrentCharge + chargeAdded);
                    }
                }
            }
        }

        void UpdateContinuousShootSound()
        {
            if (UseContinuousShootSound)
            {
                if (m_WantsToShoot && m_CurrentAmmo >= 1f)
                {
                    if (!m_ContinuousShootAudioSource.isPlaying)
                    {
                        m_ShootAudioSource.PlayOneShot(ShootSfx);
                        m_ShootAudioSource.PlayOneShot(ContinuousShootStartSfx);
                        m_ContinuousShootAudioSource.Play();
                    }
                }
                else if (m_ContinuousShootAudioSource.isPlaying)
                {
                    m_ShootAudioSource.PlayOneShot(ContinuousShootEndSfx);
                    m_ContinuousShootAudioSource.Stop();
                }
            }
        }

        public void ShowWeapon(bool show)
        {
            WeaponRoot.SetActive(show);

            if (show && ChangeWeaponSfx)
            {
                m_ShootAudioSource.PlayOneShot(ChangeWeaponSfx);
            }

            IsWeaponActive = show;
        }

        public void UseAmmo(float amount)
        {
            m_CurrentAmmo = Mathf.Clamp(m_CurrentAmmo - amount, 0f, MaxAmmo);
            m_CarriedPhysicalBullets -= Mathf.RoundToInt(amount);
            m_CarriedPhysicalBullets = Mathf.Clamp(m_CarriedPhysicalBullets, 0, MaxAmmo);
            m_LastTimeShot = Time.time;
        }

        public bool HandleShootInputs(bool inputDown, bool inputHeld, bool inputUp)
        {
            m_WantsToShoot = inputDown || inputHeld;
            if (m_IsBurstFiring)
            {
                return false;
            }

            switch (ShootType)
            {
                case WeaponShootType.Manual:
                    if (inputDown)
                    {
                        return TryShoot();
                    }

                    return false;

                case WeaponShootType.Automatic:
                    if (inputHeld)
                    {
                        return TryShoot();
                    }

                    return false;

                case WeaponShootType.Charge:
                    if (inputHeld)
                    {
                        TryBeginCharge();
                    }

                    // Check if we released charge or if the weapon shoot autmatically when it's fully charged
                    if (inputUp || (AutomaticReleaseOnCharged && CurrentCharge >= 1f))
                    {
                        return TryReleaseCharge();
                    }

                    return false;

                default:
                    return false;
            }
        }

        bool TryShoot()
        {
            if (m_CurrentAmmo >= 1f
                && m_LastTimeShot + GetEffectiveDelayBetweenShots() < Time.time)
            {
                HandleShoot(1f);
                m_CurrentAmmo -= 1f;

                if (ShootType == WeaponShootType.Manual)
                {
                    StartBurstFireIfNeeded(GetBurstShotCount(), 1f);
                }

                return true;
            }

            return false;
        }

        bool TryBeginCharge()
        {
            if (!IsCharging
                && m_CurrentAmmo >= AmmoUsedOnStartCharge
                && Mathf.FloorToInt((m_CurrentAmmo - AmmoUsedOnStartCharge) * BulletsPerShot) > 0
                && m_LastTimeShot + GetEffectiveDelayBetweenShots() < Time.time)
            {
                UseAmmo(AmmoUsedOnStartCharge);

                LastChargeTriggerTimestamp = Time.time;
                IsCharging = true;

                return true;
            }

            return false;
        }

        bool TryReleaseCharge()
        {
            if (IsCharging)
            {
                float releaseCharge = CurrentCharge;
                HandleShoot(releaseCharge);
                StartBurstFireIfNeeded(GetBurstShotCount(), releaseCharge);

                CurrentCharge = 0f;
                IsCharging = false;

                return true;
            }

            return false;
        }

        void HandleShoot(float chargeRatio)
        {
            int additionalProjectiles = 0;
            if (ShootType == WeaponShootType.Automatic && m_WeaponStatsRuntime != null)
            {
                additionalProjectiles = m_WeaponStatsRuntime.AdditionalProjectilesFinal;
            }

            int bulletsPerShotFinal = ShootType == WeaponShootType.Charge
                ? Mathf.CeilToInt(chargeRatio * BulletsPerShot)
                : BulletsPerShot;
            bulletsPerShotFinal = Mathf.Max(1, bulletsPerShotFinal + additionalProjectiles);

            bool useTripleShot = ShootType == WeaponShootType.Automatic && UseTripleShot;
            float sideAngleOffset = Mathf.Abs(TripleShotAngleOffset);

            // spawn all bullets with random direction
            for (int i = 0; i < bulletsPerShotFinal; i++)
            {
                Vector3 centerDirection = GetShotDirectionWithinSpread(WeaponMuzzle);
                SpawnProjectile(centerDirection);

                if (useTripleShot)
                {
                    Vector3 leftDirection =
                        Quaternion.AngleAxis(-sideAngleOffset, WeaponMuzzle.up) * centerDirection;
                    Vector3 rightDirection =
                        Quaternion.AngleAxis(sideAngleOffset, WeaponMuzzle.up) * centerDirection;
                    SpawnProjectile(leftDirection);
                    SpawnProjectile(rightDirection);
                }
            }

            // muzzle flash
            if (MuzzleFlashPrefab != null)
            {
                GameObject muzzleFlashInstance;
                if (ObjPrefabManager.Instance != null)
                {
                    Transform parent = UnparentMuzzleFlash ? null : WeaponMuzzle.transform;
                    muzzleFlashInstance = ObjPrefabManager.Instance.Spawn(MuzzleFlashPrefab.transform, WeaponMuzzle.position,
                        WeaponMuzzle.rotation, parent).gameObject;
                }
                else
                {
                    muzzleFlashInstance = Instantiate(MuzzleFlashPrefab, WeaponMuzzle.position,
                        WeaponMuzzle.rotation, WeaponMuzzle.transform);
                }

                // Unparent the muzzleFlashInstance
                if (UnparentMuzzleFlash)
                {
                    muzzleFlashInstance.transform.SetParent(null);
                }

                TimedSelfDestruct timedSelfDestruct = muzzleFlashInstance.GetComponent<TimedSelfDestruct>();
                if (timedSelfDestruct != null)
                {
                    timedSelfDestruct.ResetLifetime(2f);
                }
                else
                {
                    Destroy(muzzleFlashInstance, 2f);
                }
            }

            if (HasPhysicalBullets)
            {
                ShootShell();
                m_CarriedPhysicalBullets--;
            }

            m_LastTimeShot = Time.time;

            // play shoot SFX
            if (ShootSfx && !UseContinuousShootSound)
            {
                m_ShootAudioSource.PlayOneShot(ShootSfx);
            }

            // Trigger attack animation if there is any
            if (WeaponAnimator)
            {
                WeaponAnimator.SetTrigger(k_AnimAttackParameter);
            }

            OnShoot?.Invoke();
            OnShootProcessed?.Invoke();
        }

        void SpawnProjectile(Vector3 shotDirection)
        {
            ProjectileBase newProjectile = ProjectilePrefab;
            Quaternion projectileRotation = Quaternion.LookRotation(shotDirection);
            if (ObjPrefabManager.Instance != null)
            {
                newProjectile = ObjPrefabManager.Instance.Spawn(ProjectilePrefab, WeaponMuzzle.position,
                    projectileRotation);
            }
            else
            {
                newProjectile = Instantiate(ProjectilePrefab, WeaponMuzzle.position, projectileRotation);
            }

            newProjectile.Shoot(this);
        }

        int GetBurstShotCount()
        {
            if ((ShootType == WeaponShootType.Charge || ShootType == WeaponShootType.Manual) &&
                m_WeaponStatsRuntime != null)
            {
                return Mathf.Max(1, m_WeaponStatsRuntime.BurstShotCountFinal);
            }

            return 1;
        }

        void StartBurstFireIfNeeded(int burstShotCount, float chargeRatio)
        {
            int remainingShots = Mathf.Max(0, burstShotCount - 1);
            if (remainingShots <= 0)
            {
                m_IsBurstFiring = false;
                m_BurstShotsRemaining = 0;
                return;
            }

            m_BurstChargeRatio = chargeRatio;
            m_BurstShotsRemaining = remainingShots;
            m_NextBurstShotTime = Time.time + Mathf.Max(GetEffectiveDelayBetweenShots(), 0.0001f);
            m_IsBurstFiring = true;
        }

        void UpdateBurstFire()
        {
            if (!m_IsBurstFiring)
            {
                return;
            }

            if (m_BurstShotsRemaining <= 0)
            {
                m_IsBurstFiring = false;
                return;
            }

            float burstInterval = Mathf.Max(GetEffectiveDelayBetweenShots(), 0.0001f);
            while (m_BurstShotsRemaining > 0 && Time.time >= m_NextBurstShotTime)
            {
                if (m_CurrentAmmo < 1f)
                {
                    m_IsBurstFiring = false;
                    m_BurstShotsRemaining = 0;
                    return;
                }

                HandleShoot(m_BurstChargeRatio);
                m_CurrentAmmo -= 1f;
                m_BurstShotsRemaining--;
                m_NextBurstShotTime += burstInterval;
            }

            if (m_BurstShotsRemaining <= 0)
            {
                m_IsBurstFiring = false;
            }
        }

        public void ApplyRuntimeMaxAmmo(int newMaxAmmo)
        {
            int clampedMaxAmmo = Mathf.Max(1, newMaxAmmo);
            if (clampedMaxAmmo == MaxAmmo)
            {
                return;
            }

            float ammoRatio = MaxAmmo > 0 ? m_CurrentAmmo / MaxAmmo : 1f;
            float carriedAmmoRatio = MaxAmmo > 0 ? (float)m_CarriedPhysicalBullets / MaxAmmo : 1f;

            MaxAmmo = clampedMaxAmmo;
            m_CurrentAmmo = Mathf.Clamp(ammoRatio * MaxAmmo, 0f, MaxAmmo);
            m_CarriedPhysicalBullets = Mathf.Clamp(Mathf.RoundToInt(carriedAmmoRatio * MaxAmmo), 0, MaxAmmo);
            CurrentAmmoRatio = m_CurrentAmmo / MaxAmmo;
        }

        public void ApplyRuntimeClipSize(int newClipSize)
        {
            int clampedClipSize = Mathf.Max(1, newClipSize);
            if (clampedClipSize == ClipSize)
            {
                return;
            }

            ClipSize = clampedClipSize;
            m_CurrentAmmo = Mathf.Clamp(m_CurrentAmmo, 0f, MaxAmmo);
            if (HasPhysicalBullets)
            {
                m_CarriedPhysicalBullets = Mathf.Clamp(m_CarriedPhysicalBullets, 0, MaxAmmo);
            }
        }

        public Vector3 GetShotDirectionWithinSpread(Transform shootTransform)
        {
            float spreadAngleRatio = BulletSpreadAngle / 180f;
            Vector3 spreadWorldDirection = Vector3.Slerp(shootTransform.forward, UnityEngine.Random.insideUnitSphere,
                spreadAngleRatio);

            return spreadWorldDirection;
        }
    }
}
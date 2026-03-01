using UnityEngine;

namespace Unity.FPS.Gameplay
{
    [DefaultExecutionOrder(30)]
    [RequireComponent(typeof(PlayerCharacterController))]
    public class FpsCameraRig : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] Transform m_CameraFollowPoint;
        [SerializeField] Camera m_MainCamera;

        [Header("Follow Damping")]
        [Tooltip("Damping for forward/back movement (seconds). Larger = more lag.")]
        [SerializeField] float m_ForwardDamping = 0.01f;

        [Tooltip("Damping for left/right movement (seconds). Larger = more lag.")]
        [SerializeField] float m_LateralDamping = 0.01f;

        [Tooltip("Smooth time for vertical movement e.g. jump (seconds). Larger = more lag, smoother catch-up.")]
        [SerializeField] float m_VerticalDamping = 0.01f;

        [Tooltip("Max speed for vertical follow (m/s). Limits snap on big jumps.")]
        [SerializeField] float m_VerticalMaxSpeed = 18f;

        [Tooltip("Max distance camera can lag behind player (meters).")]
        [SerializeField] float m_MaxOffsetDistance = 0.1f;

        [Header("Landing Wobble")]
        [Tooltip("Initial vertical drop on landing (meters). Camera dips then bounces back.")]
        [SerializeField] float m_LandingWobbleAmount = 0.025f;

        [Tooltip("Oscillation frequency of the bounce (Hz).")]
        [SerializeField] float m_LandingWobbleFrequency = 2.5f;

        [Tooltip("Damping 0=very bouncy, 1=no overshoot.")]
        [Range(0.1f, 1f)]
        [SerializeField] float m_LandingWobbleDamping = 0.35f;

        [Header("Runtime Rig")]
        [SerializeField] bool m_ReparentMainCameraAtRuntime = true;

        [Tooltip("When true, weapon camera and weapon hierarchy follow target pose with no damping/breathing/shake")]
        [SerializeField] bool m_SeparateWeaponCamera = true;

        PlayerCharacterController m_Controller;
        Transform m_PlayerTransform;
        PlayerWeaponsManager m_WeaponsManager;
        Transform m_WeaponGroupRoot;

        Transform m_RigRoot;
        Transform m_YawPivot;
        Transform m_PitchPivot;
        Transform m_EffectsPivot;
        Transform m_WeaponAnchor;
        FpsCameraEffects m_CameraEffects;

        Vector3 m_OffsetLocal;
        float m_VerticalOffsetVelocity;
        bool m_WasGrounded;
        float m_LandingWobbleOffset;
        float m_LandingWobbleVelocity;
        bool m_Initialized;

        public FpsCameraEffects CameraEffects => m_CameraEffects;

        void Awake()
        {
            m_Controller = GetComponent<PlayerCharacterController>();
            m_PlayerTransform = transform;
            m_WeaponsManager = GetComponent<PlayerWeaponsManager>();

            if (m_MainCamera == null)
            {
                m_MainCamera = m_Controller != null ? m_Controller.PlayerCamera : null;
            }

            if (m_CameraFollowPoint == null && m_Controller != null)
            {
                m_CameraFollowPoint = m_Controller.CameraFollowPoint;
            }

            BuildRigIfNeeded();
            InitializeState();
        }

        void LateUpdate()
        {
            if (!m_Initialized || m_Controller == null || m_CameraFollowPoint == null || m_RigRoot == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            UpdateRotation();
            UpdateWeaponAnchor();
            UpdateFollow(dt);
            UpdateLandingWobble(dt);
            UpdateEffectsMoveFactor();
            m_WasGrounded = m_Controller.IsGrounded;
        }

        void BuildRigIfNeeded()
        {
            if (m_MainCamera == null)
            {
                return;
            }

            if (m_RigRoot == null)
            {
                var rigRootGo = new GameObject("CameraRigRoot");
                m_RigRoot = rigRootGo.transform;
            }

            if (m_YawPivot == null)
            {
                var yawGo = new GameObject("YawAlign");
                m_YawPivot = yawGo.transform;
                m_YawPivot.SetParent(m_RigRoot, false);
            }

            if (m_PitchPivot == null)
            {
                var pitchGo = new GameObject("PitchPivot");
                m_PitchPivot = pitchGo.transform;
                m_PitchPivot.SetParent(m_YawPivot, false);
            }

            if (m_EffectsPivot == null)
            {
                var effectsGo = new GameObject("EffectsPivot");
                m_EffectsPivot = effectsGo.transform;
                m_EffectsPivot.SetParent(m_PitchPivot, false);
            }

            if (m_ReparentMainCameraAtRuntime && m_MainCamera.transform.parent != m_EffectsPivot)
            {
                // Main camera is driven by rig (lag + breathing + shake)
                m_MainCamera.transform.SetParent(m_EffectsPivot, false);
                m_MainCamera.transform.localPosition = Vector3.zero;
                m_MainCamera.transform.localRotation = Quaternion.identity;
            }

            SetupWeaponHierarchyIfNeeded();

            m_CameraEffects = m_EffectsPivot.GetComponent<FpsCameraEffects>();
            if (m_CameraEffects == null)
            {
                m_CameraEffects = m_EffectsPivot.gameObject.AddComponent<FpsCameraEffects>();
            }

            if (m_Controller != null)
            {
                m_Controller.CameraPitchPivot = m_PitchPivot;
            }
        }

        void SetupWeaponHierarchyIfNeeded()
        {
            if (!m_SeparateWeaponCamera || m_WeaponsManager == null || m_WeaponsManager.WeaponParentSocket == null ||
                m_CameraFollowPoint == null)
            {
                return;
            }

            if (m_WeaponAnchor == null)
            {
                var weaponAnchorGo = new GameObject("WeaponRigAnchor");
                m_WeaponAnchor = weaponAnchorGo.transform;
                m_WeaponAnchor.SetParent(m_PlayerTransform, false);
            }

            // WeaponGroupRoot is the common parent of weapon sockets/poses (Default/Aiming/Down + WeaponParentSocket).
            if (m_WeaponGroupRoot == null)
            {
                m_WeaponGroupRoot = m_WeaponsManager.WeaponParentSocket.parent;
            }

            if (m_WeaponGroupRoot == null)
            {
                return;
            }

            // If already reparented, nothing to do.
            if (m_WeaponGroupRoot.parent == m_WeaponAnchor)
            {
                return;
            }

            // Preserve the weapon group's local layout (relative to main camera) after moving it under WeaponRigAnchor.
            Vector3 savedLocalPos = m_WeaponGroupRoot.localPosition;
            Quaternion savedLocalRot = m_WeaponGroupRoot.localRotation;

            // Initialize anchor pose to current target pose to avoid a large teleport on the first frame.
            m_WeaponAnchor.SetPositionAndRotation(m_CameraFollowPoint.position, m_PitchPivot.rotation);

            m_WeaponGroupRoot.SetParent(m_WeaponAnchor, false);
            m_WeaponGroupRoot.localPosition = savedLocalPos;
            m_WeaponGroupRoot.localRotation = savedLocalRot;
        }

        void InitializeState()
        {
            if (m_CameraFollowPoint == null)
            {
                return;
            }

            m_OffsetLocal = Vector3.zero;
            m_VerticalOffsetVelocity = 0f;
            m_WasGrounded = m_Controller != null && m_Controller.IsGrounded;
            m_LandingWobbleOffset = 0f;
            m_LandingWobbleVelocity = 0f;
            if (m_RigRoot != null)
            {
                m_RigRoot.position = m_CameraFollowPoint.position;
            }

            m_Initialized = true;
        }

        void UpdateRotation()
        {
            Vector3 playerEuler = m_PlayerTransform.eulerAngles;
            m_YawPivot.rotation = Quaternion.Euler(0f, playerEuler.y, 0f);

            float pitch = m_Controller.CameraVerticalAngle;
            m_PitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void UpdateWeaponAnchor()
        {
            if (!m_SeparateWeaponCamera || m_WeaponAnchor == null || m_CameraFollowPoint == null)
            {
                return;
            }

            // Weapon rig follows target pose directly (no lag, no effects).
            m_WeaponAnchor.SetPositionAndRotation(m_CameraFollowPoint.position, m_PitchPivot.rotation);
        }

        void UpdateFollow(float dt)
        {
            Vector3 targetPos = m_CameraFollowPoint.position;
            Vector3 forward = m_YawPivot.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            else forward.Normalize();
            Vector3 right = m_YawPivot.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            else right.Normalize();
            Vector3 up = Vector3.up;

            Vector3 offsetWorld = m_RigRoot.position - targetPos;
            float forwardComp = Vector3.Dot(offsetWorld, forward);
            float rightComp = Vector3.Dot(offsetWorld, right);
            float upComp = Vector3.Dot(offsetWorld, up);

            float tF = Mathf.Exp(-dt / Mathf.Max(0.001f, m_ForwardDamping));
            float tR = Mathf.Exp(-dt / Mathf.Max(0.001f, m_LateralDamping));
            forwardComp *= tF;
            rightComp *= tR;
            upComp = Mathf.SmoothDamp(upComp, 0f, ref m_VerticalOffsetVelocity, m_VerticalDamping, m_VerticalMaxSpeed, dt);

            Vector3 offsetLocal = new Vector3(rightComp, upComp, forwardComp);
            float len = offsetLocal.magnitude;
            if (len > m_MaxOffsetDistance && len > 0.001f)
            {
                offsetLocal *= m_MaxOffsetDistance / len;
            }

            m_OffsetLocal = offsetLocal;
            Vector3 rigPos = targetPos + (offsetLocal.x * right + offsetLocal.y * up + offsetLocal.z * forward);
            m_RigRoot.position = rigPos + (m_LandingWobbleOffset * Vector3.up);
        }

        void UpdateLandingWobble(float dt)
        {
            bool grounded = m_Controller.IsGrounded;
            if (!m_WasGrounded && grounded)
            {
                m_LandingWobbleOffset = -m_LandingWobbleAmount;
                m_LandingWobbleVelocity = 0f;
            }

            float omega = Mathf.PI * 2f * m_LandingWobbleFrequency;
            float stiffness = omega * omega;
            float dampingCoeff = 2f * m_LandingWobbleDamping * omega;
            float accel = -stiffness * m_LandingWobbleOffset - dampingCoeff * m_LandingWobbleVelocity;
            m_LandingWobbleVelocity += accel * dt;
            m_LandingWobbleOffset += m_LandingWobbleVelocity * dt;
        }

        void UpdateEffectsMoveFactor()
        {
            if (m_CameraEffects == null)
            {
                return;
            }

            float maxSpeed = m_Controller.MaxSpeedOnGround * m_Controller.SprintSpeedModifier;
            float speed = GetHorizontalSpeed();
            float moveFactor = maxSpeed > 0f ? Mathf.Clamp01(speed / maxSpeed) : 0f;
            m_CameraEffects.SetMoveFactor(moveFactor);
        }

        float GetHorizontalSpeed()
        {
            Vector3 velocity = m_Controller.CharacterVelocity;
            velocity.y = 0f;
            return velocity.magnitude;
        }

        public void SetShake(float intensity)
        {
            if (m_CameraEffects != null)
            {
                m_CameraEffects.SetShake(intensity);
            }
        }

        public void AddShake(float intensity, float duration)
        {
            if (m_CameraEffects != null)
            {
                m_CameraEffects.AddShake(intensity, duration);
            }
        }

        public void PlayEnvelopeShake(Object owner, float peakIntensity, float duration, ShakeEnvelopeProfile profile)
        {
            if (m_CameraEffects != null)
            {
                m_CameraEffects.PlayEnvelopeShake(owner, peakIntensity, duration, profile);
            }
        }

        public void StopEnvelopeShake(Object owner)
        {
            if (m_CameraEffects != null)
            {
                m_CameraEffects.StopEnvelopeShake(owner);
            }
        }
    }
}

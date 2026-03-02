using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;

namespace Unity.FPS.Roguelike.Level
{
    public class SegmentDoorGate : MonoBehaviour
    {
        [Header("Colliders")]
        [SerializeField] private Collider[] m_BlockingColliders;
        [SerializeField] private Collider m_TriggerCollider;

        [Header("Door Transforms")]
        [SerializeField] private Transform m_LeftDoor;
        [SerializeField] private Transform m_RightDoor;
        [Tooltip("每扇门绕铰链轴旋转的角度（度）")]
        [SerializeField] private float m_OpenAngle = 130f;
        [Tooltip("绕哪根轴旋转（本地），一般 Y=门铰链")]
        [SerializeField] private Vector3 m_RotationAxis = Vector3.up;

        [Header("Open Animation")]
        [SerializeField] private float m_ShakeDuration = 0.12f;
        [SerializeField] private float m_ShakeStrength = 4f;
        [SerializeField] private int m_ShakeVibrato = 15;
        [SerializeField] private float m_OpenDuration = 0.18f;
        [SerializeField] private Ease m_OpenEase = Ease.OutBack;
        [SerializeField] private float m_OpenJellyPunch = 6f;
        [SerializeField] private float m_OpenJellyDuration = 0.45f;
        [SerializeField] private int m_OpenJellyVibrato = 6;
        [SerializeField] private float m_OpenJellyElasticity = 0.4f;
        [SerializeField] private float m_OpenCameraShakeIntensity = 0.12f;
        [SerializeField] private float m_OpenCameraShakeDuration = 0.35f;
        [Tooltip("开门期间相机震动形态（曲线由相机系统统一管理）")]
        [SerializeField] private ShakeEnvelopeProfile m_OpenCameraShakeProfile = ShakeEnvelopeProfile.Normal;

        [Header("Close Animation")]
        [SerializeField] private float m_CloseDuration = 0.3f;
        [SerializeField] private Ease m_CloseEase = Ease.InOutSine;
        [SerializeField] private float m_CloseJellyPunch = 5f;
        [SerializeField] private float m_CloseJellyDuration = 0.35f;
        [SerializeField] private int m_CloseJellyVibrato = 5;
        [SerializeField] private float m_CloseJellyElasticity = 0.35f;
        [SerializeField] private SfxKey m_OpenSfxKey = SfxKey.DoorOpen;
        [SerializeField] private SfxKey m_CloseSfxKey = SfxKey.DoorClose;

        [Header("Open State Visual")]
        [Tooltip("门已打开时显示的预制体上的 Renderer，开门时会变为亮色")]
        [SerializeField] private Renderer[] m_OpenStateRenderers;
        [Tooltip("门关闭时指示物颜色（暗）")]
        [SerializeField] private Color m_ClosedColor = new Color(0.45f, 0.45f, 0.45f, 1f);
        [Tooltip("门打开时指示物颜色（亮）")]
        [SerializeField] private Color m_OpenColor = new Color(1f, 0.95f, 0.7f, 1f);
        [Tooltip("Shader 主色属性名，URP 一般为 _BaseColor，Built-in 一般为 _Color")]
        [SerializeField] private string m_ColorPropertyName = "_BaseColor";

        public event Action<SegmentDoorGate> OnOpened;
        public event Action<SegmentDoorGate> OnPlayerEnterGate;
        public event Action<SegmentDoorGate> OnPlayerExitGate;

        public bool IsOpen { get; private set; }
        public Collider TriggerCollider => m_TriggerCollider;

        /// <summary>
        /// 无动画地设置为开启/关闭状态（用于需要初始状态不播动画的场景）。
        /// </summary>
        public void SetOpenStateImmediate(bool open)
        {
            KillDoorTweens();
            IsOpen = open;
            SetCollidersEnabled(!open);
            ApplyDoorVisualState();
        }

        private Vector3 m_LeftDoorClosedEuler;
        private Vector3 m_RightDoorClosedEuler;
        private Sequence m_OpenSequence;
        private Sequence m_CloseSequence;
        private MaterialPropertyBlock m_ColorBlock;
        private int m_ColorPropertyId;
        private FpsCameraEffects m_OpenCameraShakeEffects;

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            KillDoorTweens();
            IsOpen = true;
            SetCollidersEnabled(false);
            ApplyOpenStateIndicatorColor();
            if (m_OpenSfxKey != SfxKey.None)
            {
                AudioUtility.PlaySfx(m_OpenSfxKey, transform.position);
            }
            OnOpened?.Invoke(this);

            if (!HasValidDoors())
            {
                float fallbackDuration = m_ShakeDuration + m_OpenDuration + m_OpenJellyDuration;
                TriggerOpenCameraShake(fallbackDuration);
                return;
            }

            m_LeftDoor.localEulerAngles = m_LeftDoorClosedEuler;
            m_RightDoor.localEulerAngles = m_RightDoorClosedEuler;

            float leftOpenAngle = -m_OpenAngle;
            float rightOpenAngle = m_OpenAngle;
            Vector3 leftOpenEuler = m_LeftDoorClosedEuler + m_RotationAxis * leftOpenAngle;
            Vector3 rightOpenEuler = m_RightDoorClosedEuler + m_RotationAxis * rightOpenAngle;

            m_OpenSequence = DOTween.Sequence();
            m_OpenSequence.SetTarget(this);
            m_OpenSequence.SetUpdate(UpdateType.Normal);

            m_OpenSequence.Append(m_LeftDoor.DOShakeRotation(m_ShakeDuration, m_ShakeStrength, m_ShakeVibrato, 90f, true, ShakeRandomnessMode.Harmonic));
            m_OpenSequence.Join(m_RightDoor.DOShakeRotation(m_ShakeDuration, m_ShakeStrength, m_ShakeVibrato, 90f, true, ShakeRandomnessMode.Harmonic));

            m_OpenSequence.Append(m_LeftDoor.DOLocalRotate(leftOpenEuler, m_OpenDuration, RotateMode.Fast).SetEase(m_OpenEase));
            m_OpenSequence.Join(m_RightDoor.DOLocalRotate(rightOpenEuler, m_OpenDuration, RotateMode.Fast).SetEase(m_OpenEase));

            Vector3 jellyPunch = m_RotationAxis * m_OpenJellyPunch;
            m_OpenSequence.Append(m_LeftDoor.DOPunchRotation(jellyPunch, m_OpenJellyDuration, m_OpenJellyVibrato, m_OpenJellyElasticity));
            m_OpenSequence.Join(m_RightDoor.DOPunchRotation(jellyPunch, m_OpenJellyDuration, m_OpenJellyVibrato, m_OpenJellyElasticity));

            TriggerOpenCameraShake(m_OpenSequence.Duration());
            m_OpenSequence.OnComplete(StopOpenCameraShake);
            m_OpenSequence.OnKill(StopOpenCameraShake);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            KillDoorTweens();
            IsOpen = false;
            ApplyOpenStateIndicatorColor();
            // 触发关门时立即启用碰撞，防止玩家触发关门后又退出门外导致进不去关卡
            SetCollidersEnabled(true);
            if (m_CloseSfxKey != SfxKey.None)
            {
                AudioUtility.PlaySfx(m_CloseSfxKey, transform.position);
            }

            if (!HasValidDoors())
            {
                return;
            }

            m_CloseSequence = DOTween.Sequence();
            m_CloseSequence.SetTarget(this);
            m_CloseSequence.SetUpdate(UpdateType.Normal);

            m_CloseSequence.Append(m_LeftDoor.DOLocalRotate(m_LeftDoorClosedEuler, m_CloseDuration, RotateMode.Fast).SetEase(m_CloseEase));
            m_CloseSequence.Join(m_RightDoor.DOLocalRotate(m_RightDoorClosedEuler, m_CloseDuration, RotateMode.Fast).SetEase(m_CloseEase));

            Vector3 jellyPunch = m_RotationAxis * m_CloseJellyPunch;
            m_CloseSequence.Append(m_LeftDoor.DOPunchRotation(jellyPunch, m_CloseJellyDuration, m_CloseJellyVibrato, m_CloseJellyElasticity));
            m_CloseSequence.Join(m_RightDoor.DOPunchRotation(jellyPunch, m_CloseJellyDuration, m_CloseJellyVibrato, m_CloseJellyElasticity));
        }

        /// <summary>
        /// 由 SegmentDoorGateTriggerRelay 在触发器内有物体进入时调用。
        /// </summary>
        public void HandleTriggerEnter(Collider other)
        {
            if (other != null && other.CompareTag("Player"))
            {
                OnPlayerEnterGate?.Invoke(this);
            }
        }

        /// <summary>
        /// 由 SegmentDoorGateTriggerRelay 在触发器内有物体离开时调用。
        /// </summary>
        public void HandleTriggerExit(Collider other)
        {
            if (other != null && other.CompareTag("Player"))
            {
                OnPlayerExitGate?.Invoke(this);
            }
        }

        private bool HasValidDoors()
        {
            return m_LeftDoor != null && m_RightDoor != null;
        }

        private void KillDoorTweens()
        {
            m_OpenSequence?.Kill();
            m_OpenSequence = null;
            m_CloseSequence?.Kill();
            m_CloseSequence = null;
            m_LeftDoor?.DOKill();
            m_RightDoor?.DOKill();
            StopOpenCameraShake();
        }

        private void Awake()
        {
            if (HasValidDoors())
            {
                m_LeftDoorClosedEuler = m_LeftDoor.localEulerAngles;
                m_RightDoorClosedEuler = m_RightDoor.localEulerAngles;
            }

            if (m_BlockingColliders == null || m_BlockingColliders.Length == 0)
            {
                var allColliders = GetComponentsInChildren<Collider>(true);
                if (allColliders != null && allColliders.Length > 0)
                {
                    var blocking = new List<Collider>(allColliders.Length);
                    for (int i = 0; i < allColliders.Length; i++)
                    {
                        var c = allColliders[i];
                        if (c != null && !c.isTrigger)
                        {
                            blocking.Add(c);
                        }
                    }
                    m_BlockingColliders = blocking.ToArray();
                }
            }

            if (m_TriggerCollider == null)
            {
                var allColliders = GetComponentsInChildren<Collider>(true);
                if (allColliders != null)
                {
                    for (int i = 0; i < allColliders.Length; i++)
                    {
                        var c = allColliders[i];
                        if (c != null && c.isTrigger)
                        {
                            m_TriggerCollider = c;
                            break;
                        }
                    }
                }
            }

            SyncOpenStateFromBlockingColliders();
            ApplyDoorVisualState();
            EnsureTriggerRelay();
        }

        private void OnDisable()
        {
            KillDoorTweens();
        }

        private void ApplyDoorVisualState()
        {
            if (HasValidDoors())
            {
                if (IsOpen)
                {
                    m_LeftDoor.localEulerAngles = m_LeftDoorClosedEuler + m_RotationAxis * (-m_OpenAngle);
                    m_RightDoor.localEulerAngles = m_RightDoorClosedEuler + m_RotationAxis * m_OpenAngle;
                }
                else
                {
                    m_LeftDoor.localEulerAngles = m_LeftDoorClosedEuler;
                    m_RightDoor.localEulerAngles = m_RightDoorClosedEuler;
                }
            }

            ApplyOpenStateIndicatorColor();
        }

        private void ApplyOpenStateIndicatorColor()
        {
            if (m_OpenStateRenderers == null || m_OpenStateRenderers.Length == 0)
            {
                return;
            }

            if (m_ColorBlock == null)
            {
                m_ColorBlock = new MaterialPropertyBlock();
                m_ColorPropertyId = Shader.PropertyToID(m_ColorPropertyName);
            }

            Color color = IsOpen ? m_OpenColor : m_ClosedColor;
            m_ColorBlock.SetColor(m_ColorPropertyId, color);

            for (int i = 0; i < m_OpenStateRenderers.Length; i++)
            {
                var r = m_OpenStateRenderers[i];
                if (r != null)
                {
                    r.SetPropertyBlock(m_ColorBlock);
                }
            }
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (m_BlockingColliders == null)
            {
                return;
            }

            for (int i = 0; i < m_BlockingColliders.Length; i++)
            {
                var blockingCollider = m_BlockingColliders[i];
                if (blockingCollider != null)
                {
                    blockingCollider.enabled = enabled;
                }
            }
        }

        private void SyncOpenStateFromBlockingColliders()
        {
            if (m_BlockingColliders == null || m_BlockingColliders.Length == 0)
            {
                IsOpen = true;
                return;
            }

            for (int i = 0; i < m_BlockingColliders.Length; i++)
            {
                var c = m_BlockingColliders[i];
                if (c != null && c.enabled)
                {
                    IsOpen = false;
                    return;
                }
            }
            IsOpen = true;
        }

        private void EnsureTriggerRelay()
        {
            if (m_TriggerCollider == null)
            {
                return;
            }

            var relay = m_TriggerCollider.GetComponent<SegmentDoorGateTriggerRelay>();
            if (relay == null)
            {
                relay = m_TriggerCollider.gameObject.AddComponent<SegmentDoorGateTriggerRelay>();
            }
            relay.Bind(this);
        }

        private void TriggerOpenCameraShake(float openAnimationDuration)
        {
            var actorsManager = FindObjectOfType<ActorsManager>();
            var player = actorsManager != null ? actorsManager.Player : null;
            if (player == null)
            {
                return;
            }

            var cameraRig = player.GetComponent<FpsCameraRig>();
            if (cameraRig == null)
            {
                return;
            }

            float duration = openAnimationDuration > 0.001f ? openAnimationDuration : Mathf.Max(m_OpenCameraShakeDuration, 0f);

            if (duration <= 0.001f || m_OpenCameraShakeIntensity <= 0f)
            {
                return;
            }

            StopOpenCameraShake();
            m_OpenCameraShakeEffects = cameraRig.CameraEffects;
            if (m_OpenCameraShakeEffects == null)
            {
                return;
            }

            m_OpenCameraShakeEffects.PlayEnvelopeShake(this, m_OpenCameraShakeIntensity, duration, m_OpenCameraShakeProfile);
        }

        private void StopOpenCameraShake()
        {
            if (m_OpenCameraShakeEffects == null)
            {
                return;
            }

            m_OpenCameraShakeEffects.StopEnvelopeShake(this);
            m_OpenCameraShakeEffects = null;
        }
    }
}

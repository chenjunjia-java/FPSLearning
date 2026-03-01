using System;
using UnityEngine;
using DG.Tweening;

namespace Unity.FPS.Roguelike.Level
{
    /// <summary>
    /// 入口门机关：默认弹起，玩家踩下后压下并触发“开门请求”；踩下后保持压下，玩家离开也不会弹起。
    /// 建议挂载在同一物体上：一个非 Trigger 的 Collider（承重、防穿模），一个 isTrigger 的 Collider（仅用于检测踩踏）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntranceDoorPressurePlate : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SegmentDoorGate m_TargetEntranceDoor;
        [SerializeField] private Transform m_PlateVisual;

        [Header("Press State")]
        [SerializeField] private Vector3 m_PressDirectionLocal = Vector3.down;
        [SerializeField] [Min(0f)] private float m_PressDistance = 0.08f;
        [SerializeField] [Min(0f)] private float m_PressDuration = 0.2f;
        [SerializeField] [Min(0f)] private float m_ReleaseDuration = 0.15f;
        [SerializeField] private bool m_OneShot = true;
        [SerializeField] private string m_PlayerTag = "Player";

        [Header("Breathing Color")]
        [SerializeField] [Tooltip("不填则从 Plate Visual 上取 Renderer")]
        private Renderer m_PlateRenderer;
        [SerializeField] [Tooltip("URP 常用 _BaseColor，Built-in 常用 _Color")]
        private string m_ColorPropertyName = "_BaseColor";
        [SerializeField] private Color m_BreathColorMin = new Color(0.4f, 0.4f, 0.5f);
        [SerializeField] private Color m_BreathColorMax = new Color(0.7f, 0.7f, 1f);
        [SerializeField] [Min(0.01f)] private float m_BreathDuration = 1.5f;
        [SerializeField] private Color m_PressedColor = new Color(0.35f, 0.35f, 0.35f);

        public event Action<EntranceDoorPressurePlate> OnPressed;
        public bool IsPressed { get; private set; }

        private Vector3 m_VisualRaisedLocalPosition;
        private Vector3 m_VisualPressedLocalPosition;
        private Tween m_PlateTween;
        private Material m_PlateMaterial;
        private Tween m_BreathTween;

        private void Awake()
        {
            if (m_PlateVisual == null)
            {
                m_PlateVisual = transform;
            }

            m_VisualRaisedLocalPosition = m_PlateVisual.localPosition;
            Vector3 direction = m_PressDirectionLocal.sqrMagnitude > 0f
                ? m_PressDirectionLocal.normalized
                : Vector3.down;
            m_VisualPressedLocalPosition = m_VisualRaisedLocalPosition + direction * m_PressDistance;

            if (m_PlateRenderer == null && m_PlateVisual != null)
            {
                m_PlateVisual.TryGetComponent(out m_PlateRenderer);
            }
            if (m_PlateRenderer != null)
            {
                m_PlateMaterial = m_PlateRenderer.material;
            }

            ApplyVisualStateImmediate();
        }

        private void OnEnable()
        {
            if (m_PlateMaterial != null && !IsPressed)
            {
                StartBreathing();
            }
        }

        private void OnDisable()
        {
            m_PlateTween?.Kill();
            m_PlateTween = null;
            m_BreathTween?.Kill();
            m_BreathTween = null;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || !other.CompareTag(m_PlayerTag))
            {
                return;
            }

            if (IsPressed && m_OneShot)
            {
                return;
            }

            Press();
        }

        public void Press()
        {
            if (IsPressed && m_OneShot)
            {
                return;
            }

            IsPressed = true;
            m_BreathTween?.Kill();
            m_BreathTween = null;
            ApplyPressedColor();
            TweenToPressed();
            OnPressed?.Invoke(this);

            if (m_TargetEntranceDoor != null)
            {
                m_TargetEntranceDoor.Open();
            }
        }

        private void StartBreathing()
        {
            if (m_PlateMaterial == null || !m_PlateMaterial.HasProperty(m_ColorPropertyName))
            {
                return;
            }

            m_BreathTween?.Kill();
            m_PlateMaterial.SetColor(m_ColorPropertyName, m_BreathColorMin);
            m_BreathTween = m_PlateMaterial
                .DOColor(m_BreathColorMax, m_BreathDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(UpdateType.Normal)
                .SetTarget(this);
        }

        private void ApplyPressedColor()
        {
            if (m_PlateMaterial != null && m_PlateMaterial.HasProperty(m_ColorPropertyName))
            {
                m_PlateMaterial.SetColor(m_ColorPropertyName, m_PressedColor);
            }
        }

        private void TweenToPressed()
        {
            if (m_PlateVisual == null)
            {
                return;
            }

            m_PlateTween?.Kill();
            m_PlateTween = m_PlateVisual
                .DOLocalMove(m_VisualPressedLocalPosition, m_PressDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(UpdateType.Normal)
                .SetTarget(this);
        }

        private void TweenToRaised()
        {
            if (m_PlateVisual == null)
            {
                return;
            }

            m_PlateTween?.Kill();
            m_PlateTween = m_PlateVisual
                .DOLocalMove(m_VisualRaisedLocalPosition, m_ReleaseDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(UpdateType.Normal)
                .SetTarget(this);
        }

        private void ApplyVisualStateImmediate()
        {
            if (m_PlateVisual == null)
            {
                return;
            }

            m_PlateVisual.localPosition = IsPressed ? m_VisualPressedLocalPosition : m_VisualRaisedLocalPosition;
        }
    }
}

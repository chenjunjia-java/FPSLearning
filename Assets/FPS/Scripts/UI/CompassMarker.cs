using Unity.FPS.AI;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class CompassMarker : MonoBehaviour
    {
        [Tooltip("Main marker image")] public Image MainImage;

        [Tooltip("Canvas group for the marker")]
        public CanvasGroup CanvasGroup;

        [Header("Enemy element")] [Tooltip("Default color for the marker")]
        public Color DefaultColor;

        [Tooltip("Alternative color for the marker")]
        public Color AltColor;

        [Header("Direction element")] [Tooltip("Use this marker as a magnetic direction")]
        public bool IsDirection;

        [Tooltip("Text content for the direction")]
        public TMPro.TextMeshProUGUI TextContent;

        EnemyController m_EnemyController;

        public void Initialize(CompassElement compassElement, string textDirection)
        {
            if (IsDirection && TextContent)
            {
                TextContent.text = textDirection;
            }
            else
            {
                m_EnemyController = compassElement.transform.GetComponent<EnemyController>();
                if (m_EnemyController != null)
                    SubscribeEnemy(m_EnemyController);
            }
        }

        /// <summary>
        /// 仅用于指南针从 EnemyManager 自动注册的敌人（肉鸽等），无需 CompassElement。
        /// </summary>
        public void InitializeWithEnemy(EnemyController enemy)
        {
            if (enemy == null)
                return;
            m_EnemyController = enemy;
            SubscribeEnemy(m_EnemyController);
        }

        void SubscribeEnemy(EnemyController enemy)
        {
            enemy.onDetectedTarget += DetectTarget;
            enemy.onLostTarget += LostTarget;
            LostTarget();
        }

        void OnDestroy()
        {
            if (m_EnemyController != null)
            {
                m_EnemyController.onDetectedTarget -= DetectTarget;
                m_EnemyController.onLostTarget -= LostTarget;
                m_EnemyController = null;
            }
        }

        public void DetectTarget()
        {
            MainImage.color = AltColor;
        }

        public void LostTarget()
        {
            MainImage.color = DefaultColor;
        }
    }
}
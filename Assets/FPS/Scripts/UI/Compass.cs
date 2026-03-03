using System.Collections.Generic;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.UI
{
    public class Compass : MonoBehaviour
    {
        public RectTransform CompasRect;
        public float VisibilityAngle = 180f;
        public float HeightDifferenceMultiplier = 2f;
        public float MinScale = 0.5f;
        public float DistanceMinScale = 50f;
        public float CompasMarginRatio = 0.8f;

        public GameObject MarkerDirectionPrefab;

        [Header("Roguelike: auto enemy markers")]
        [Tooltip("Prefab for enemy markers on compass. If set, enemies from EnemyManager will be shown automatically.")]
        [SerializeField] CompassMarker m_EnemyMarkerPrefab;

        Transform m_PlayerTransform;
        Dictionary<Transform, CompassMarker> m_ElementsDictionnary = new Dictionary<Transform, CompassMarker>();
        HashSet<Transform> m_EnemyTransforms = new HashSet<Transform>();
        List<Transform> m_ToRemoveBuffer = new List<Transform>();
        EnemyManager m_EnemyManager;

        float m_WidthMultiplier;
        float m_HeightOffset;

        void Awake()
        {
            PlayerCharacterController playerCharacterController = FindObjectOfType<PlayerCharacterController>();
            DebugUtility.HandleErrorIfNullFindObject<PlayerCharacterController, Compass>(playerCharacterController,
                this);
            m_PlayerTransform = playerCharacterController.transform;

            m_WidthMultiplier = CompasRect.rect.width / VisibilityAngle;
            m_HeightOffset = -CompasRect.rect.height / 2;
        }

        void OnEnable()
        {
            m_EnemyManager = FindObjectOfType<EnemyManager>();
        }

        void Update()
        {
            SyncEnemyMarkers();

            foreach (var element in m_ElementsDictionnary)
            {
                float distanceRatio = 1;
                float heightDifference = 0;
                float angle;

                if (element.Value.IsDirection)
                {
                    angle = Vector3.SignedAngle(m_PlayerTransform.forward,
                        element.Key.transform.localPosition.normalized, Vector3.up);
                }
                else
                {
                    Vector3 targetDir = (element.Key.transform.position - m_PlayerTransform.position).normalized;
                    targetDir = Vector3.ProjectOnPlane(targetDir, Vector3.up);
                    Vector3 playerForward = Vector3.ProjectOnPlane(m_PlayerTransform.forward, Vector3.up);
                    angle = Vector3.SignedAngle(playerForward, targetDir, Vector3.up);

                    Vector3 directionVector = element.Key.transform.position - m_PlayerTransform.position;

                    heightDifference = (directionVector.y) * HeightDifferenceMultiplier;
                    heightDifference = Mathf.Clamp(heightDifference, -CompasRect.rect.height / 2 * CompasMarginRatio,
                        CompasRect.rect.height / 2 * CompasMarginRatio);

                    distanceRatio = directionVector.magnitude / DistanceMinScale;
                    distanceRatio = Mathf.Clamp01(distanceRatio);
                }

                if (angle > -VisibilityAngle / 2 && angle < VisibilityAngle / 2)
                {
                    element.Value.CanvasGroup.alpha = 1;
                    element.Value.CanvasGroup.transform.localPosition = new Vector2(m_WidthMultiplier * angle,
                        heightDifference + m_HeightOffset);
                    element.Value.CanvasGroup.transform.localScale =
                        Vector3.one * Mathf.Lerp(1, MinScale, distanceRatio);
                }
                else
                {
                    element.Value.CanvasGroup.alpha = 0;
                }
            }
        }

        public void RegisterCompassElement(Transform element, CompassMarker marker)
        {
            // 防止同一 Transform 重复注册（如跨关卡时多次初始化）
            if (m_ElementsDictionnary.ContainsKey(element))
                UnregisterCompassElement(element);
            // 主角只保留一个点：若当前注册的是玩家或其子物体，先移除所有已有的“玩家”相关标记（避免每关新建子物体导致多个主角点）
            if (m_PlayerTransform != null && (element == m_PlayerTransform || element.IsChildOf(m_PlayerTransform)))
                RemoveExistingPlayerMarkers();

            marker.transform.SetParent(CompasRect);
            m_ElementsDictionnary.Add(element, marker);
        }

        void RemoveExistingPlayerMarkers()
        {
            m_ToRemoveBuffer.Clear();
            foreach (var kv in m_ElementsDictionnary)
            {
                if (kv.Key == m_PlayerTransform || kv.Key.IsChildOf(m_PlayerTransform))
                    m_ToRemoveBuffer.Add(kv.Key);
            }
            for (int i = 0; i < m_ToRemoveBuffer.Count; i++)
                UnregisterCompassElement(m_ToRemoveBuffer[i]);
        }

        public void UnregisterCompassElement(Transform element)
        {
            if (m_ElementsDictionnary.TryGetValue(element, out CompassMarker marker) && marker.CanvasGroup != null)
                Destroy(marker.CanvasGroup.gameObject);
            m_ElementsDictionnary.Remove(element);
            m_EnemyTransforms.Remove(element);
        }

        void SyncEnemyMarkers()
        {
            if (m_EnemyMarkerPrefab == null || m_EnemyManager == null || m_EnemyManager.Enemies == null)
                return;

            List<EnemyController> enemies = m_EnemyManager.Enemies;

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyController ec = enemies[i];
                if (ec == null)
                    continue;
                Transform t = ec.transform;
                // Only show enemies that explicitly opt-in with a CompassElement on the root.
                if (!t.TryGetComponent<CompassElement>(out _))
                    continue;
                if (m_EnemyTransforms.Contains(t))
                    continue;
                if (m_ElementsDictionnary.ContainsKey(t))
                    continue;

                CompassMarker marker = Instantiate(m_EnemyMarkerPrefab);
                marker.InitializeWithEnemy(ec);
                RegisterCompassElement(t, marker);
                m_EnemyTransforms.Add(t);
            }

            m_ToRemoveBuffer.Clear();
            foreach (Transform tr in m_EnemyTransforms)
            {
                if (tr == null)
                {
                    m_ToRemoveBuffer.Add(tr);
                    continue;
                }
                bool stillEnemy = false;
                bool stillHasCompassElement = false;
                for (int j = 0; j < m_EnemyManager.Enemies.Count; j++)
                {
                    if (m_EnemyManager.Enemies[j] != null && m_EnemyManager.Enemies[j].transform == tr)
                    {
                        stillEnemy = true;
                        stillHasCompassElement = tr.TryGetComponent<CompassElement>(out _);
                        break;
                    }
                }
                if (!stillEnemy || !stillHasCompassElement)
                    m_ToRemoveBuffer.Add(tr);
            }

            for (int k = 0; k < m_ToRemoveBuffer.Count; k++)
            {
                UnregisterCompassElement(m_ToRemoveBuffer[k]);
            }
        }
    }
}
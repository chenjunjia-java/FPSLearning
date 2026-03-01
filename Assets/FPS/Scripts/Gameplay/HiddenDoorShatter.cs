using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Game
{
    public class HiddenDoorShatter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] Health m_Health;
        [SerializeField] Transform m_ChunksRoot;
        [SerializeField] Rigidbody[] m_Chunks;
        [SerializeField] Collider[] m_DisableCollidersOnShatter;

        [Header("Behavior")]
        [SerializeField] bool m_AutoFindChunks = true;
        [SerializeField] bool m_DetachChunksOnShatter = true;

        [Header("Forces")]
        [SerializeField] float m_ForwardImpulse = 12f;
        [SerializeField] float m_RandomImpulse = 3f;
        [SerializeField] float m_TorqueImpulse = 2f;

        bool m_Shattered;

        void Awake()
        {
            if (m_Health == null)
            {
                m_Health = GetComponent<Health>();
                if (m_Health == null)
                {
                    m_Health = GetComponentInParent<Health>();
                }
            }

            if (m_AutoFindChunks)
            {
                CacheChunks();
            }

            SetChunksKinematic(true);
        }

        void OnEnable()
        {
            if (m_Health != null)
            {
                m_Health.OnDie += OnDie;
            }
        }

        void OnDisable()
        {
            if (m_Health != null)
            {
                m_Health.OnDie -= OnDie;
            }
        }

        void CacheChunks()
        {
            Transform root = m_ChunksRoot != null ? m_ChunksRoot : transform;
            Rigidbody[] all = root.GetComponentsInChildren<Rigidbody>(true);
            if (all == null || all.Length == 0)
            {
                m_Chunks = new Rigidbody[0];
                return;
            }

            var list = new List<Rigidbody>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                Rigidbody rb = all[i];
                if (rb == null)
                {
                    continue;
                }

                if (rb.transform == transform)
                {
                    continue;
                }

                list.Add(rb);
            }

            m_Chunks = list.ToArray();
        }

        void SetChunksKinematic(bool isKinematic)
        {
            if (m_Chunks == null)
            {
                return;
            }

            for (int i = 0; i < m_Chunks.Length; i++)
            {
                Rigidbody rb = m_Chunks[i];
                if (rb == null)
                {
                    continue;
                }

                rb.isKinematic = isKinematic;

                if (isKinematic)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.Sleep();
                }
                else
                {
                    rb.WakeUp();
                }
            }
        }

        void OnDie()
        {
            Shatter();
        }

        public void Shatter()
        {
            if (m_Shattered)
            {
                return;
            }
            m_Shattered = true;

            for (int i = 0; i < m_DisableCollidersOnShatter.Length; i++)
            {
                Collider c = m_DisableCollidersOnShatter[i];
                if (c != null)
                {
                    c.enabled = false;
                }
            }

            Vector3 direction = transform.forward;
            Vector3 hitPoint = transform.position;

            if (m_Health != null)
            {
                LastHitInfo hitInfo = m_Health.GetComponent<LastHitInfo>();
                if (hitInfo != null && hitInfo.HasValue)
                {
                    direction = hitInfo.LastDirection;
                    hitPoint = hitInfo.LastPoint;
                }
            }

            SetChunksKinematic(false);

            if (m_Chunks == null)
            {
                return;
            }

            for (int i = 0; i < m_Chunks.Length; i++)
            {
                Rigidbody rb = m_Chunks[i];
                if (rb == null)
                {
                    continue;
                }

                if (m_DetachChunksOnShatter)
                {
                    rb.transform.SetParent(null, true);
                }

                Vector3 random = m_RandomImpulse > 0f ? UnityEngine.Random.insideUnitSphere * m_RandomImpulse : Vector3.zero;
                rb.AddForce(direction * m_ForwardImpulse + random, ForceMode.Impulse);

                if (m_TorqueImpulse > 0f)
                {
                    rb.AddTorque(UnityEngine.Random.insideUnitSphere * m_TorqueImpulse, ForceMode.Impulse);
                }
            }
        }
    }
}


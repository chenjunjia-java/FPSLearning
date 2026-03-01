using Unity.FPS.Game;
using Unity.FPS.GameFramework;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public class Pickup : MonoBehaviour, IPoolable
    {
        [Tooltip("Frequency at which the item will move up and down")]
        public float VerticalBobFrequency = 1f;

        [Tooltip("Distance the item will move up and down")]
        public float BobbingAmount = 1f;

        [Tooltip("Rotation angle per second")] public float RotatingSpeed = 360f;

        [Tooltip("Sound played on pickup")] public AudioClip PickupSfx;
        [Tooltip("VFX spawned on pickup")] public GameObject PickupVfxPrefab;

        public Rigidbody PickupRigidbody { get; private set; }

        Collider m_Collider;
        Vector3 m_StartPosition;
        bool m_HasPlayedFeedback;

        protected virtual void Start()
        {
            PickupRigidbody = GetComponent<Rigidbody>();
            DebugUtility.HandleErrorIfNullGetComponent<Rigidbody, Pickup>(PickupRigidbody, this, gameObject);
            m_Collider = GetComponent<Collider>();
            DebugUtility.HandleErrorIfNullGetComponent<Collider, Pickup>(m_Collider, this, gameObject);

            // ensure the physics setup is a kinematic rigidbody trigger
            PickupRigidbody.isKinematic = true;
            m_Collider.isTrigger = true;

            // Remember start position for animation
            m_StartPosition = transform.position;
        }

        public void OnSpawned()
        {
            m_HasPlayedFeedback = false;
            m_StartPosition = transform.position;
        }

        public void OnDespawned()
        {
        }

        void Update()
        {
            // Handle bobbing
            float bobbingAnimationPhase = ((Mathf.Sin(Time.time * VerticalBobFrequency) * 0.5f) + 0.5f) * BobbingAmount;
            transform.position = m_StartPosition + Vector3.up * bobbingAnimationPhase;

            // Handle rotating
            transform.Rotate(Vector3.up, RotatingSpeed * Time.deltaTime, Space.Self);
        }

        void OnTriggerEnter(Collider other)
        {
            PlayerCharacterController pickingPlayer = other.GetComponent<PlayerCharacterController>();

            if (pickingPlayer != null)
            {
                OnPicked(pickingPlayer);

                PickupEvent evt = Events.PickupEvent;
                evt.Pickup = gameObject;
                EventManager.Broadcast(evt);
            }
        }

        protected virtual void OnPicked(PlayerCharacterController playerController)
        {
            PlayPickupFeedback();
        }

        public void PlayPickupFeedback()
        {
            if (m_HasPlayedFeedback)
                return;

            if (PickupSfx)
            {
                AudioUtility.CreateSFX(PickupSfx, transform.position, AudioUtility.AudioGroups.Pickup, 0f);
            }

            if (PickupVfxPrefab)
            {
                if (ObjPrefabManager.Instance != null)
                {
                    GameObject pickupVfxInstance = ObjPrefabManager.Instance
                        .Spawn(PickupVfxPrefab.transform, transform.position, Quaternion.identity)
                        .gameObject;

                    TimedSelfDestruct timedSelfDestruct = pickupVfxInstance.GetComponent<TimedSelfDestruct>();
                    if (timedSelfDestruct != null)
                    {
                        timedSelfDestruct.ResetLifetime(1f);
                    }
                }
                else
                {
                    Instantiate(PickupVfxPrefab, transform.position, Quaternion.identity);
                }
            }

            m_HasPlayedFeedback = true;
        }

        protected void DespawnOrDestroy()
        {
            PooledInstance pooledInstance = GetComponent<PooledInstance>();
            if (pooledInstance != null)
            {
                pooledInstance.Despawn();
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
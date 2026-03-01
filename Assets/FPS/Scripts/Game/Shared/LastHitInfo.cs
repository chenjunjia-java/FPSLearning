using UnityEngine;

namespace Unity.FPS.Game
{
    public class LastHitInfo : MonoBehaviour
    {
        public Vector3 LastDirection { get; private set; }
        public Vector3 LastPoint { get; private set; }
        public float LastTime { get; private set; }
        public bool HasValue { get; private set; }

        public void Set(Vector3 direction, Vector3 point)
        {
            if (direction.sqrMagnitude > 0.0001f)
            {
                LastDirection = direction.normalized;
            }
            else
            {
                LastDirection = Vector3.forward;
            }

            LastPoint = point;
            LastTime = Time.time;
            HasValue = true;
        }

        public void ResetInfo()
        {
            HasValue = false;
            LastDirection = Vector3.forward;
            LastPoint = Vector3.zero;
            LastTime = 0f;
        }
    }
}

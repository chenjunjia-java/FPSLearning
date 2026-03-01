using UnityEngine;

namespace Unity.FPS.Roguelike.Waves
{
    [DisallowMultipleComponent]
    public sealed class WaveEnemyTag : MonoBehaviour
    {
        public int WaveId { get; private set; } = -1;

        public void SetWaveId(int waveId)
        {
            WaveId = waveId;
        }
    }
}


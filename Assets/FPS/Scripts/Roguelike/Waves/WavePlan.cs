using System;
using UnityEngine;

namespace Unity.FPS.Roguelike.Waves
{
    [Serializable]
    public struct Wave
    {
        [Min(0)] public int Count;
        [Min(0f)] public float SpawnInterval;
    }

    [Serializable]
    public struct WavePlan
    {
        public Wave[] Waves;
    }
}


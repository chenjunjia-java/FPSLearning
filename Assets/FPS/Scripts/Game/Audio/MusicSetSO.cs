using UnityEngine;

namespace Unity.FPS.Game
{
    [CreateAssetMenu(fileName = "MusicSet", menuName = "FPS/Audio/Music Set")]
    public class MusicSetSO : ScriptableObject
    {
        [Header("Clips")]
        public AudioClip Wind;
        public AudioClip Noise;
        public AudioClip Environment;
        public AudioClip Main;

        [Header("Layer Volume")]
        [Range(0f, 1f)] public float WindVolume = 1f;
        [Range(0f, 1f)] public float NoiseVolume = 1f;
        [Range(0f, 1f)] public float EnvironmentVolume = 1f;
        [Range(0f, 1f)] public float MainVolume = 1f;

        [Header("Playback")]
        public bool Loop = true;
    }
}

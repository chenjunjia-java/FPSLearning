using UnityEngine;

namespace Unity.FPS.Game
{
    public static class SfxKey
    {
        public static int Hash(string key) => Animator.StringToHash(key);
    }
}


using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Game
{
    public class ActorsManager : MonoBehaviour
    {
        public List<Actor> Actors { get; private set; } = new List<Actor>();
        public GameObject Player { get; private set; }

        public void SetPlayer(GameObject player) => Player = player;

        void Awake()
        {
            if (Actors == null)
            {
                Actors = new List<Actor>();
            }
        }
    }
}

using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AI
{
    public class EnemyManager : MonoBehaviour
    {
        public List<EnemyController> Enemies { get; private set; }
        public int NumberOfEnemiesTotal { get; private set; }
        public int NumberOfEnemiesRemaining => Enemies != null ? Enemies.Count : 0;

        void Awake()
        {
            EnsureEnemyListInitialized();
        }

        public void RegisterEnemy(EnemyController enemy)
        {
            EnsureEnemyListInitialized();
            if (enemy == null || Enemies.Contains(enemy))
            {
                return;
            }

            Enemies.Add(enemy);

            NumberOfEnemiesTotal++;
        }

        public void UnregisterEnemy(EnemyController enemyKilled)
        {
            EnsureEnemyListInitialized();
            if (enemyKilled == null || !Enemies.Contains(enemyKilled))
            {
                return;
            }

            int enemiesRemainingNotification = NumberOfEnemiesRemaining - 1;

            EnemyKillEvent evt = Events.EnemyKillEvent;
            evt.Enemy = enemyKilled.gameObject;
            evt.RemainingEnemyCount = enemiesRemainingNotification;
            EventManager.Broadcast(evt);

            // removes the enemy from the list, so that we can keep track of how many are left on the map
            Enemies.Remove(enemyKilled);
        }

        public void UnregisterEnemySilently(EnemyController enemy)
        {
            EnsureEnemyListInitialized();
            if (enemy == null)
            {
                return;
            }

            Enemies.Remove(enemy);
        }

        void EnsureEnemyListInitialized()
        {
            if (Enemies == null)
            {
                Enemies = new List<EnemyController>();
            }
        }
    }
}
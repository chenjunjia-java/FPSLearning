using System.Collections.Generic;
using UnityEngine;
using NavMeshSurface = Unity.AI.Navigation.NavMeshSurface;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.FPS.Roguelike.Level
{
    public static class RoguelikeMainSceneBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapOnMainScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "MainScene")
            {
                return;
            }

            var existingGenerator = Object.FindObjectOfType<RoguelikeLevelGenerator>();
            if (existingGenerator != null)
            {
                return;
            }

            var bootstrapGo = new GameObject("RoguelikeBootstrap");
            var generator = bootstrapGo.AddComponent<RoguelikeLevelGenerator>();
            bootstrapGo.AddComponent<DebugOpenDoorTrigger>();

            var firstSegment = Object.FindObjectOfType<LevelSegment>();
            var navMeshSurface = Object.FindObjectOfType<NavMeshSurface>();
            var normalSegments = new List<GameObject>(2);
            GameObject bossSegment = null;

#if UNITY_EDITOR
            TryLoadDefaultSegments(normalSegments, out bossSegment);
#endif

            generator.Configure(firstSegment, normalSegments, bossSegment, navMeshSurface);
            generator.StartRun();
        }

#if UNITY_EDITOR
        private static void TryLoadDefaultSegments(List<GameObject> normalSegments, out GameObject bossSegment)
        {
            normalSegments.Clear();
            bossSegment = null;

            var roomSmallT = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/Level/Rooms/Room_Small_T.prefab");
            var roomSmallY = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/Level/Rooms/Room_Small_Y.prefab");
            var roomMedium = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/Level/Rooms/Room_Medium.prefab");

            if (roomSmallT != null) normalSegments.Add(roomSmallT);
            if (roomSmallY != null) normalSegments.Add(roomSmallY);
            if (roomMedium != null) bossSegment = roomMedium;
        }
#endif
    }
}

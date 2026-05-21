using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GreedLast.Editor
{
    public static class GreedLastSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/GreedLast_Boot.unity";

        public static void Build()
        {
            Directory.CreateDirectory("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "GreedLast_Boot";

            var marker = new GameObject("GreedLastSceneMarker");
            marker.transform.position = Vector3.zero;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}

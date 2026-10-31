#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FracturedFrequencies.EditorTools
{
    /// <summary>
    /// Rebuilds the Room 1 scene once after the isometric version of the builder is installed, then opens it.
    /// Runs once per machine and project; afterwards use Tools > Fractured Frequencies > Build Room 1.
    /// </summary>
    [InitializeOnLoad]
    public static class Room1AutoBuild
    {
        private const string Version = "iso-v1";
        private static string PrefKey => $"FF_Room1AutoBuilt_{Version}_{Application.dataPath}";

        static Room1AutoBuild()
        {
            EditorApplication.delayCall += TryAutoBuild;
        }

        private static void TryAutoBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (EditorPrefs.GetBool(PrefKey, false)) return;

            EditorPrefs.SetBool(PrefKey, true);
            Debug.Log("[FF] Building the isometric version of Room 1...");
            Room1Builder.Build();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Room1Builder.ScenePath) != null)
                EditorSceneManager.OpenScene(Room1Builder.ScenePath);
        }
    }
}
#endif

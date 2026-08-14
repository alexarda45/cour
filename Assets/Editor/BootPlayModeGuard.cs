#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ChromaBlast.Editor
{
    [InitializeOnLoad]
    internal static class BootPlayModeGuard
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        static BootPlayModeGuard()
        {
            EditorApplication.delayCall += EnsureBootStartsPlayMode;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                EnsureBootStartsPlayMode();
            }
        }

        private static void EnsureBootStartsPlayMode()
        {
            SceneAsset bootScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
            if (bootScene != null && EditorSceneManager.playModeStartScene != bootScene)
            {
                EditorSceneManager.playModeStartScene = bootScene;
            }
        }
    }
}
#endif

using GroceryQuotaHorror.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GroceryQuotaHorror.Editor
{
    [InitializeOnLoad]
    public static class LocalPrototypeLauncher
    {
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string PendingLaunchKey = "GroceryQuotaHorror.LocalPrototypeLaunchPending";

        static LocalPrototypeLauncher()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Grocery Quota Horror/Play Prototype Local")]
        private static void PlayPrototypeLocal()
        {
            SessionState.SetBool(PendingLaunchKey, true);

            if (EditorApplication.isPlaying)
            {
                QueuePrototypeLoad();
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                SessionState.EraseBool(PendingLaunchKey);
                return;
            }

            EditorSceneManager.OpenScene(BootstrapScenePath);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Grocery Quota Horror/Play Prototype Local", true)]
        private static bool ValidatePlayPrototypeLocal()
        {
            return !EditorApplication.isCompiling &&
                   (EditorApplication.isPlaying || !EditorApplication.isPlayingOrWillChangePlaymode);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingLaunchKey, false))
            {
                QueuePrototypeLoad();
            }
        }

        private static void QueuePrototypeLoad()
        {
            EditorApplication.delayCall += () =>
            {
                var bootstrap = Object.FindAnyObjectByType<NetworkBootstrap>();
                if (bootstrap == null)
                {
                    Debug.LogError("[OfflineSpawn] Cannot open TestPrototype locally because NetworkBootstrap is not present.");
                    SessionState.EraseBool(PendingLaunchKey);
                    return;
                }

                SessionState.EraseBool(PendingLaunchKey);
                bootstrap.OpenPrototypeLocal();
            };
        }
    }
}

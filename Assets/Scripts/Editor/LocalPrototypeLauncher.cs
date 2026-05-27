using GroceryQuotaHorror.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GroceryQuotaHorror.Editor
{
    public static class LocalPrototypeLauncher
    {
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string PendingLaunchKey = "GroceryQuotaHorror.LocalLaunchDestination";

        private enum LocalLaunchDestination
        {
            Prototype = 1,
            Night = 2
        }

        [MenuItem("Grocery Quota Horror/Play Prototype Local _F6")]
        private static void PlayPrototypeLocal()
        {
            StartLocalLaunch(LocalLaunchDestination.Prototype);
        }

        [MenuItem("Grocery Quota Horror/Play Prototype Local _F6", true)]
        private static bool ValidatePlayPrototypeLocal()
        {
            return CanStartLocalLaunch();
        }

        [MenuItem("Grocery Quota Horror/Play Night Local _F7")]
        private static void PlayNightLocal()
        {
            StartLocalLaunch(LocalLaunchDestination.Night);
        }

        [MenuItem("Grocery Quota Horror/Play Night Local _F7", true)]
        private static bool ValidatePlayNightLocal()
        {
            return CanStartLocalLaunch();
        }

        private static bool CanStartLocalLaunch()
        {
            return !EditorApplication.isCompiling &&
                   (EditorApplication.isPlaying || !EditorApplication.isPlayingOrWillChangePlaymode);
        }

        private static void StartLocalLaunch(LocalLaunchDestination destination)
        {
            SessionState.SetInt(PendingLaunchKey, (int)destination);

            if (EditorApplication.isPlaying)
            {
                SessionState.EraseInt(PendingLaunchKey);
                QueueLocalSceneLoad(destination);
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                SessionState.EraseInt(PendingLaunchKey);
                return;
            }

            EditorSceneManager.OpenScene(BootstrapScenePath);
            EditorApplication.EnterPlaymode();
        }

        private static void QueueLocalSceneLoad(LocalLaunchDestination destination)
        {
            EditorApplication.delayCall += () =>
            {
                var bootstrap = Object.FindAnyObjectByType<NetworkBootstrap>();
                if (bootstrap == null)
                {
                    Debug.LogError("[OfflineSpawn] Cannot open a local scene because NetworkBootstrap is not present.");
                    return;
                }

                if (destination == LocalLaunchDestination.Prototype)
                {
                    bootstrap.OpenPrototypeLocal();
                }
                else
                {
                    bootstrap.OpenNightLocal();
                }
            };
        }
    }
}

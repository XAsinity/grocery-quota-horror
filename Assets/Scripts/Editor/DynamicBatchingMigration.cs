using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GroceryQuotaHorror.Editor
{
    [InitializeOnLoad]
    internal static class DynamicBatchingMigration
    {
        static DynamicBatchingMigration()
        {
            DisableDeprecatedDynamicBatching();
        }

        private static void DisableDeprecatedDynamicBatching()
        {
            var getDynamicBatching = typeof(PlayerSettings).GetMethod(
                "GetDynamicBatchingForPlatform",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(BuildTarget) },
                null);

            var setDynamicBatching = typeof(PlayerSettings).GetMethod(
                "SetDynamicBatchingForPlatform",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(BuildTarget), typeof(bool) },
                null);

            if (getDynamicBatching == null || setDynamicBatching == null)
            {
                Debug.LogWarning("Could not find Unity PlayerSettings dynamic batching API.");
                return;
            }

            var changed = false;
            foreach (var targetName in new[] { "StandaloneWindows64", "StandaloneWindows", "StandaloneOSX", "StandaloneLinux64" })
            {
                if (!Enum.TryParse(targetName, out BuildTarget target))
                {
                    continue;
                }

                var enabled = (bool)getDynamicBatching.Invoke(null, new object[] { target });
                if (!enabled)
                {
                    continue;
                }

                setDynamicBatching.Invoke(null, new object[] { target, false });
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Disabled deprecated Dynamic Batching in Player Settings for standalone targets.");
        }
    }
}

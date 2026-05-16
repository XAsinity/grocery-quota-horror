using GroceryQuotaHorror.Data;
using UnityEditor;
using UnityEngine;

namespace GroceryQuotaHorror.Editor
{
    [InitializeOnLoad]
    public static class GameplayTuningBootstrap
    {
        private const string ResourcesFolder = "Assets/Resources";
        private const string AssetPath = "Assets/Resources/GameplayTuning.asset";

        static GameplayTuningBootstrap()
        {
            EditorApplication.delayCall += EnsureAssetExists;
        }

        private static void EnsureAssetExists()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            var existing = AssetDatabase.LoadAssetAtPath<GameplayTuning>(AssetPath);
            if (existing != null)
            {
                return;
            }

            var asset = ScriptableObject.CreateInstance<GameplayTuning>();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}

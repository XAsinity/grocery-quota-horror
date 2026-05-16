using GroceryQuotaHorror.Physics;
using GroceryQuotaHorror.Player;
using UnityEditor;
using UnityEngine;

namespace GroceryQuotaHorror.Editor
{
    [InitializeOnLoad]
    public static class RagdollSandboxBootstrap
    {
        private const string CharacterModelPath = "Assets/CharacterREF/newgamecharacter.fbx";
        private const string RagdollPrefabPath = "Assets/Prefabs/Gameplay/NewGameCharacterRagdoll.prefab";

        static RagdollSandboxBootstrap()
        {
            EditorApplication.delayCall += EnsureSetup;
        }

        private static void EnsureSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
            if (modelAsset == null)
            {
                return;
            }

            EnsureRagdollPrefab(modelAsset);
            EnsurePlayerReference();
            AssetDatabase.SaveAssets();
        }

        private static void EnsureRagdollPrefab(GameObject modelAsset)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RagdollPrefabPath);
            if (prefab != null)
            {
                return;
            }

            var root = new GameObject("NewGameCharacterRagdoll");
            var ragdoll = root.AddComponent<SpawnableRagdoll>();
            var modelInstance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
            if (modelInstance != null)
            {
                modelInstance.name = modelAsset.name;
                modelInstance.transform.SetParent(root.transform, false);
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;

                var serialized = new SerializedObject(ragdoll);
                serialized.FindProperty("modelRoot").objectReferenceValue = modelInstance.transform;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, RagdollPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void EnsurePlayerReference()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainCharacterPlayerBootstrap.PlayerPrefabPath);
            var ragdollPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RagdollPrefabPath);
            if (playerPrefab == null || ragdollPrefab == null)
            {
                return;
            }

            var player = playerPrefab.GetComponent<PlayerController>();
            if (player == null)
            {
                return;
            }

            var serialized = new SerializedObject(player);
            var property = serialized.FindProperty("spawnableRagdollPrefab");
            if (property.objectReferenceValue == ragdollPrefab)
            {
                return;
            }

            property.objectReferenceValue = ragdollPrefab.GetComponent<SpawnableRagdoll>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(playerPrefab);
        }
    }
}

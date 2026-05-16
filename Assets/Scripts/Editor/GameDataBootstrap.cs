using System.Collections.Generic;
using GroceryQuotaHorror.Data;
using UnityEditor;
using UnityEngine;

namespace GroceryQuotaHorror.Editor
{
    [InitializeOnLoad]
    public static class GameDataBootstrap
    {
        private const string DataRoot = "Assets/Data";
        private const string ResourcesRoot = "Assets/Resources";
        private const string BalanceFolder = "Assets/Resources/BalanceProfiles";
        private const string PrototypeProfilePath = "Assets/Resources/BalanceProfiles/Prototype.asset";
        private const string NightProfilePath = "Assets/Resources/BalanceProfiles/Night.asset";
        private const string StressProfilePath = "Assets/Resources/BalanceProfiles/StressTest.asset";
        private const string ContentDatabasePath = "Assets/Resources/GameContentDatabase.asset";

        static GameDataBootstrap()
        {
            EditorApplication.delayCall += EnsureDataAssets;
        }

        [MenuItem("Tools/Grocery Quota Horror/Validate Gameplay Data")]
        public static void ValidateGameplayData()
        {
            EnsureDataAssets();
            var content = AssetDatabase.LoadAssetAtPath<GameContentDatabase>(ContentDatabasePath);
            if (content == null)
            {
                Debug.LogError("Missing GameContentDatabase asset.");
                return;
            }

            ValidateUniqueIds(content.itemPool, item => item != null ? item.itemId : string.Empty, "item");
            ValidateUniqueIds(content.monsterPool, monster => monster != null ? monster.monsterId : string.Empty, "monster");
            ValidateUniqueIds(content.chunkPool, chunk => chunk != null ? chunk.chunkId : string.Empty, "chunk");

            for (var i = 0; i < content.chunkPool.Count; i++)
            {
                if (content.chunkPool[i] == null || content.chunkPool[i].prefab != null)
                {
                    continue;
                }

                Debug.LogWarning($"Chunk asset '{content.chunkPool[i].name}' is missing a prefab reference.");
            }

            if (content.itemPickupPrefab == null || content.monsterPrefab == null || content.nightGameManagerPrefab == null)
            {
                Debug.LogWarning("Content database is missing one or more prefab references.");
            }

            Debug.Log("Gameplay data validation completed.");
        }

        [MenuItem("Tools/Grocery Quota Horror/Rebuild Shared Data Assets")]
        public static void EnsureDataAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EnsureFolder(ResourcesRoot);
            EnsureFolder(BalanceFolder);
            var content = LoadOrCreate<GameContentDatabase>(ContentDatabasePath);
            PopulateContentDatabase(content);
            EnsureProfile(PrototypeProfilePath, profile => { });
            EnsureProfile(NightProfilePath, profile =>
            {
                profile.objectives.nightLengthSeconds = 520f;
                profile.spawn.monsterBudget = 4;
            });
            EnsureProfile(StressProfilePath, profile =>
            {
                profile.playerMovement.moveSpeed = 6f;
                profile.playerBody.pelvisFollowForce = 160f;
                profile.ragdoll.ragdollHoldForce = 140f;
                profile.spawn.monsterBudget = 5;
            });
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void PopulateContentDatabase(GameContentDatabase content)
        {
            content.itemPool = LoadAssets<ItemDefinition>($"{DataRoot}/Items");
            content.monsterPool = LoadAssets<MonsterDefinition>($"{DataRoot}/Monsters");
            content.chunkPool = LoadAssets<StoreChunkDefinition>($"{DataRoot}/Chunks");
            content.itemPickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/ItemPickup.prefab");
            content.monsterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Monster.prefab");
            content.nightGameManagerPrefab = AssetDatabase.LoadAssetAtPath<GroceryQuotaHorror.Core.NightGameManager>("Assets/Prefabs/Managers/NightGameManager.prefab");
            EditorUtility.SetDirty(content);
        }

        private static void EnsureProfile(string path, System.Action<GameBalanceProfile> configure)
        {
            var profile = LoadOrCreate<GameBalanceProfile>(path);
            configure(profile);
            EditorUtility.SetDirty(profile);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static List<T> LoadAssets<T>(string folder) where T : Object
        {
            var assets = new List<T>();
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            return assets;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void ValidateUniqueIds<T>(IReadOnlyList<T> list, System.Func<T, string> getId, string label) where T : Object
        {
            var seen = new HashSet<string>();
            for (var i = 0; i < list.Count; i++)
            {
                var id = getId(list[i]);
                if (string.IsNullOrWhiteSpace(id))
                {
                    Debug.LogWarning($"Found {label} asset with empty id: {list[i]?.name}");
                    continue;
                }

                if (!seen.Add(id))
                {
                    Debug.LogWarning($"Duplicate {label} id found: {id}");
                }
            }
        }
    }
}

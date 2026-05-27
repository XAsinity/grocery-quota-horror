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
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void PopulateContentDatabase(GameContentDatabase content)
        {
            RepairChunkDefinitions();
            content.itemPool = LoadAssets<ItemDefinition>($"{DataRoot}/Items");
            content.monsterPool = LoadAssets<MonsterDefinition>($"{DataRoot}/Monsters");
            content.chunkPool = LoadAssets<StoreChunkDefinition>($"{DataRoot}/Chunks");
            content.itemPickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/ItemPickup.prefab");
            content.monsterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Monster.prefab");
            content.nightGameManagerPrefab = AssetDatabase.LoadAssetAtPath<GroceryQuotaHorror.Core.NightGameManager>("Assets/Prefabs/Managers/NightGameManager.prefab");
            EditorUtility.SetDirty(content);
        }

        private static void RepairChunkDefinitions()
        {
            var definitions = new (string id, RoomType type, SpawnZone zone, bool allowMonsters, Color color)[]
            {
                ("front_entrance", RoomType.FrontEntrance, SpawnZone.Front, false, new Color(0.75f, 0.75f, 0.8f)),
                ("produce", RoomType.Produce, SpawnZone.Retail, true, new Color(0.2f, 0.65f, 0.2f)),
                ("retail_a", RoomType.RetailAisle, SpawnZone.Retail, true, new Color(0.8f, 0.7f, 0.25f)),
                ("retail_b", RoomType.RetailAisle, SpawnZone.Retail, true, new Color(0.7f, 0.45f, 0.15f)),
                ("freezer", RoomType.Freezer, SpawnZone.Cold, true, new Color(0.55f, 0.8f, 1f)),
                ("bakery", RoomType.Bakery, SpawnZone.Retail, true, new Color(0.8f, 0.55f, 0.3f)),
                ("stockroom", RoomType.Stockroom, SpawnZone.Backroom, true, new Color(0.45f, 0.45f, 0.45f)),
                ("utility", RoomType.Utility, SpawnZone.Utility, true, new Color(0.55f, 0.55f, 0.7f)),
                ("loading_dock", RoomType.LoadingDock, SpawnZone.Backroom, true, new Color(0.3f, 0.3f, 0.3f)),
                ("checkout", RoomType.Checkout, SpawnZone.Checkout, false, new Color(0.9f, 0.2f, 0.2f))
            };

            for (var i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                var chunk = AssetDatabase.LoadAssetAtPath<StoreChunkDefinition>($"{DataRoot}/Chunks/{definition.id}.asset");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Chunks/{definition.id}.prefab");
                if (chunk == null || prefab == null)
                {
                    Debug.LogWarning($"Cannot repair chunk definition '{definition.id}' because its data asset or prefab is missing.");
                    continue;
                }

                if (chunk.chunkId == definition.id &&
                    chunk.roomType == definition.type &&
                    chunk.prefab == prefab &&
                    chunk.spawnTags == definition.zone &&
                    chunk.allowMonsters == definition.allowMonsters &&
                    chunk.gizmoColor == definition.color)
                {
                    continue;
                }

                chunk.chunkId = definition.id;
                chunk.roomType = definition.type;
                chunk.prefab = prefab;
                chunk.spawnTags = definition.zone;
                chunk.allowMonsters = definition.allowMonsters;
                chunk.gizmoColor = definition.color;
                EditorUtility.SetDirty(chunk);
            }
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

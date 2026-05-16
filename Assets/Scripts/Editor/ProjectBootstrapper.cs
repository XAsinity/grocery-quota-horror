using System.Collections.Generic;
using GroceryQuotaHorror.Bootstrap;
using GroceryQuotaHorror.Core;
using GroceryQuotaHorror.Data;
using GroceryQuotaHorror.Generation;
using GroceryQuotaHorror.Interaction;
using GroceryQuotaHorror.Monsters;
using GroceryQuotaHorror.Player;
using GroceryQuotaHorror.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GroceryQuotaHorror.Editor
{
    public static class ProjectBootstrapper
    {
        private const string AssetsRoot = "Assets";
        private const string ScenesRoot = "Assets/Scenes";
        private const string DataRoot = "Assets/Data";
        private const string PrefabRoot = "Assets/Prefabs";

        private static void BuildStarterProject()
        {
            EnsureFolders();
            CreateDataAssets();
            CreateGameplayPrefabs();
            CreateChunkPrefabsAndData();
            GameDataBootstrap.EnsureDataAssets();
            CreateScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Grocery Quota Horror starter project generated.");
        }

        private static void BuildStarterProjectBatch()
        {
            BuildStarterProject();
            EditorApplication.Exit(0);
        }

        private static void EnsureFolders()
        {
            var folders = new[]
            {
                ScenesRoot,
                $"{DataRoot}/Items",
                $"{DataRoot}/Monsters",
                $"{DataRoot}/Chunks",
                $"{PrefabRoot}/Chunks",
                $"{PrefabRoot}/Gameplay",
                $"{PrefabRoot}/Managers"
            };

            for (var i = 0; i < folders.Length; i++)
            {
                if (!AssetDatabase.IsValidFolder(folders[i]))
                {
                    var parts = folders[i].Split('/');
                    var current = parts[0];
                    for (var j = 1; j < parts.Length; j++)
                    {
                        var next = $"{current}/{parts[j]}";
                        if (!AssetDatabase.IsValidFolder(next))
                        {
                            AssetDatabase.CreateFolder(current, parts[j]);
                        }

                        current = next;
                    }
                }
            }
        }

        private static void CreateDataAssets()
        {
            var runConfig = LoadOrCreateAsset<RunConfig>($"{DataRoot}/RunConfig.asset");
            runConfig.itemPool.Clear();
            runConfig.monsterPool.Clear();
            runConfig.chunkPool.Clear();

            var items = new (string id, string name, int value, SpawnZone zone, Color tint)[]
            {
                ("beans", "Beans", 1, SpawnZone.Retail, new Color(0.75f, 0.1f, 0.1f)),
                ("milk", "Milk", 2, SpawnZone.Cold, Color.white),
                ("bread", "Bread", 1, SpawnZone.Retail, new Color(0.75f, 0.55f, 0.3f)),
                ("cereal", "Cereal", 2, SpawnZone.Retail, new Color(0.95f, 0.8f, 0.2f)),
                ("fish", "Frozen Fish", 3, SpawnZone.Cold, new Color(0.55f, 0.85f, 1f)),
                ("soap", "Soap", 2, SpawnZone.Utility, new Color(0.6f, 0.9f, 0.9f)),
                ("soda", "Soda", 1, SpawnZone.Checkout | SpawnZone.Retail, new Color(0.2f, 0.2f, 0.8f)),
                ("cake", "Cake", 3, SpawnZone.Retail, new Color(0.95f, 0.65f, 0.8f)),
                ("steak", "Steak", 3, SpawnZone.Cold | SpawnZone.Backroom, new Color(0.7f, 0.2f, 0.25f)),
                ("rice", "Rice", 2, SpawnZone.Retail, new Color(0.9f, 0.9f, 0.7f))
            };

            for (var i = 0; i < items.Length; i++)
            {
                var asset = LoadOrCreateAsset<ItemDefinition>($"{DataRoot}/Items/{items[i].id}.asset");
                asset.itemId = items[i].id;
                asset.displayName = items[i].name;
                asset.quotaValue = items[i].value;
                asset.allowedZones = items[i].zone;
                asset.tint = items[i].tint;
                runConfig.itemPool.Add(asset);
            }

            var monsters = new (string id, string name, MonsterArchetype type, Color tint)[]
            {
                ("lurker", "Aisle Lurker", MonsterArchetype.Roamer, new Color(0.65f, 0.1f, 0.1f)),
                ("listener", "Freezer Listener", MonsterArchetype.Listener, new Color(0.2f, 0.4f, 0.8f)),
                ("stalker", "Stockroom Stalker", MonsterArchetype.Ambusher, new Color(0.5f, 0.8f, 0.1f))
            };

            for (var i = 0; i < monsters.Length; i++)
            {
                var asset = LoadOrCreateAsset<MonsterDefinition>($"{DataRoot}/Monsters/{monsters[i].id}.asset");
                asset.monsterId = monsters[i].id;
                asset.displayName = monsters[i].name;
                asset.archetype = monsters[i].type;
                asset.tint = monsters[i].tint;
                runConfig.monsterPool.Add(asset);
            }

            EditorUtility.SetDirty(runConfig);
        }

        private static void CreateGameplayPrefabs()
        {
            var player = CreatePlayerPrefab();
            var item = CreateItemPrefab();
            var monster = CreateMonsterPrefab();
            var manager = CreateManagerPrefab();

            var content = AssetDatabase.LoadAssetAtPath<GameContentDatabase>("Assets/Resources/GameContentDatabase.asset");
            manager.Configure(content, null);
            EditorUtility.SetDirty(manager);
        }

        private static PlayerController CreatePlayerPrefab()
        {
            MainCharacterPlayerBootstrap.EnsurePlayerPrefab();
            return AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Gameplay/Player.prefab")?.GetComponent<PlayerController>();
        }

        private static ItemPickup CreateItemPrefab()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "ItemPickup";
            root.transform.localScale = new Vector3(0.45f, 0.3f, 0.45f);
            root.AddComponent<Rigidbody>();
            root.AddComponent<NetworkObject>();
            var pickup = root.AddComponent<ItemPickup>();

            var so = new SerializedObject(pickup);
            so.FindProperty("meshRenderer").objectReferenceValue = root.GetComponent<MeshRenderer>();
            so.ApplyModifiedPropertiesWithoutUndo();

            var path = $"{PrefabRoot}/Gameplay/ItemPickup.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<ItemPickup>();
        }

        private static MonsterController CreateMonsterPrefab()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "Monster";
            root.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
            root.AddComponent<NetworkObject>();
            root.AddComponent<Unity.Netcode.Components.NetworkTransform>();
            var monster = root.AddComponent<MonsterController>();
            var so = new SerializedObject(monster);
            so.FindProperty("meshRenderer").objectReferenceValue = root.GetComponent<MeshRenderer>();
            so.ApplyModifiedPropertiesWithoutUndo();

            var path = $"{PrefabRoot}/Gameplay/Monster.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<MonsterController>();
        }

        private static NightGameManager CreateManagerPrefab()
        {
            var root = new GameObject("NightGameManager");
            root.AddComponent<NetworkObject>();
            var manager = root.AddComponent<NightGameManager>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/Managers/NightGameManager.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<NightGameManager>();
        }

        private static void CreateChunkPrefabsAndData()
        {
            var runConfig = AssetDatabase.LoadAssetAtPath<RunConfig>($"{DataRoot}/RunConfig.asset");
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
                var prefab = BuildChunkPrefab(definitions[i].id, definitions[i].zone, definitions[i].color);
                var chunk = LoadOrCreateAsset<StoreChunkDefinition>($"{DataRoot}/Chunks/{definitions[i].id}.asset");
                chunk.chunkId = definitions[i].id;
                chunk.roomType = definitions[i].type;
                chunk.prefab = prefab;
                chunk.spawnTags = definitions[i].zone;
                chunk.allowMonsters = definitions[i].allowMonsters;
                chunk.gizmoColor = definitions[i].color;
                if (!runConfig.chunkPool.Contains(chunk))
                {
                    runConfig.chunkPool.Add(chunk);
                }
            }

            EditorUtility.SetDirty(runConfig);
        }

        private static GameObject BuildChunkPrefab(string chunkId, SpawnZone zone, Color accent)
        {
            var root = new GameObject(chunkId);
            root.transform.position = Vector3.zero;
            root.AddComponent<ChunkMetadata>().spawnZone = zone;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(root.transform);
            floor.transform.localScale = new Vector3(18f, 0.3f, 24f);
            floor.transform.localPosition = Vector3.zero;
            floor.GetComponent<Renderer>().sharedMaterial.color = new Color(0.08f, 0.08f, 0.08f);

            var leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftWall.transform.SetParent(root.transform);
            leftWall.transform.localScale = new Vector3(0.4f, 4f, 24f);
            leftWall.transform.localPosition = new Vector3(-9f, 2f, 0f);
            leftWall.GetComponent<Renderer>().sharedMaterial.color = accent * 0.7f;

            var rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightWall.transform.SetParent(root.transform);
            rightWall.transform.localScale = new Vector3(0.4f, 4f, 24f);
            rightWall.transform.localPosition = new Vector3(9f, 2f, 0f);
            rightWall.GetComponent<Renderer>().sharedMaterial.color = accent * 0.7f;

            for (var i = 0; i < 3; i++)
            {
                var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shelf.transform.SetParent(root.transform);
                shelf.transform.localScale = new Vector3(1.6f, 2.2f, 8f);
                shelf.transform.localPosition = new Vector3(-4f + i * 4f, 1.1f, 0f);
                shelf.GetComponent<Renderer>().sharedMaterial.color = accent;

                var marker = new GameObject($"SpawnMarker_{i}");
                marker.transform.SetParent(shelf.transform);
                marker.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                marker.AddComponent<ItemSpawnMarker>().zone = zone;
            }

            var patrol = new GameObject("PatrolPoint");
            patrol.transform.SetParent(root.transform);
            patrol.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            patrol.AddComponent<PatrolPoint>();

            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.transform.SetParent(root.transform);
            door.transform.localScale = new Vector3(3f, 3f, 0.2f);
            door.transform.localPosition = new Vector3(0f, 1.5f, 10.8f);
            door.AddComponent<DoorInteractable>();

            var hide = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hide.transform.SetParent(root.transform);
            hide.transform.localScale = new Vector3(1f, 1f, 1f);
            hide.transform.localPosition = new Vector3(-7f, 0.5f, -8f);
            hide.AddComponent<HideSpot>();
            hide.GetComponent<Renderer>().sharedMaterial.color = Color.black;

            var path = $"{PrefabRoot}/Chunks/{chunkId}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateScenes()
        {
            CreateBootstrapScene();
            CreateNightScene();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene($"{ScenesRoot}/Bootstrap.unity", true),
                new EditorBuildSettingsScene($"{ScenesRoot}/SupermarketNight.unity", true),
                new EditorBuildSettingsScene($"{ScenesRoot}/TestPrototype.unity", true)
            };
        }

        private static void CreateBootstrapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera");
            camera.tag = "MainCamera";
            camera.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 3f, -10f);
            camera.transform.rotation = Quaternion.Euler(12f, 0f, 0f);

            var light = new GameObject("Directional Light");
            light.AddComponent<Light>().type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            var netRoot = new GameObject("NetworkRoot");
            var transport = netRoot.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", (ushort)7777);
            var manager = netRoot.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig();
            manager.NetworkConfig.PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Gameplay/Player.prefab");
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.EnableSceneManagement = true;
            var prefabList = LoadOrCreateNetworkPrefabsList($"{DataRoot}/NetworkPrefabsList.asset");
            ClearNetworkPrefabsList(prefabList);
            prefabList.Add(new NetworkPrefab { Prefab = manager.NetworkConfig.PlayerPrefab });
            prefabList.Add(new NetworkPrefab { Prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Gameplay/ItemPickup.prefab") });
            prefabList.Add(new NetworkPrefab { Prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Gameplay/Monster.prefab") });
            prefabList.Add(new NetworkPrefab { Prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Managers/NightGameManager.prefab") });
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);

            var bootstrap = netRoot.AddComponent<NetworkBootstrap>();
            var so = new SerializedObject(bootstrap);
            so.FindProperty("networkManager").objectReferenceValue = manager;
            so.FindProperty("balanceProfile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameBalanceProfile>("Assets/Resources/BalanceProfiles/Prototype.asset");
            so.FindProperty("contentDatabase").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameContentDatabase>("Assets/Resources/GameContentDatabase.asset");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, $"{ScenesRoot}/Bootstrap.unity");
        }

        private static void CreateNightScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera");
            camera.tag = "MainCamera";
            camera.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 18f, -18f);
            camera.transform.rotation = Quaternion.Euler(28f, 0f, 0f);

            var light = new GameObject("Moon Light");
            var moon = light.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.intensity = 0.6f;
            light.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            var root = new GameObject("NightSystems");
            var dynamicRoot = new GameObject("GeneratedWorld");
            dynamicRoot.transform.SetParent(root.transform);

            var generator = root.AddComponent<StoreGenerator>();
            var bootstrap = root.AddComponent<NightSceneBootstrap>();
            root.AddComponent<HudUiController>();

            var deposit = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            deposit.name = "DepositZone";
            deposit.transform.position = new Vector3(0f, 0.5f, -8f);
            deposit.transform.localScale = new Vector3(2f, 0.5f, 2f);
            deposit.AddComponent<DepositZone>();

            var extraction = GameObject.CreatePrimitive(PrimitiveType.Cube);
            extraction.name = "ExtractionZone";
            extraction.transform.position = new Vector3(0f, 1f, 220f);
            extraction.transform.localScale = new Vector3(4f, 2f, 2f);
            extraction.AddComponent<ExtractionZone>();
            extraction.GetComponent<Renderer>().sharedMaterial.color = new Color(0.2f, 0.8f, 0.2f);

            var bootstrapSo = new SerializedObject(bootstrap);
            bootstrapSo.FindProperty("balanceProfile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameBalanceProfile>("Assets/Resources/BalanceProfiles/Night.asset");
            bootstrapSo.FindProperty("contentDatabase").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameContentDatabase>("Assets/Resources/GameContentDatabase.asset");
            bootstrapSo.FindProperty("storeGenerator").objectReferenceValue = generator;
            bootstrapSo.FindProperty("gameManagerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<NightGameManager>($"{PrefabRoot}/Managers/NightGameManager.prefab");
            bootstrapSo.FindProperty("dynamicRoot").objectReferenceValue = dynamicRoot.transform;
            bootstrapSo.ApplyModifiedPropertiesWithoutUndo();

            var generatorSo = new SerializedObject(generator);
            generatorSo.FindProperty("contentDatabase").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameContentDatabase>("Assets/Resources/GameContentDatabase.asset");
            generatorSo.FindProperty("chunkRoot").objectReferenceValue = dynamicRoot.transform;
            generatorSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, $"{ScenesRoot}/SupermarketNight.unity");
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
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

        private static NetworkPrefabsList LoadOrCreateNetworkPrefabsList(string path)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(path);
            if (list != null)
            {
                return list;
            }

            list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            AssetDatabase.CreateAsset(list, path);
            return list;
        }

        private static void ClearNetworkPrefabsList(NetworkPrefabsList list)
        {
            var serialized = new SerializedObject(list);
            serialized.FindProperty("List").ClearArray();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

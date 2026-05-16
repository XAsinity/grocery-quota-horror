using System.Collections.Generic;
using GroceryQuotaHorror.Core;
using GroceryQuotaHorror.Data;
using GroceryQuotaHorror.Generation;
using Unity.Netcode;
using UnityEngine;

namespace GroceryQuotaHorror.Bootstrap
{
    public sealed class NightSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private GameBalanceProfile balanceProfile;
        [SerializeField] private GameContentDatabase contentDatabase;
        [SerializeField] private StoreGenerator storeGenerator;
        [SerializeField] private NightGameManager gameManagerPrefab;
        [SerializeField] private Transform dynamicRoot;

        private NightGameManager gameManagerInstance;

        public IReadOnlyList<ItemSpawnMarker> CurrentSpawnMarkers => storeGenerator.SpawnMarkers;
        public IReadOnlyList<Transform> CurrentPatrolPoints => storeGenerator.PatrolPoints;

        private void Start()
        {
            GameRuntime.SetRuntimeAssets(balanceProfile, contentDatabase);
            var networkManager = NetworkManager.Singleton;
            var isOfflineLocal = NetworkBootstrap.LocalOfflineMode || networkManager == null || !networkManager.IsListening;
            if (!isOfflineLocal && !networkManager.IsServer)
            {
                return;
            }

            var content = GameRuntime.Content;
            var managerPrefab = gameManagerPrefab != null ? gameManagerPrefab : content != null ? content.nightGameManagerPrefab : null;
            if (managerPrefab == null)
            {
                Debug.LogError("NightSceneBootstrap is missing a NightGameManager prefab reference.");
                return;
            }

            gameManagerInstance = Instantiate(managerPrefab, Vector3.zero, Quaternion.identity);
            gameManagerInstance.Configure(GameRuntime.Content, dynamicRoot);
            if (!isOfflineLocal && gameManagerInstance.NetworkObject != null)
            {
                gameManagerInstance.NetworkObject.Spawn();
            }

            var balance = GameRuntime.Balance;
            var defaultSeed = balance != null
                ? Random.Range(balance.spawn.defaultSeedMin, balance.spawn.defaultSeedMax)
                : Random.Range(1000, 999999);
            gameManagerInstance.StartNight(defaultSeed);
        }

        public void GenerateStore(int seed)
        {
            storeGenerator.Configure(GameRuntime.Content, dynamicRoot);
            storeGenerator.Generate(seed);
        }
    }
}

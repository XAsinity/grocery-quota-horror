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
        [SerializeField] private RunConfig runConfig;
        [SerializeField] private StoreGenerator storeGenerator;
        [SerializeField] private NightGameManager gameManagerPrefab;
        [SerializeField] private GameObject itemPickupPrefab;
        [SerializeField] private GameObject monsterPrefab;
        [SerializeField] private Transform dynamicRoot;

        private NightGameManager gameManagerInstance;

        public IReadOnlyList<ItemSpawnMarker> CurrentSpawnMarkers => storeGenerator.SpawnMarkers;
        public IReadOnlyList<Transform> CurrentPatrolPoints => storeGenerator.PatrolPoints;

        private void Start()
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                return;
            }

            gameManagerInstance = Instantiate(gameManagerPrefab, Vector3.zero, Quaternion.identity);
            gameManagerInstance.Configure(runConfig, itemPickupPrefab, monsterPrefab, dynamicRoot);
            gameManagerInstance.NetworkObject.Spawn();
            gameManagerInstance.StartNight(Random.Range(1000, 999999));
        }

        public void GenerateStore(int seed)
        {
            storeGenerator.Configure(runConfig, dynamicRoot);
            storeGenerator.Generate(seed);
        }
    }
}


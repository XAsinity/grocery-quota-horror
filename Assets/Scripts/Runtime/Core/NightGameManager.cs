using System;
using System.Collections.Generic;
using GroceryQuotaHorror.Bootstrap;
using GroceryQuotaHorror.Data;
using GroceryQuotaHorror.Interaction;
using GroceryQuotaHorror.Monsters;
using GroceryQuotaHorror.Player;
using Unity.Netcode;
using UnityEngine;

namespace GroceryQuotaHorror.Core
{
    public sealed class NightGameManager : NetworkBehaviour
    {
        public static NightGameManager Instance { get; private set; }

        [SerializeField] private GameContentDatabase contentDatabase;
        [SerializeField] private Transform dynamicRoot;

        private readonly NetworkVariable<int> seed = new(-1);
        private readonly NetworkVariable<int> quotaValue = new();
        private readonly NetworkVariable<float> timeRemaining = new();
        private readonly NetworkVariable<bool> quotaMet = new();
        private readonly NetworkVariable<RunResult> runResult = new(RunResult.InProgress);

        private readonly List<ObjectiveEntry> objectiveEntries = new();
        private readonly List<ItemPickup> liveItems = new();
        private readonly List<MonsterController> liveMonsters = new();
        private readonly HashSet<ulong> downedPlayers = new();

        private bool worldSpawned;

        public IReadOnlyList<ObjectiveEntry> ObjectiveEntries => objectiveEntries;
        public float TimeRemaining => timeRemaining.Value;
        public bool QuotaMet => quotaMet.Value;
        public RunResult CurrentResult => runResult.Value;
        public int Seed => seed.Value;
        public GameContentDatabase ContentDatabase => contentDatabase;

        public event Action StateChanged;

        private bool IsOfflineLocalMode => NetworkBootstrap.LocalOfflineMode || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;

        public void Configure(GameContentDatabase content, Transform root)
        {
            contentDatabase = content;
            dynamicRoot = root;
        }

        private void Awake()
        {
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            seed.OnValueChanged += OnNetworkStateChanged;
            quotaValue.OnValueChanged += OnNetworkStateChanged;
            timeRemaining.OnValueChanged += OnNetworkStateChanged;
            quotaMet.OnValueChanged += OnNetworkStateChanged;
            runResult.OnValueChanged += OnRunResultChanged;
        }

        public override void OnNetworkDespawn()
        {
            seed.OnValueChanged -= OnNetworkStateChanged;
            quotaValue.OnValueChanged -= OnNetworkStateChanged;
            timeRemaining.OnValueChanged -= OnNetworkStateChanged;
            quotaMet.OnValueChanged -= OnNetworkStateChanged;
            runResult.OnValueChanged -= OnRunResultChanged;
        }

        private void Update()
        {
            if ((!IsServer && !IsOfflineLocalMode) || runResult.Value != RunResult.InProgress || GameRuntime.Balance == null)
            {
                return;
            }

            timeRemaining.Value = Mathf.Max(0f, timeRemaining.Value - Time.deltaTime);
            if (timeRemaining.Value <= 0f && runResult.Value == RunResult.InProgress)
            {
                runResult.Value = RunResult.FailedQuota;
            }
        }

        public void StartNight(int nightSeed)
        {
            if ((!IsServer && !IsOfflineLocalMode) || contentDatabase == null || GameRuntime.Balance == null)
            {
                return;
            }

            var objectives = GameRuntime.Balance.objectives;
            seed.Value = nightSeed;
            quotaValue.Value = new System.Random(nightSeed).Next(objectives.minQuotaValue, objectives.maxQuotaValue + 1);
            timeRemaining.Value = objectives.nightLengthSeconds;
            quotaMet.Value = false;
            runResult.Value = RunResult.InProgress;
            objectiveEntries.Clear();
            downedPlayers.Clear();
            BuildObjectiveList(nightSeed);
            SpawnWorldAfterGeneration();
            StateChanged?.Invoke();
        }

        private void BuildObjectiveList(int nightSeed)
        {
            objectiveEntries.Clear();
            var objectives = GameRuntime.Balance.objectives;
            var random = new System.Random(nightSeed ^ objectives.objectiveSeedSalt);
            var chosen = new HashSet<int>();
            var target = Mathf.Clamp(quotaValue.Value / Mathf.Max(1, objectives.objectiveQuotaDivisor), objectives.objectiveMinCount, objectives.objectiveMaxCount);

            while (chosen.Count < target && chosen.Count < contentDatabase.itemPool.Count)
            {
                chosen.Add(random.Next(0, contentDatabase.itemPool.Count));
            }

            foreach (var itemIndex in chosen)
            {
                var item = contentDatabase.itemPool[itemIndex];
                objectiveEntries.Add(new ObjectiveEntry
                {
                    itemIndex = itemIndex,
                    displayName = item.displayName,
                    requiredCount = Mathf.Max(1, quotaValue.Value / Math.Max(item.quotaValue, 1) / target),
                    depositedCount = 0
                });
            }
        }

        private void SpawnWorldAfterGeneration()
        {
            if (worldSpawned)
            {
                DespawnWorld();
            }

            var bootstrap = FindFirstObjectByType<NightSceneBootstrap>();
            if (bootstrap == null)
            {
                return;
            }

            bootstrap.GenerateStore(seed.Value);
            var markers = bootstrap.CurrentSpawnMarkers;
            var content = contentDatabase;
            var spawn = GameRuntime.Balance.spawn;

            for (var i = 0; i < markers.Count; i++)
            {
                var definitionIndex = ChooseItemIndexForZone(markers[i].zone, seed.Value + i * spawn.itemSeedStep);
                var itemPrefab = content.itemPickupPrefab;
                var itemInstance = Instantiate(itemPrefab, markers[i].transform.position, Quaternion.identity, dynamicRoot);
                var pickup = itemInstance.GetComponent<ItemPickup>();
                pickup.Initialize(definitionIndex, content);
                if (!IsOfflineLocalMode && pickup.NetworkObject != null)
                {
                    pickup.NetworkObject.Spawn(true);
                }

                liveItems.Add(pickup);
            }

            var patrolPoints = bootstrap.CurrentPatrolPoints;
            var monsterCount = Mathf.Min(spawn.monsterBudget, patrolPoints.Count);
            for (var i = 0; i < monsterCount; i++)
            {
                var definitionIndex = i % content.monsterPool.Count;
                var monsterGo = Instantiate(content.monsterPrefab, patrolPoints[i].position, Quaternion.identity, dynamicRoot);
                var monster = monsterGo.GetComponent<MonsterController>();
                monster.Initialize(definitionIndex, content, patrolPoints);
                if (!IsOfflineLocalMode && monster.NetworkObject != null)
                {
                    monster.NetworkObject.Spawn(true);
                }

                liveMonsters.Add(monster);
            }

            worldSpawned = true;
        }

        private int ChooseItemIndexForZone(SpawnZone zone, int randomSeed)
        {
            var candidates = new List<int>();
            for (var i = 0; i < contentDatabase.itemPool.Count; i++)
            {
                if ((contentDatabase.itemPool[i].allowedZones & zone) != 0)
                {
                    candidates.Add(i);
                }
            }

            if (candidates.Count == 0)
            {
                return Mathf.Abs(randomSeed) % contentDatabase.itemPool.Count;
            }

            var random = new System.Random(randomSeed);
            return candidates[random.Next(0, candidates.Count)];
        }

        private void DespawnWorld()
        {
            for (var i = 0; i < liveItems.Count; i++)
            {
                if (liveItems[i] != null && liveItems[i].NetworkObject != null && liveItems[i].NetworkObject.IsSpawned)
                {
                    liveItems[i].NetworkObject.Despawn(true);
                }
                else if (liveItems[i] != null)
                {
                    Destroy(liveItems[i].gameObject);
                }
            }

            for (var i = 0; i < liveMonsters.Count; i++)
            {
                if (liveMonsters[i] != null && liveMonsters[i].NetworkObject != null && liveMonsters[i].NetworkObject.IsSpawned)
                {
                    liveMonsters[i].NetworkObject.Despawn(true);
                }
                else if (liveMonsters[i] != null)
                {
                    Destroy(liveMonsters[i].gameObject);
                }
            }

            liveItems.Clear();
            liveMonsters.Clear();
            worldSpawned = false;
        }

        public bool TryDepositItem(int itemIndex)
        {
            if (!IsServer && !IsOfflineLocalMode)
            {
                return false;
            }

            var item = contentDatabase.itemPool[itemIndex];
            var matched = false;
            for (var i = 0; i < objectiveEntries.Count; i++)
            {
                if (objectiveEntries[i].itemIndex != itemIndex || objectiveEntries[i].Complete)
                {
                    continue;
                }

                objectiveEntries[i].depositedCount += 1;
                matched = true;
                break;
            }

            if (!matched)
            {
                var total = 0;
                for (var i = 0; i < objectiveEntries.Count; i++)
                {
                    total += objectiveEntries[i].depositedCount * contentDatabase.itemPool[objectiveEntries[i].itemIndex].quotaValue;
                }

                if (total + item.quotaValue >= quotaValue.Value)
                {
                    matched = true;
                }
            }

            quotaMet.Value = IsQuotaSatisfied();
            StateChanged?.Invoke();
            return matched;
        }

        private bool IsQuotaSatisfied()
        {
            var total = 0;
            for (var i = 0; i < objectiveEntries.Count; i++)
            {
                total += objectiveEntries[i].depositedCount * contentDatabase.itemPool[objectiveEntries[i].itemIndex].quotaValue;
            }

            return total >= quotaValue.Value;
        }

        public void RegisterPlayerDown(ulong clientId)
        {
            if (!IsServer && !IsOfflineLocalMode)
            {
                return;
            }

            downedPlayers.Add(clientId);
            var connectedPlayerCount = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening
                ? NetworkManager.ConnectedClientsIds.Count
                : 1;
            if (downedPlayers.Count >= connectedPlayerCount)
            {
                runResult.Value = RunResult.TeamWipe;
            }
        }

        public void RegisterPlayerRevived(ulong clientId)
        {
            if (!IsServer && !IsOfflineLocalMode)
            {
                return;
            }

            downedPlayers.Remove(clientId);
        }

        public bool CanExtract()
        {
            return quotaMet.Value && runResult.Value == RunResult.InProgress;
        }

        public void CompleteRun()
        {
            if ((IsServer || IsOfflineLocalMode) && CanExtract())
            {
                runResult.Value = RunResult.Success;
            }
        }

        private void OnNetworkStateChanged<T>(T previousValue, T newValue)
        {
            if (contentDatabase != null && seed.Value >= 0 && objectiveEntries.Count == 0)
            {
                BuildObjectiveList(seed.Value);
            }

            if (!IsServer && seed.Value >= 0)
            {
                var bootstrap = FindFirstObjectByType<NightSceneBootstrap>();
                if (bootstrap != null)
                {
                    bootstrap.GenerateStore(seed.Value);
                }
            }

            StateChanged?.Invoke();
        }

        private void OnRunResultChanged(RunResult previousValue, RunResult newValue)
        {
            StateChanged?.Invoke();
        }
    }
}

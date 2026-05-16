using System;
using System.Collections.Generic;
using GroceryQuotaHorror.Data;
using UnityEngine;

namespace GroceryQuotaHorror.Generation
{
    public sealed class StoreGenerator : MonoBehaviour
    {
        [SerializeField] private GameContentDatabase contentDatabase;
        [SerializeField] private Transform chunkRoot;

        private readonly List<GameObject> spawnedChunks = new();
        private readonly List<ItemSpawnMarker> spawnMarkers = new();
        private readonly List<Transform> patrolPoints = new();

        public IReadOnlyList<ItemSpawnMarker> SpawnMarkers => spawnMarkers;
        public IReadOnlyList<Transform> PatrolPoints => patrolPoints;

        public void Configure(GameContentDatabase content, Transform root)
        {
            contentDatabase = content;
            chunkRoot = root;
        }

        public void Generate(int seed)
        {
            Clear();

            if (contentDatabase == null || chunkRoot == null)
            {
                return;
            }

            var random = new System.Random(seed);
            var layout = BuildChunkSequence(random);
            var spacing = GameRuntime.Balance != null ? GameRuntime.Balance.layout.chunkSpacing : 26f;
            var cursor = Vector3.zero;
            for (var i = 0; i < layout.Count; i++)
            {
                var chunkInstance = Instantiate(layout[i].prefab, cursor, Quaternion.identity, chunkRoot);
                chunkInstance.name = $"{i:00}_{layout[i].chunkId}";
                spawnedChunks.Add(chunkInstance);
                CollectMarkers(chunkInstance);
                cursor += Vector3.forward * spacing;
            }
        }

        public void Clear()
        {
            for (var i = spawnedChunks.Count - 1; i >= 0; i--)
            {
                if (spawnedChunks[i] != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(spawnedChunks[i]);
                    }
                    else
                    {
                        DestroyImmediate(spawnedChunks[i]);
                    }
                }
            }

            spawnedChunks.Clear();
            spawnMarkers.Clear();
            patrolPoints.Clear();
        }

        private List<StoreChunkDefinition> BuildChunkSequence(System.Random random)
        {
            var front = FindFirst(RoomType.FrontEntrance);
            var checkout = FindFirst(RoomType.Checkout);
            var loadingDock = FindFirst(RoomType.LoadingDock);
            var middle = new List<StoreChunkDefinition>();

            for (var i = 0; i < contentDatabase.chunkPool.Count; i++)
            {
                var def = contentDatabase.chunkPool[i];
                if (def.roomType == RoomType.FrontEntrance || def.roomType == RoomType.Checkout || def.roomType == RoomType.LoadingDock)
                {
                    continue;
                }

                middle.Add(def);
            }

            var middleCount = GameRuntime.Balance != null ? GameRuntime.Balance.layout.middleChunkCount : 6;
            var sequence = new List<StoreChunkDefinition> { front };
            for (var i = 0; i < middleCount; i++)
            {
                sequence.Add(middle[random.Next(0, middle.Count)]);
            }

            sequence.Add(loadingDock);
            sequence.Add(checkout);
            return sequence;
        }

        private StoreChunkDefinition FindFirst(RoomType type)
        {
            for (var i = 0; i < contentDatabase.chunkPool.Count; i++)
            {
                if (contentDatabase.chunkPool[i].roomType == type)
                {
                    return contentDatabase.chunkPool[i];
                }
            }

            throw new InvalidOperationException($"Missing chunk for room type {type}.");
        }

        private void CollectMarkers(GameObject chunkInstance)
        {
            spawnMarkers.AddRange(chunkInstance.GetComponentsInChildren<ItemSpawnMarker>());
            var patrols = chunkInstance.GetComponentsInChildren<PatrolPoint>();
            for (var i = 0; i < patrols.Length; i++)
            {
                patrolPoints.Add(patrols[i].transform);
            }
        }
    }
}

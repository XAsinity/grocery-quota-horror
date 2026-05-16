using System;
using System.Collections.Generic;
using UnityEngine;

namespace GroceryQuotaHorror.Data
{
    [CreateAssetMenu(menuName = "Grocery Quota Horror/Run Config", fileName = "RunConfig")]
    public sealed class RunConfig : ScriptableObject
    {
        public int minQuotaValue = 12;
        public int maxQuotaValue = 20;
        public float nightLengthSeconds = 420f;
        public int middleChunkCount = 6;
        public int monsterBudget = 3;
        public List<ItemDefinition> itemPool = new();
        public List<MonsterDefinition> monsterPool = new();
        public List<StoreChunkDefinition> chunkPool = new();

        public int GetQuotaForSeed(int seed)
        {
            var random = new System.Random(seed);
            return random.Next(minQuotaValue, maxQuotaValue + 1);
        }
    }

    public enum RoomType
    {
        FrontEntrance,
        RetailAisle,
        Produce,
        Freezer,
        Bakery,
        Stockroom,
        Utility,
        Checkout,
        LoadingDock
    }

    [Flags]
    public enum SpawnZone
    {
        None = 0,
        Front = 1 << 0,
        Retail = 1 << 1,
        Cold = 1 << 2,
        Backroom = 1 << 3,
        Checkout = 1 << 4,
        Utility = 1 << 5
    }

    public enum MonsterArchetype
    {
        Roamer,
        Listener,
        Ambusher
    }
}


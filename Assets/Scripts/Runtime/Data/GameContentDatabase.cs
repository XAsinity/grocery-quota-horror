using System.Collections.Generic;
using GroceryQuotaHorror.Core;
using UnityEngine;

namespace GroceryQuotaHorror.Data
{
    [CreateAssetMenu(menuName = "Grocery Quota Horror/Game Content Database", fileName = "GameContentDatabase")]
    public sealed class GameContentDatabase : ScriptableObject
    {
        public List<ItemDefinition> itemPool = new();
        public List<MonsterDefinition> monsterPool = new();
        public List<StoreChunkDefinition> chunkPool = new();
        public GameObject itemPickupPrefab;
        public GameObject monsterPrefab;
        public NightGameManager nightGameManagerPrefab;
    }
}

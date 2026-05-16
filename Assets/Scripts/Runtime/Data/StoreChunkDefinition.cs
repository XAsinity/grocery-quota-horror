using UnityEngine;

namespace GroceryQuotaHorror.Data
{
    [CreateAssetMenu(menuName = "Grocery Quota Horror/Store Chunk Definition", fileName = "StoreChunkDefinition")]
    public sealed class StoreChunkDefinition : ScriptableObject
    {
        public string chunkId = "front_entrance";
        public RoomType roomType = RoomType.RetailAisle;
        public GameObject prefab;
        public SpawnZone spawnTags = SpawnZone.Retail;
        public bool allowMonsters = true;
        public Color gizmoColor = Color.green;
    }
}


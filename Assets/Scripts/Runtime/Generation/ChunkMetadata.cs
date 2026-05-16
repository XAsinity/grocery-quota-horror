using GroceryQuotaHorror.Data;
using UnityEngine;

namespace GroceryQuotaHorror.Generation
{
    public sealed class ChunkMetadata : MonoBehaviour
    {
        public RoomType roomType;
        public SpawnZone spawnZone;
        public bool allowMonsters = true;
    }
}


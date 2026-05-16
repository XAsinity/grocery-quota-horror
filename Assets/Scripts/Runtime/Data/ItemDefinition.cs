using UnityEngine;

namespace GroceryQuotaHorror.Data
{
    [CreateAssetMenu(menuName = "Grocery Quota Horror/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string itemId = "beans";
        public string displayName = "Canned Beans";
        public int quotaValue = 1;
        public float carryWeight = 1f;
        public SpawnZone allowedZones = SpawnZone.Retail;
        public Color tint = Color.white;
    }
}


using UnityEngine;

namespace GroceryQuotaHorror.Data
{
    [CreateAssetMenu(menuName = "Grocery Quota Horror/Monster Definition", fileName = "MonsterDefinition")]
    public sealed class MonsterDefinition : ScriptableObject
    {
        public string monsterId = "lurker";
        public string displayName = "Aisle Lurker";
        public MonsterArchetype archetype = MonsterArchetype.Roamer;
        public float moveSpeed = 3.5f;
        public float detectionRange = 12f;
        public float chaseRange = 18f;
        public float attackRange = 1.7f;
        public float attackCooldown = 3f;
        public Color tint = Color.red;
    }
}


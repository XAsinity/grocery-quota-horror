using UnityEngine;

namespace GroceryQuotaHorror.Data
{
    [CreateAssetMenu(menuName = "Grocery Quota Horror/Gameplay Tuning", fileName = "GameplayTuning")]
    public sealed class GameplayTuning : ScriptableObject
    {
        [Header("Global Physics")]
        public Vector3 worldGravity = new(0f, -9.81f, 0f);

        [Header("Player")]
        public float playerGravity = -20f;

        [Header("Ragdoll Grab")]
        public float ragdollHoldForce = 45f;
        public float ragdollHoldDamping = 4f;
        public float ragdollHeldLinearDamping = 1.1f;
        public float ragdollHeldAngularDamping = 0.45f;
        public float ragdollHeldVerticalAssist = 0.35f;
        public float ragdollHeldMaxForce = 80f;
    }
}

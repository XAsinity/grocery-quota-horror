using UnityEngine;

namespace GroceryQuotaHorror.Player
{
    public sealed class PlayerImpactCollisionRelay : MonoBehaviour
    {
        [SerializeField] private PlayerController owner;

        public void SetOwner(PlayerController player)
        {
            owner = player;
        }

        private void Awake()
        {
            if (owner == null)
            {
                owner = GetComponentInParent<PlayerController>();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            owner?.HandleExternalImpactCollision(collision, true);
        }
    }
}

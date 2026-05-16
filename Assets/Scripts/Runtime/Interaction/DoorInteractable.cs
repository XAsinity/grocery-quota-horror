using GroceryQuotaHorror.Player;
using UnityEngine;

namespace GroceryQuotaHorror.Interaction
{
    public sealed class DoorInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform hinge;
        [SerializeField] private float openAngle = 100f;

        private bool isOpen;

        public string Prompt => isOpen ? "Close door" : "Open door";

        public void Interact(PlayerController player)
        {
            isOpen = !isOpen;
            if (hinge != null)
            {
                hinge.localRotation = Quaternion.Euler(0f, isOpen ? openAngle : 0f, 0f);
            }
        }
    }
}


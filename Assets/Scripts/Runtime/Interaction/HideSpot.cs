using GroceryQuotaHorror.Player;
using UnityEngine;

namespace GroceryQuotaHorror.Interaction
{
    public sealed class HideSpot : MonoBehaviour, IInteractable
    {
        public string Prompt => "Hide";

        public void Interact(PlayerController player)
        {
            player.ToggleHide(transform.position + Vector3.back * 0.5f);
        }
    }
}


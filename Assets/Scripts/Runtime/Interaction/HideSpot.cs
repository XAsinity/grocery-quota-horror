using GroceryQuotaHorror.Data;
using GroceryQuotaHorror.Player;
using UnityEngine;

namespace GroceryQuotaHorror.Interaction
{
    public sealed class HideSpot : MonoBehaviour, IInteractable
    {
        public string Prompt => "Hide";

        public void Interact(PlayerController player)
        {
            var hideOffset = GameRuntime.Balance != null ? GameRuntime.Balance.interaction.hideOffset : 0.5f;
            player.ToggleHide(transform.position + Vector3.back * hideOffset);
        }
    }
}

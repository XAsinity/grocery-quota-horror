using GroceryQuotaHorror.Core;
using GroceryQuotaHorror.Player;
using UnityEngine;

namespace GroceryQuotaHorror.Interaction
{
    public sealed class DepositZone : MonoBehaviour, IInteractable
    {
        public string Prompt => "Deposit item";

        public void Interact(PlayerController player)
        {
            player.TryDepositHeldItem();
        }
    }
}


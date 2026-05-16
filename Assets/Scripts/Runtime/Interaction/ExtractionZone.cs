using GroceryQuotaHorror.Core;
using GroceryQuotaHorror.Player;
using UnityEngine;

namespace GroceryQuotaHorror.Interaction
{
    public sealed class ExtractionZone : MonoBehaviour, IInteractable
    {
        public string Prompt => NightGameManager.Instance != null && NightGameManager.Instance.CanExtract() ? "Extract" : "Quota locked";

        public void Interact(PlayerController player)
        {
            if (NightGameManager.Instance != null && NightGameManager.Instance.CanExtract())
            {
                NightGameManager.Instance.CompleteRun();
            }
        }
    }
}


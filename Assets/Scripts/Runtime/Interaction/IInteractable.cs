namespace GroceryQuotaHorror.Interaction
{
    public interface IInteractable
    {
        string Prompt { get; }
        void Interact(Player.PlayerController player);
    }
}


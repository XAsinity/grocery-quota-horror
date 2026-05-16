using System.Threading.Tasks;
using Unity.Netcode;

namespace GroceryQuotaHorror.Networking
{
    public interface SessionService
    {
        Task<string> StartHostAsync(bool useRelay);
        Task<bool> StartClientAsync(string joinCode, bool useRelay);
        void Shutdown();
    }
}


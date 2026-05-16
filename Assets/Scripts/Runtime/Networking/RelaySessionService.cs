using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace GroceryQuotaHorror.Networking
{
    public sealed class RelaySessionService : SessionService
    {
        private readonly NetworkManager networkManager;
        private readonly UnityTransport transport;

        public RelaySessionService(NetworkManager manager, UnityTransport utp)
        {
            networkManager = manager;
            transport = utp;
        }

        public async Task<string> StartHostAsync(bool useRelay)
        {
            if (!useRelay)
            {
                networkManager.StartHost();
                return "LOCAL";
            }

            await EnsureServicesAsync();
            var allocation = await RelayService.Instance.CreateAllocationAsync(3);
            var code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
            networkManager.StartHost();
            return code;
        }

        public async Task<bool> StartClientAsync(string joinCode, bool useRelay)
        {
            if (!useRelay)
            {
                transport.SetConnectionData("127.0.0.1", (ushort)7777);
                return networkManager.StartClient();
            }

            await EnsureServicesAsync();
            var allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            transport.SetRelayServerData(allocation.ToRelayServerData("dtls"));
            return networkManager.StartClient();
        }

        public void Shutdown()
        {
            if (networkManager.IsListening)
            {
                networkManager.Shutdown();
            }
        }

        private static async Task EnsureServicesAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }
    }
}

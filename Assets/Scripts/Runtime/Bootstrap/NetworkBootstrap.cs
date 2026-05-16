using System;
using GroceryQuotaHorror.Networking;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GroceryQuotaHorror.Bootstrap
{
    public sealed class NetworkBootstrap : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;

        private SessionService sessionService;
        private string joinCode = string.Empty;
        private string lastStatus = "Choose a session mode.";

        private void Start()
        {
            if (networkManager == null)
            {
                networkManager = FindFirstObjectByType<NetworkManager>();
            }

            DontDestroyOnLoad(networkManager.gameObject);
            var transport = networkManager.GetComponent<UnityTransport>();
            sessionService = new RelaySessionService(networkManager, transport);
        }

        private void OnGUI()
        {
            if (SceneManager.GetActiveScene().name != "Bootstrap")
            {
                return;
            }

            GUILayout.BeginArea(new Rect(24f, 24f, 360f, 320f), GUI.skin.window);
            GUILayout.Label("Grocery Quota Horror");
            GUILayout.Label("Huge store. Bad monsters. Grab quota and get out.");
            GUILayout.Space(8f);

            if (!networkManager.IsListening)
            {
                if (GUILayout.Button("Host Local"))
                {
                    _ = StartHostAsync(false);
                }

                if (GUILayout.Button("Host Relay"))
                {
                    _ = StartHostAsync(true);
                }

                joinCode = GUILayout.TextField(joinCode);
                if (GUILayout.Button("Join Relay"))
                {
                    _ = StartClientAsync(joinCode, true);
                }

                if (GUILayout.Button("Join Local"))
                {
                    _ = StartClientAsync("LOCAL", false);
                }
            }
            else
            {
                GUILayout.Label($"Session: {lastStatus}");
                if (networkManager.IsHost && GUILayout.Button("Begin Night"))
                {
                    networkManager.SceneManager.LoadScene("SupermarketNight", LoadSceneMode.Single);
                }

                if (GUILayout.Button("Shutdown"))
                {
                    sessionService.Shutdown();
                }
            }

            GUILayout.Space(10f);
            GUILayout.Label(lastStatus);
            GUILayout.EndArea();
        }

        private async System.Threading.Tasks.Task StartHostAsync(bool useRelay)
        {
            try
            {
                var code = await sessionService.StartHostAsync(useRelay);
                lastStatus = useRelay ? $"Relay code: {code}" : "Hosting local on 127.0.0.1:7777";
            }
            catch (Exception ex)
            {
                lastStatus = ex.Message;
            }
        }

        private async System.Threading.Tasks.Task StartClientAsync(string code, bool useRelay)
        {
            try
            {
                if (await sessionService.StartClientAsync(code, useRelay))
                {
                    lastStatus = useRelay ? $"Joined relay {code}" : "Joined local host";
                }
                else
                {
                    lastStatus = "Join failed.";
                }
            }
            catch (Exception ex)
            {
                lastStatus = ex.Message;
            }
        }
    }
}

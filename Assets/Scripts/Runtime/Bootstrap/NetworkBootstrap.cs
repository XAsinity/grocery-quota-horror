using System;
using GroceryQuotaHorror.Data;
using GroceryQuotaHorror.Networking;
using GroceryQuotaHorror.Player;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GroceryQuotaHorror.Bootstrap
{
    public sealed class NetworkBootstrap : MonoBehaviour
    {
        private const string BootstrapSceneName = "Bootstrap";
        private const string SupermarketSceneName = "SupermarketNight";
        private const string PrototypeSceneName = "TestPrototype";
        private const float OfflinePlayerSpawnHeight = 1.5f;

        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private GameBalanceProfile balanceProfile;
        [SerializeField] private GameContentDatabase contentDatabase;

        public static bool LocalOfflineMode { get; private set; }

        private SessionService sessionService;
        private string joinCode = string.Empty;
        private string lastStatus = "Choose a session mode.";
        private string pendingOfflineSceneName = string.Empty;
        private float pendingOfflineSpawnDeadline = -1f;

        private void Start()
        {
            GameRuntime.SetRuntimeAssets(balanceProfile, contentDatabase);
            if (networkManager == null)
            {
                networkManager = FindFirstObjectByType<NetworkManager>();
            }

            if (networkManager != null)
            {
                DontDestroyOnLoad(networkManager.gameObject);
                var transport = networkManager.GetComponent<UnityTransport>();
                if (transport != null)
                {
                    sessionService = new RelaySessionService(networkManager, transport);
                }
            }

            SceneManager.sceneLoaded += OnUnitySceneLoaded;
            Debug.Log("[OfflineSpawn] NetworkBootstrap started and sceneLoaded listener attached.");
        }

        private void OnDestroy()
        {
            if (networkManager != null && networkManager.SceneManager != null)
            {
                networkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
            }

            SceneManager.sceneLoaded -= OnUnitySceneLoaded;
        }

        private void OnGUI()
        {
            if (SceneManager.GetActiveScene().name != BootstrapSceneName)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(24f, 24f, 380f, 380f), GUI.skin.window);
            GUILayout.Label("Grocery Quota Horror");
            GUILayout.Label("Huge store. Bad monsters. Grab quota and get out.");
            GUILayout.Space(8f);

            if (GUILayout.Button("Play Night Local"))
            {
                LoadLocalScene(SupermarketSceneName);
            }

            if (GUILayout.Button("Open Prototype Local"))
            {
                OpenPrototypeLocal();
            }

            GUILayout.Space(12f);
            GUILayout.Label("Network Session");

            if (networkManager == null)
            {
                GUILayout.Label("No NetworkManager found. Local scene buttons still work.");
            }
            else if (!networkManager.IsListening)
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
                    TryLoadGameplayScene(SupermarketSceneName);
                }

                if (networkManager.IsHost && GUILayout.Button("Open Prototype"))
                {
                    TryLoadGameplayScene(PrototypeSceneName);
                }

                if (GUILayout.Button("Shutdown"))
                {
                    sessionService?.Shutdown();
                    LocalOfflineMode = false;
                    lastStatus = "Session shut down.";
                }
            }

            GUILayout.Space(10f);
            GUILayout.Label(lastStatus);
            GUILayout.EndArea();
        }

        private void Update()
        {
            if (!LocalOfflineMode || string.IsNullOrEmpty(pendingOfflineSceneName))
            {
                return;
            }

            var activeScene = SceneManager.GetActiveScene().name;
            if (activeScene != pendingOfflineSceneName)
            {
                return;
            }

            if (FindFirstObjectByType<PlayerController>() != null)
            {
                pendingOfflineSceneName = string.Empty;
                pendingOfflineSpawnDeadline = -1f;
                return;
            }

            if (pendingOfflineSpawnDeadline > 0f && Time.unscaledTime >= pendingOfflineSpawnDeadline)
            {
                TrySpawnOfflinePlayer(activeScene, "update-fallback");
            }
        }

        private async System.Threading.Tasks.Task StartHostAsync(bool useRelay)
        {
            try
            {
                if (networkManager == null || sessionService == null)
                {
                    lastStatus = "NetworkManager or transport is missing.";
                    return;
                }

                LocalOfflineMode = false;
                var code = await sessionService.StartHostAsync(useRelay);
                SubscribeToNetworkSceneEvents();
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
                if (networkManager == null || sessionService == null)
                {
                    lastStatus = "NetworkManager or transport is missing.";
                    return;
                }

                LocalOfflineMode = false;
                if (await sessionService.StartClientAsync(code, useRelay))
                {
                    SubscribeToNetworkSceneEvents();
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

        private void TryLoadGameplayScene(string sceneName)
        {
            if (networkManager == null || !networkManager.IsHost || networkManager.SceneManager == null)
            {
                lastStatus = $"Can't open {sceneName}: host session is not running.";
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                lastStatus = $"Scene '{sceneName}' is not in Build Settings.";
                return;
            }

            var status = networkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            if (status == SceneEventProgressStatus.Started)
            {
                lastStatus = $"Loading {sceneName}...";
                return;
            }

            if (networkManager.ConnectedClientsIds.Count <= 1)
            {
                lastStatus = $"NGO scene load returned {status}. Falling back to direct scene load for local testing.";
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                return;
            }

            lastStatus = $"Couldn't load {sceneName}: {status}.";
        }

        private void LoadLocalScene(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                lastStatus = $"Scene '{sceneName}' is not in Build Settings.";
                return;
            }

            LocalOfflineMode = true;
            pendingOfflineSceneName = sceneName;
            pendingOfflineSpawnDeadline = Time.unscaledTime + 0.25f;
            if (networkManager != null && networkManager.IsListening)
            {
                sessionService?.Shutdown();
            }

            lastStatus = $"Loading {sceneName} locally...";
            Debug.Log($"[OfflineSpawn] Requesting local scene load for '{sceneName}'.");
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        public void OpenPrototypeLocal()
        {
            LoadLocalScene(PrototypeSceneName);
        }

        private void SubscribeToNetworkSceneEvents()
        {
            if (networkManager?.SceneManager == null)
            {
                return;
            }

            networkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
            networkManager.SceneManager.OnSceneEvent += OnSceneEvent;
        }

        private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"[OfflineSpawn] sceneLoaded fired for '{scene.name}' mode={mode} localOffline={LocalOfflineMode}.");
            if (!LocalOfflineMode)
            {
                return;
            }

            TrySpawnOfflinePlayer(scene.name, "sceneLoaded");
        }

        private void TrySpawnOfflinePlayer(string sceneName, string source)
        {
            if (sceneName != SupermarketSceneName && sceneName != PrototypeSceneName)
            {
                return;
            }

            if (FindFirstObjectByType<PlayerController>() != null)
            {
                Debug.Log($"[OfflineSpawn] Player already exists in '{sceneName}' during {source}, skipping spawn.");
                pendingOfflineSceneName = string.Empty;
                pendingOfflineSpawnDeadline = -1f;
                return;
            }

            var playerPrefab = networkManager != null ? networkManager.NetworkConfig.PlayerPrefab : null;
            if (playerPrefab == null && GameRuntime.Content != null)
            {
                playerPrefab = networkManager != null ? networkManager.NetworkConfig.PlayerPrefab : null;
            }
            if (playerPrefab == null)
            {
                Debug.LogWarning($"[OfflineSpawn] No player prefab available during {source}. NetworkManager present={networkManager != null}");
                lastStatus = $"Loaded {sceneName}, but no player prefab was available.";
                return;
            }

            var spawnPosition = sceneName == PrototypeSceneName
                ? new Vector3(0f, OfflinePlayerSpawnHeight, 205f)
                : new Vector3(0f, OfflinePlayerSpawnHeight, -6f);
            var playerInstance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            var networkTransform = playerInstance.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (LocalOfflineMode && networkTransform != null)
            {
                networkTransform.enabled = false;
            }

            Debug.Log($"[OfflineSpawn] Spawned player prefab '{playerPrefab.name}' in scene '{sceneName}' from {source} at {spawnPosition}. Root world position: {playerInstance.transform.position}");

            var cameras = playerInstance.GetComponentsInChildren<Camera>(true);
            for (var i = 0; i < cameras.Length; i++)
            {
                var cameraTransform = cameras[i].transform;
                Debug.Log($"[OfflineSpawn] Player camera candidate '{cameras[i].name}' world={cameraTransform.position} local={cameraTransform.localPosition} parent='{(cameraTransform.parent != null ? cameraTransform.parent.name : "<none>")}' enabled={cameras[i].enabled}");
            }

            pendingOfflineSceneName = string.Empty;
            pendingOfflineSpawnDeadline = -1f;
            lastStatus = $"Loaded {sceneName} locally.";
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            switch (sceneEvent.SceneEventType)
            {
                case SceneEventType.Load:
                    lastStatus = $"Scene load started: {sceneEvent.SceneName}";
                    break;
                case SceneEventType.LoadComplete:
                    lastStatus = $"Scene loaded: {sceneEvent.SceneName}";
                    break;
                case SceneEventType.LoadEventCompleted:
                    lastStatus = $"All clients finished loading {sceneEvent.SceneName}.";
                    break;
            }
        }
    }
}

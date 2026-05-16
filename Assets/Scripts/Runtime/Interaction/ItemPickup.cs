using GroceryQuotaHorror.Core;
using GroceryQuotaHorror.Data;
using GroceryQuotaHorror.Player;
using Unity.Netcode;
using UnityEngine;

namespace GroceryQuotaHorror.Interaction
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ItemPickup : NetworkBehaviour, IInteractable
    {
        [SerializeField] private MeshRenderer meshRenderer;

        private readonly NetworkVariable<int> definitionIndex = new(-1);
        private readonly NetworkVariable<ulong> holderId = new(ulong.MaxValue);

        private RunConfig runConfig;
        private Rigidbody body;

        public string Prompt => holderId.Value == ulong.MaxValue ? "Pick up" : string.Empty;
        public int DefinitionIndex => definitionIndex.Value;

        public void Initialize(int itemIndex, RunConfig config)
        {
            runConfig = config;
            definitionIndex.Value = itemIndex;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (meshRenderer == null)
            {
                meshRenderer = GetComponentInChildren<MeshRenderer>();
            }
        }

        public override void OnNetworkSpawn()
        {
            if (runConfig == null && NightGameManager.Instance != null)
            {
                runConfig = NightGameManager.Instance.RunConfig;
            }

            ApplyVisuals();
        }

        private void Update()
        {
            if (!IsServer || holderId.Value == ulong.MaxValue)
            {
                return;
            }

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(holderId.Value, out var networkObject))
            {
                return;
            }

            var player = networkObject.GetComponent<PlayerController>();
            if (player == null)
            {
                return;
            }

            var anchor = player.transform.position + player.transform.forward * 1.1f + Vector3.up * 1.1f;
            transform.SetPositionAndRotation(anchor, Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f));
        }

        public void Interact(PlayerController player)
        {
            player.RequestPickup(this);
        }

        [ServerRpc(RequireOwnership = false)]
        public void PickupServerRpc(ulong playerId)
        {
            holderId.Value = playerId;
            body.isKinematic = true;
        }

        [ServerRpc(RequireOwnership = false)]
        public void DropServerRpc(Vector3 position)
        {
            holderId.Value = ulong.MaxValue;
            body.isKinematic = false;
            transform.position = position;
        }

        public void Consume()
        {
            if (IsServer && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }

        private void ApplyVisuals()
        {
            if (runConfig == null || definitionIndex.Value < 0 || definitionIndex.Value >= runConfig.itemPool.Count || meshRenderer == null)
            {
                return;
            }

            meshRenderer.sharedMaterial.color = runConfig.itemPool[definitionIndex.Value].tint;
        }
    }
}

using GroceryQuotaHorror.Bootstrap;
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

        private GameContentDatabase contentDatabase;
        private Rigidbody body;
        private PlayerController localHolder;

        private bool IsOfflineLocalMode => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkBootstrap.LocalOfflineMode;

        public string Prompt => holderId.Value == ulong.MaxValue && localHolder == null ? "Pick up" : string.Empty;
        public int DefinitionIndex => definitionIndex.Value;

        public void Initialize(int itemIndex, GameContentDatabase content)
        {
            contentDatabase = content;
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
            if (contentDatabase == null)
            {
                contentDatabase = NightGameManager.Instance != null ? NightGameManager.Instance.ContentDatabase : GameRuntime.Content;
            }

            ApplyVisuals();
        }

        private void Update()
        {
            if ((!IsServer && !IsOfflineLocalMode) || (holderId.Value == ulong.MaxValue && localHolder == null) || GameRuntime.Balance == null)
            {
                return;
            }

            var player = localHolder;
            if (player == null)
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(holderId.Value, out var networkObject))
                {
                    return;
                }

                player = networkObject.GetComponent<PlayerController>();
                if (player == null)
                {
                    return;
                }
            }

            var interaction = GameRuntime.Balance.interaction;
            var anchor = player.transform.position + player.transform.forward * interaction.heldItemForwardOffset + Vector3.up * interaction.heldItemUpOffset;
            transform.SetPositionAndRotation(anchor, Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f));
        }

        public void Interact(PlayerController player)
        {
            player.RequestPickup(this);
        }

        public void LocalPickup(PlayerController player)
        {
            localHolder = player;
            holderId.Value = ulong.MaxValue;
            body.isKinematic = true;
        }

        public void LocalDrop(Vector3 position)
        {
            localHolder = null;
            holderId.Value = ulong.MaxValue;
            body.isKinematic = false;
            transform.position = position;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void PickupServerRpc(ulong playerId)
        {
            localHolder = null;
            holderId.Value = playerId;
            body.isKinematic = true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void DropServerRpc(Vector3 position)
        {
            localHolder = null;
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
            else if (IsOfflineLocalMode)
            {
                Destroy(gameObject);
            }
        }

        private void ApplyVisuals()
        {
            if (contentDatabase == null || definitionIndex.Value < 0 || definitionIndex.Value >= contentDatabase.itemPool.Count || meshRenderer == null)
            {
                return;
            }

            meshRenderer.sharedMaterial.color = contentDatabase.itemPool[definitionIndex.Value].tint;
        }
    }
}

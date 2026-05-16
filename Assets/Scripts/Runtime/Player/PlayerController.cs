using GroceryQuotaHorror.Core;
using GroceryQuotaHorror.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace GroceryQuotaHorror.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerController : NetworkBehaviour, IInteractable
    {
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float sprintMultiplier = 1.6f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float interactRange = 3.2f;
        [SerializeField] private Light flashlight;
        [SerializeField] private Renderer[] bodyRenderers;

        private readonly NetworkVariable<bool> isDowned = new();
        private readonly NetworkVariable<bool> isHidden = new();

        private CharacterController controller;
        private Camera playerCamera;
        private Vector3 velocity;
        private ItemPickup heldItem;

        public bool IsDowned => isDowned.Value;
        public bool IsHidden => isHidden.Value;
        public ItemPickup HeldItem => heldItem;
        public string Prompt => IsDowned ? "Revive teammate" : string.Empty;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                playerCamera = Camera.main;
                if (playerCamera != null)
                {
                    playerCamera.transform.SetParent(transform);
                    playerCamera.transform.localPosition = new Vector3(0f, 1.5f, -2.6f);
                    playerCamera.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
                }
            }

            ApplyVisualState();
            isHidden.OnValueChanged += OnHiddenChanged;
            isDowned.OnValueChanged += OnDownedChanged;
        }

        public override void OnNetworkDespawn()
        {
            isHidden.OnValueChanged -= OnHiddenChanged;
            isDowned.OnValueChanged -= OnDownedChanged;
        }

        private void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.F) && flashlight != null)
            {
                flashlight.enabled = !flashlight.enabled;
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                DropHeldItem();
            }

            if (IsDowned)
            {
                return;
            }

            Move();

            if (Input.GetKeyDown(KeyCode.E))
            {
                TryInteract();
            }
        }

        private void Move()
        {
            var horizontal = Input.GetAxisRaw("Horizontal");
            var vertical = Input.GetAxisRaw("Vertical");
            var input = new Vector3(horizontal, 0f, vertical).normalized;
            var speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);
            var world = transform.TransformDirection(input);
            controller.Move(world * speed * Time.deltaTime);
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
            if (controller.isGrounded && velocity.y < 0f)
            {
                velocity.y = -1f;
            }

            var mouse = Input.GetAxis("Mouse X");
            transform.Rotate(0f, mouse * 120f * Time.deltaTime, 0f);
        }

        private void TryInteract()
        {
            if (!Physics.Raycast(transform.position + Vector3.up * 1.2f, transform.forward, out var hit, interactRange))
            {
                return;
            }

            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            interactable?.Interact(this);
        }

        public void RequestPickup(ItemPickup pickup)
        {
            if (heldItem != null || pickup == null)
            {
                return;
            }

            heldItem = pickup;
            pickup.PickupServerRpc(NetworkObjectId);
        }

        public void TryDepositHeldItem()
        {
            if (heldItem == null || NightGameManager.Instance == null)
            {
                return;
            }

            DepositHeldItemServerRpc(heldItem.NetworkObjectId, heldItem.DefinitionIndex);
            heldItem = null;
        }

        [ServerRpc]
        private void DepositHeldItemServerRpc(ulong itemObjectId, int definitionIndex)
        {
            if (!NightGameManager.Instance.TryDepositItem(definitionIndex))
            {
                return;
            }

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemObjectId, out var itemObject))
            {
                itemObject.GetComponent<ItemPickup>()?.Consume();
            }
        }

        public void DropHeldItem()
        {
            if (heldItem == null)
            {
                return;
            }

            heldItem.DropServerRpc(transform.position + transform.forward * 1.4f);
            heldItem = null;
        }

        public void ToggleHide(Vector3 hidePosition)
        {
            if (isHidden.Value)
            {
                SetHiddenServerRpc(false, transform.position);
            }
            else
            {
                SetHiddenServerRpc(true, hidePosition);
            }
        }

        [ServerRpc]
        private void SetHiddenServerRpc(bool hidden, Vector3 targetPosition)
        {
            isHidden.Value = hidden;
            transform.position = targetPosition;
        }

        public void ApplyDamage()
        {
            if (IsServer && !isDowned.Value)
            {
                isDowned.Value = true;
                NightGameManager.Instance?.RegisterPlayerDown(OwnerClientId);
            }
        }

        public void Revive()
        {
            if (IsDowned)
            {
                ReviveServerRpc();
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ReviveServerRpc()
        {
            isDowned.Value = false;
            NightGameManager.Instance?.RegisterPlayerRevived(OwnerClientId);
        }

        private void ApplyVisualState()
        {
            var visible = !isHidden.Value;
            for (var i = 0; i < bodyRenderers.Length; i++)
            {
                bodyRenderers[i].enabled = visible;
            }
        }

        private void OnHiddenChanged(bool previousValue, bool newValue)
        {
            ApplyVisualState();
        }

        private void OnDownedChanged(bool previousValue, bool newValue)
        {
            ApplyVisualState();
        }

        public void Interact(PlayerController player)
        {
            if (IsDowned && player != this)
            {
                Revive();
            }
        }
    }
}

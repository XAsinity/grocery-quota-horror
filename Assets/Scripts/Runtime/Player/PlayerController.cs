using System.Collections.Generic;
using GroceryQuotaHorror.Core;
using GroceryQuotaHorror.Data;
using GroceryQuotaHorror.Interaction;
using GroceryQuotaHorror.Physics;
using Unity.Netcode;
using UnityEngine;

namespace GroceryQuotaHorror.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerController : NetworkBehaviour, IInteractable
    {
        [SerializeField] private Light flashlight;
        [SerializeField] private Renderer[] bodyRenderers;
        [SerializeField] private SpawnableRagdoll spawnableRagdollPrefab;
        [SerializeField] private ActiveRagdollController activeRagdoll;

        private readonly NetworkVariable<bool> isDowned = new();
        private readonly NetworkVariable<bool> isHidden = new();

        private CharacterController controller;
        private Camera playerCamera;
        private Vector3 velocity;
        private Vector3 smoothedMoveInput;
        private ItemPickup heldItem;
        private Rigidbody grabbedBody;
        private SpawnableRagdoll grabbedRagdoll;
        private Vector3 lastGrabbedTargetPosition;
        private Vector3 grabbedTargetVelocity;
        private float cameraPitch;
        private Vector2 pendingMouseDelta;
        private Vector3 limpCameraVelocity;
        private readonly List<Collider> ignoredProjectileColliders = new();
        private Vector3 desiredMoveInput;
        private bool wantsSprint;

        public bool IsDowned => isDowned.Value;
        public bool IsHidden => isHidden.Value;
        public ItemPickup HeldItem => heldItem;
        public string Prompt => IsDowned ? "Revive teammate" : string.Empty;

        private bool HasLocalAuthority => !IsSpawned || IsOwner;
        private GameBalanceProfile Balance => GameRuntime.Balance;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (activeRagdoll == null)
            {
                activeRagdoll = GetComponent<ActiveRagdollController>();
            }
        }

        private void Start()
        {
            if (!IsSpawned)
            {
                SetupLocalPlayer();
            }
        }

        private void OnEnable()
        {
            GameRuntime.RuntimeSettingsChanged += OnRuntimeSettingsChanged;
        }

        private void OnDisable()
        {
            GameRuntime.RuntimeSettingsChanged -= OnRuntimeSettingsChanged;
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                SetupLocalPlayer();
            }

            ApplyVisualState();
            isHidden.OnValueChanged += OnHiddenChanged;
            isDowned.OnValueChanged += OnDownedChanged;
        }

        public override void OnNetworkDespawn()
        {
            isHidden.OnValueChanged -= OnHiddenChanged;
            isDowned.OnValueChanged -= OnDownedChanged;

            if (IsOwner)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Update()
        {
            if (!HasLocalAuthority || Balance == null)
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

            UpdateCursorState();

            if (Input.GetKeyDown(KeyCode.B))
            {
                SpawnPhysicsBall();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ToggleBodyState();
            }

            if (IsDowned)
            {
                ReleaseGrabbedBody(false);
                if (activeRagdoll != null && activeRagdoll.State != BodyDriveState.Downed)
                {
                    activeRagdoll.SetState(BodyDriveState.Downed);
                }

                return;
            }

            CaptureMoveAndLookInput();

            if (activeRagdoll != null && activeRagdoll.IsAvailable && activeRagdoll.State == BodyDriveState.Limp)
            {
                ReleaseGrabbedBody(false);
                UpdateLimpLookInfluence();
                return;
            }

            MoveAndLook();
            HandleRagdollSpawnInput();
            HandleRagdollGrabInput();

            if (Input.GetKeyDown(KeyCode.E))
            {
                TryInteract();
            }
        }

        private void FixedUpdate()
        {
            if (!HasLocalAuthority || Balance == null)
            {
                return;
            }

            if (activeRagdoll != null && activeRagdoll.IsAvailable)
            {
                var normalizedMove = Vector3.ClampMagnitude(smoothedMoveInput, 1f);
                activeRagdoll.UpdateSupportedMotion(
                    normalizedMove,
                    normalizedMove.magnitude,
                    wantsSprint && normalizedMove.sqrMagnitude > 0.01f,
                    controller != null && controller.isGrounded,
                    cameraPitch);
            }

            if (grabbedBody == null || playerCamera == null)
            {
                return;
            }

            var interaction = Balance.interaction;
            var ragdoll = Balance.ragdoll;
            var targetPosition = playerCamera.transform.position + playerCamera.transform.forward * interaction.grabbedHoldDistance;
            grabbedTargetVelocity = (targetPosition - lastGrabbedTargetPosition) / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            lastGrabbedTargetPosition = targetPosition;

            var toTarget = targetPosition - grabbedBody.worldCenterOfMass;
            var desiredAcceleration = new Vector3(toTarget.x, toTarget.y * ragdoll.ragdollHeldVerticalAssist, toTarget.z);
            var force = desiredAcceleration * ragdoll.ragdollHoldForce - grabbedBody.linearVelocity * ragdoll.ragdollHoldDamping;
            grabbedBody.AddForce(Vector3.ClampMagnitude(force, ragdoll.ragdollHeldMaxForce), ForceMode.Acceleration);
            grabbedBody.angularVelocity *= 0.94f;
        }

        private void LateUpdate()
        {
            if (!HasLocalAuthority || playerCamera == null || Balance == null)
            {
                return;
            }

            if (activeRagdoll != null && activeRagdoll.IsAvailable && activeRagdoll.State == BodyDriveState.Limp)
            {
                var head = activeRagdoll.HeadTransform;
                if (head != null)
                {
                    var sharpness = Mathf.Max(0.01f, Balance.playerCamera.limpFollowPositionSharpness);
                    var rotationSharpness = Mathf.Max(0.01f, Balance.playerCamera.limpFollowRotationSharpness);
                    playerCamera.transform.position = Vector3.SmoothDamp(
                        playerCamera.transform.position,
                        head.position,
                        ref limpCameraVelocity,
                        1f / sharpness);
                    playerCamera.transform.rotation = Quaternion.Slerp(
                        playerCamera.transform.rotation,
                        head.rotation,
                        1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
                }
            }
        }

        private void CaptureMoveAndLookInput()
        {
            desiredMoveInput = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical")).normalized;
            wantsSprint = Input.GetKey(KeyCode.LeftShift);
            pendingMouseDelta += new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        }

        private void MoveAndLook()
        {
            var movement = Balance.playerMovement;
            smoothedMoveInput = Vector3.Lerp(smoothedMoveInput, desiredMoveInput, 1f - Mathf.Exp(-movement.movementSmoothing * Time.deltaTime));

            var speed = movement.moveSpeed * (wantsSprint ? movement.sprintMultiplier : 1f);
            var world = transform.TransformDirection(smoothedMoveInput);
            controller.Move(world * speed * Time.deltaTime);
            velocity.y += movement.playerGravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
            if (controller.isGrounded && velocity.y < 0f)
            {
                velocity.y = -1f;
            }

            var mouseX = pendingMouseDelta.x * movement.lookSensitivity;
            var mouseY = pendingMouseDelta.y * movement.lookSensitivity;
            pendingMouseDelta = Vector2.zero;
            transform.Rotate(0f, mouseX * movement.mouseTurnRate * Time.deltaTime, 0f);

            cameraPitch = Mathf.Clamp(cameraPitch - mouseY * movement.mouseTurnRate * Time.deltaTime, -movement.maxLookPitch, movement.maxLookPitch);
            if (playerCamera != null)
            {
                playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
            }
        }

        private void UpdateCursorState()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (Input.GetMouseButtonDown(0))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void SetupLocalPlayer()
        {
            GameRuntime.ApplyGlobalPhysics();
            playerCamera = GetComponentInChildren<Camera>(true);
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            if (playerCamera != null)
            {
                playerCamera.tag = "MainCamera";
                playerCamera.enabled = true;
                var listener = playerCamera.GetComponent<AudioListener>();
                if (listener != null)
                {
                    listener.enabled = true;
                }

                var cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                for (var i = 0; i < cameras.Length; i++)
                {
                    if (cameras[i] == playerCamera)
                    {
                        continue;
                    }

                    cameras[i].enabled = false;
                    var otherListener = cameras[i].GetComponent<AudioListener>();
                    if (otherListener != null)
                    {
                        otherListener.enabled = false;
                    }
                }

                playerCamera.transform.SetParent(transform, false);
                playerCamera.transform.localPosition = Balance.playerCamera.supportedLocalOffset;
                playerCamera.transform.localRotation = Quaternion.identity;
            }

            if (activeRagdoll != null && activeRagdoll.IsAvailable)
            {
                activeRagdoll.SetState(BodyDriveState.Supported, true);
            }

            ApplyVisualState();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void ToggleBodyState()
        {
            if (activeRagdoll == null)
            {
                return;
            }

            if (!activeRagdoll.IsAvailable)
            {
                Debug.LogWarning("Active ragdoll is not available on this player, so the ragdoll toggle was ignored.", this);
                return;
            }

            var wasLimp = activeRagdoll.State == BodyDriveState.Limp;
            activeRagdoll.ToggleLimpState();
            controller.enabled = activeRagdoll.State != BodyDriveState.Limp;
            if (wasLimp && playerCamera != null)
            {
                playerCamera.transform.SetParent(transform, false);
                playerCamera.transform.localPosition = Balance.playerCamera.supportedLocalOffset;
                playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
                limpCameraVelocity = Vector3.zero;
            }
        }

        private void UpdateLimpLookInfluence()
        {
            if (activeRagdoll == null || Balance == null)
            {
                return;
            }

            var movement = Balance.playerMovement;
            var mouseX = pendingMouseDelta.x * movement.lookSensitivity;
            var mouseY = pendingMouseDelta.y * movement.lookSensitivity;
            pendingMouseDelta = Vector2.zero;
            activeRagdoll.ApplyHeadLookInfluence(mouseX, mouseY);
        }

        private void HandleRagdollSpawnInput()
        {
            if (!Input.GetKeyDown(KeyCode.P) || spawnableRagdollPrefab == null || Balance == null)
            {
                return;
            }

            var forward = playerCamera != null ? playerCamera.transform.forward : transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = transform.forward;
            }

            forward.Normalize();
            var spawnPosition = transform.position + Vector3.up * Balance.ragdoll.spawnVerticalOffset + forward * Balance.ragdoll.spawnDistance;
            var spawnRotation = Quaternion.LookRotation(forward, Vector3.up);
            Instantiate(spawnableRagdollPrefab, spawnPosition, spawnRotation);
        }

        private void SpawnPhysicsBall()
        {
            if (playerCamera == null || Balance == null)
            {
                return;
            }

            var projectile = Balance.projectile;
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "PhysicsBall";
            ball.transform.position = playerCamera.transform.position + playerCamera.transform.forward * projectile.spawnDistance;
            ball.transform.localScale = Vector3.one * projectile.scale;

            var rigidbody = ball.AddComponent<Rigidbody>();
            rigidbody.mass = projectile.mass;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            var projectileCollider = ball.GetComponent<Collider>();

            ignoredProjectileColliders.Clear();
            ignoredProjectileColliders.AddRange(GetComponentsInChildren<Collider>(true));
            for (var i = 0; i < ignoredProjectileColliders.Count; i++)
            {
                if (projectileCollider != null && ignoredProjectileColliders[i] != null)
                {
                    UnityEngine.Physics.IgnoreCollision(projectileCollider, ignoredProjectileColliders[i], true);
                }
            }

            rigidbody.AddForce(playerCamera.transform.forward * projectile.speed, ForceMode.Impulse);
            Destroy(ball, projectile.lifetime);
        }

        private void HandleRagdollGrabInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                TryStartGrab();
            }

            if (grabbedBody != null && !Input.GetMouseButton(0))
            {
                ReleaseGrabbedBody(true);
            }
        }

        private void TryStartGrab()
        {
            if (playerCamera == null || Balance == null)
            {
                return;
            }

            if (!UnityEngine.Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out var hit, Balance.interaction.interactRange))
            {
                return;
            }

            var body = hit.rigidbody;
            var ragdoll = body != null ? body.GetComponentInParent<SpawnableRagdoll>() : null;
            if (body == null || ragdoll == null)
            {
                return;
            }

            grabbedBody = body;
            grabbedRagdoll = ragdoll;
            lastGrabbedTargetPosition = playerCamera.transform.position + playerCamera.transform.forward * Balance.interaction.grabbedHoldDistance;
            grabbedTargetVelocity = Vector3.zero;
            grabbedRagdoll.SetGrabbed(true);
        }

        private void ReleaseGrabbedBody(bool applyThrow)
        {
            if (grabbedRagdoll != null)
            {
                grabbedRagdoll.SetGrabbed(false);
            }

            if (grabbedBody != null && applyThrow && playerCamera != null && Balance != null)
            {
                grabbedBody.linearVelocity = grabbedTargetVelocity + playerCamera.transform.forward * Balance.interaction.throwImpulse;
            }

            grabbedBody = null;
            grabbedRagdoll = null;
            grabbedTargetVelocity = Vector3.zero;
        }

        private void TryInteract()
        {
            if (Balance == null)
            {
                return;
            }

            var origin = playerCamera != null ? playerCamera.transform.position : transform.position + Vector3.up * 1.2f;
            var direction = playerCamera != null ? playerCamera.transform.forward : transform.forward;
            if (!UnityEngine.Physics.Raycast(origin, direction, out var hit, Balance.interaction.interactRange))
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
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                pickup.LocalPickup(this);
            }
            else
            {
                pickup.PickupServerRpc(NetworkObjectId);
            }
        }

        public void TryDepositHeldItem()
        {
            if (heldItem == null || NightGameManager.Instance == null)
            {
                return;
            }

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                if (NightGameManager.Instance.TryDepositItem(heldItem.DefinitionIndex))
                {
                    heldItem.Consume();
                }
            }
            else
            {
                DepositHeldItemServerRpc(heldItem.NetworkObjectId, heldItem.DefinitionIndex);
            }

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
            if (heldItem == null || Balance == null)
            {
                return;
            }

            var dropPosition = transform.position + transform.forward * Balance.interaction.dropDistance;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                heldItem.LocalDrop(dropPosition);
            }
            else
            {
                heldItem.DropServerRpc(dropPosition);
            }

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
                if (activeRagdoll != null)
                {
                    activeRagdoll.SetState(BodyDriveState.Downed);
                }

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
                if (bodyRenderers[i] != null)
                {
                    bodyRenderers[i].enabled = visible;
                }
            }
        }

        private void OnHiddenChanged(bool previousValue, bool newValue)
        {
            ApplyVisualState();
        }

        private void OnDownedChanged(bool previousValue, bool newValue)
        {
            if (activeRagdoll != null)
            {
                activeRagdoll.SetState(newValue ? BodyDriveState.Downed : BodyDriveState.Supported);
            }

            if (!newValue)
            {
                controller.enabled = true;
            }

            ApplyVisualState();
        }

        private void OnRuntimeSettingsChanged()
        {
            if (playerCamera != null && activeRagdoll != null && activeRagdoll.State != BodyDriveState.Limp && Balance != null)
            {
                playerCamera.transform.localPosition = Balance.playerCamera.supportedLocalOffset;
            }
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

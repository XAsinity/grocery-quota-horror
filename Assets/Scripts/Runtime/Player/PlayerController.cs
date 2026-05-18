using System;
using System.Collections.Generic;
using GroceryQuotaHorror.Core;
using GroceryQuotaHorror.Data;
using GroceryQuotaHorror.Interaction;
using GroceryQuotaHorror.Physics;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace GroceryQuotaHorror.Player
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerController : NetworkBehaviour, IInteractable
    {
        [SerializeField] private Light flashlight;
        [SerializeField] private Renderer[] bodyRenderers;
        [SerializeField] private SpawnableRagdoll spawnableRagdollPrefab;
        [SerializeField] private ActiveRagdollController activeRagdoll;
        [SerializeField] private PlayerLooseBodyController looseBody;

        private readonly NetworkVariable<bool> isDowned = new();
        private readonly NetworkVariable<bool> isHidden = new();

        private CharacterController controller;
        private Rigidbody rootBody;
        private CapsuleCollider physicalCollider;
        private Camera playerCamera;
        private Transform cameraAnchor;
        private Vector3 velocity;
        private Vector3 smoothedMoveInput;
        private ItemPickup heldItem;
        private Rigidbody grabbedBody;
        private SpawnableRagdoll grabbedRagdoll;
        private Vector3 lastGrabbedTargetPosition;
        private Vector3 grabbedTargetVelocity;
        private float cameraPitch;
        private float visualLookYaw;
        private Vector2 pendingMouseDelta;
        private Vector3 cameraFollowVelocity;
        private readonly List<Collider> ignoredProjectileColliders = new();
        private Vector3 desiredMoveInput;
        private bool wantsSprint;
        private bool freeLookHeld;
        private float targetYaw;
        private float groundedUntilTime;
        private Vector3 lastGroundNormal = Vector3.up;
        private bool wasGroundedLastPhysics;
        private float jumpQueuedUntil;
        private float nextAllowedJumpTime;
        private float ignoreGroundContactsUntil;
        private bool reportedSupportedColliderLeak;
        private bool recoveryCameraBlendActive;
        private float recoveryCameraBlendStartedAt;
        private Vector3 recoveryCameraStartPosition;
        private Quaternion recoveryCameraStartRotation;
        private bool postRecoveryWatchActive;
        private float postRecoveryWatchStartedAt;
        private float postRecoveryNextLogAt;
        private Vector3 postRecoveryExpectedRoot;
        private float postRecoveryAnchorUntil;
        private bool recoveryPending;
        private float recoverAtTime;
        private const float GroundSnapSkin = 0f;
        private const float GroundSnapProbeHeight = 2f;
        private const float GroundSnapProbeDistance = 8f;
        private const float GroundSnapMaxUpwardCorrection = 0.22f;
        private const float RecoveryMaxUpwardCorrection = 0.18f;
        private const float MinGroundNormalY = 0.45f;
        private const float RecoveryCenterRayStartOffset = 0.35f;
        private const float RecoveryCenterRayDistance = 12f;
        private const float ManualRecoveryDelaySeconds = 2.5f;
        private const float RecoveryCameraBlendSeconds = 0.45f;
        private const float PostRecoveryAnchorSeconds = 0.65f;
        private const float PostRecoveryWatchSeconds = 0.75f;
        private const float PostRecoveryLogInterval = 0.15f;
        private const float PostRecoveryDriftWarnDistance = 0.35f;

        public bool IsDowned => isDowned.Value;
        public bool IsHidden => isHidden.Value;
        public ItemPickup HeldItem => heldItem;
        public string Prompt => IsDowned ? "Revive teammate" : string.Empty;

        private bool HasLocalAuthority => !IsSpawned || IsOwner;
        private GameBalanceProfile Balance => GameRuntime.Balance;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            rootBody = GetComponent<Rigidbody>();
            physicalCollider = GetComponent<CapsuleCollider>();
            targetYaw = transform.eulerAngles.y;
            if (!IsSpawned && GroceryQuotaHorror.Bootstrap.NetworkBootstrap.LocalOfflineMode)
            {
                var networkTransform = GetComponent<NetworkTransform>();
                if (networkTransform != null)
                {
                    networkTransform.enabled = false;
                }
            }

            if (activeRagdoll == null)
            {
                activeRagdoll = GetComponent<ActiveRagdollController>();
            }

            if (looseBody == null)
            {
                looseBody = GetComponent<PlayerLooseBodyController>();
            }

            if (looseBody == null)
            {
                looseBody = gameObject.AddComponent<PlayerLooseBodyController>();
            }

            if (activeRagdoll != null)
            {
                activeRagdoll.enabled = false;
            }

            ConfigurePhysicalRoot();
            looseBody.Initialize();
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

            UpdatePendingRecovery();

            if (IsDowned)
            {
                ReleaseGrabbedBody(false);

                return;
            }

            CaptureMoveAndLookInput();

            if (activeRagdoll != null && activeRagdoll.enabled && activeRagdoll.IsAvailable && activeRagdoll.State == BodyDriveState.Limp)
            {
                ReleaseGrabbedBody(false);
                UpdateLimpLookInfluence();
                return;
            }

            MoveAndLook();
            var supportedMove = Vector3.ClampMagnitude(smoothedMoveInput, 1f);
            var supportedSpeed01 = supportedMove.magnitude;
            var supportedSprinting = wantsSprint && smoothedMoveInput.sqrMagnitude > 0.01f;
            var supportedGrounded = IsPhysicallyGrounded();
            if (looseBody != null && looseBody.IsAvailable)
            {
                looseBody.ApplySupportedPose(
                    supportedMove,
                    supportedSpeed01,
                    supportedSprinting,
                    supportedGrounded,
                    visualLookYaw,
                    cameraPitch,
                    Balance);
            }

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

            ApplyPhysicalMovement();
            ReportSupportedColliderLeaks();

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

            RefreshCameraAttachment();
            UpdatePostRecoveryWatch();
        }

        private void CaptureMoveAndLookInput()
        {
            desiredMoveInput = ReadKeyboardMoveInput();
            wantsSprint = Input.GetKey(KeyCode.LeftShift);
            freeLookHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            pendingMouseDelta += new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            if ((Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Jump")) && Balance != null)
            {
                jumpQueuedUntil = Time.time + Balance.playerMovement.jumpBufferSeconds;
            }
        }

        private static Vector3 ReadKeyboardMoveInput()
        {
            var x = 0f;
            var z = 0f;
            if (Input.GetKey(KeyCode.A))
            {
                x -= 1f;
            }

            if (Input.GetKey(KeyCode.D))
            {
                x += 1f;
            }

            if (Input.GetKey(KeyCode.S))
            {
                z -= 1f;
            }

            if (Input.GetKey(KeyCode.W))
            {
                z += 1f;
            }

            return Vector3.ClampMagnitude(new Vector3(x, 0f, z), 1f);
        }

        private void MoveAndLook()
        {
            var movement = Balance.playerMovement;
            smoothedMoveInput = Vector3.Lerp(smoothedMoveInput, desiredMoveInput, 1f - Mathf.Exp(-movement.movementSmoothing * Time.deltaTime));

            var mouseX = pendingMouseDelta.x * movement.lookSensitivity;
            var mouseY = pendingMouseDelta.y * movement.lookSensitivity;
            pendingMouseDelta = Vector2.zero;
            var body = Balance.playerBody;
            var turnRate = movement.mouseTurnRate * (wantsSprint ? 1.18f : 1f);
            if (freeLookHeld)
            {
                visualLookYaw += mouseX * turnRate * Time.deltaTime;
                visualLookYaw = Mathf.Clamp(visualLookYaw, -body.supportedLookYawDegrees, body.supportedLookYawDegrees);
            }
            else
            {
                targetYaw += mouseX * turnRate * Time.deltaTime;
                visualLookYaw = Mathf.MoveTowards(visualLookYaw, 0f, body.supportedLookYawReturnSharpness * 16f * Time.deltaTime);
            }

            cameraPitch = Mathf.Clamp(cameraPitch - mouseY * movement.mouseTurnRate * Time.deltaTime, -movement.maxLookPitch, movement.maxLookPitch);
        }

        private void ApplyPhysicalMovement()
        {
            if (rootBody == null || Balance == null || IsDowned || (activeRagdoll != null && activeRagdoll.enabled && activeRagdoll.State == BodyDriveState.Limp))
            {
                return;
            }

            if (rootBody.isKinematic)
            {
                SetPhysicalRootEnabled(true);
            }

            rootBody.MoveRotation(Quaternion.Euler(0f, targetYaw, 0f));
            if (Time.time < postRecoveryAnchorUntil)
            {
                rootBody.position = postRecoveryExpectedRoot;
                transform.position = postRecoveryExpectedRoot;
                rootBody.linearVelocity = Vector3.zero;
                rootBody.angularVelocity = Vector3.zero;
                return;
            }

            if (looseBody != null && looseBody.BlocksHorizontalMovement)
            {
                ApplyGravityOnly();
                return;
            }

            var movement = Balance.playerMovement;
            var speed = movement.moveSpeed * (wantsSprint ? movement.sprintMultiplier : 1f);
            var rootRotation = Quaternion.Euler(0f, targetYaw, 0f);
            var grounded = IsPhysicallyGrounded();
            if (!wasGroundedLastPhysics && grounded)
            {
                looseBody?.NotifyLanding(Mathf.Abs(rootBody.linearVelocity.y));
            }

            var groundNormal = grounded ? lastGroundNormal : Vector3.up;
            var desiredDirection = rootRotation * desiredMoveInput;
            if (grounded)
            {
                desiredDirection = Vector3.ProjectOnPlane(desiredDirection, groundNormal).normalized;
            }

            if (grounded && Time.time <= jumpQueuedUntil && Time.time >= nextAllowedJumpTime)
            {
                var jumpVelocity = rootBody.linearVelocity;
                jumpVelocity.y = movement.jumpVelocity;
                rootBody.linearVelocity = jumpVelocity;
                groundedUntilTime = 0f;
                ignoreGroundContactsUntil = Time.time + 0.18f;
                wasGroundedLastPhysics = false;
                jumpQueuedUntil = 0f;
                nextAllowedJumpTime = Time.time + movement.jumpCooldown;
                looseBody?.NotifyJump();
                return;
            }

            var desiredVelocity = desiredDirection * speed;
            var currentVelocity = rootBody.linearVelocity;
            var currentPlanar = grounded ? Vector3.ProjectOnPlane(currentVelocity, groundNormal) : new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            var desiredPlanar = grounded ? Vector3.ProjectOnPlane(desiredVelocity, groundNormal) : new Vector3(desiredVelocity.x, 0f, desiredVelocity.z);
            if (desiredMoveInput.sqrMagnitude < 0.0001f && smoothedMoveInput.sqrMagnitude < 0.0004f)
            {
                var brakeRate = Mathf.Max(8f, movement.movementSmoothing * (grounded ? 3.5f : 1.5f));
                var stoppedPlanar = Vector3.MoveTowards(currentPlanar, Vector3.zero, brakeRate * Time.fixedDeltaTime);
                rootBody.linearVelocity = grounded
                    ? stoppedPlanar + Vector3.Project(currentVelocity, groundNormal)
                    : new Vector3(stoppedPlanar.x, currentVelocity.y, stoppedPlanar.z);
                currentVelocity = rootBody.linearVelocity;
                currentPlanar = grounded ? Vector3.ProjectOnPlane(currentVelocity, groundNormal) : new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            }

            var controlMultiplier = grounded ? movement.groundVelocityFollow : movement.airControlMultiplier;
            var acceleration = (desiredPlanar - currentPlanar) * Mathf.Max(1f, movement.movementSmoothing) * Mathf.Clamp01(controlMultiplier);
            rootBody.AddForce(acceleration, ForceMode.Acceleration);

            rootBody.AddForce(Vector3.up * movement.playerGravity, ForceMode.Acceleration);
            var movingOnGround = grounded && desiredMoveInput.sqrMagnitude > 0.01f;
            if (movingOnGround)
            {
                rootBody.AddForce(-groundNormal * movement.groundStickForce, ForceMode.Acceleration);
            }

            if (grounded && rootBody.linearVelocity.y < movement.groundedMaxFallSpeed)
            {
                var clampedVelocity = rootBody.linearVelocity;
                clampedVelocity.y = movement.groundedMaxFallSpeed;
                rootBody.linearVelocity = clampedVelocity;
            }

            var maxHorizontalSpeed = speed * 1.08f;
            var newVelocity = rootBody.linearVelocity;
            var newHorizontal = new Vector3(newVelocity.x, 0f, newVelocity.z);
            if (newHorizontal.magnitude > maxHorizontalSpeed)
            {
                newHorizontal = newHorizontal.normalized * maxHorizontalSpeed;
                rootBody.linearVelocity = new Vector3(newHorizontal.x, newVelocity.y, newHorizontal.z);
            }

            wasGroundedLastPhysics = grounded;
        }

        private void ApplyGravityOnly()
        {
            var movement = Balance.playerMovement;
            var currentVelocity = rootBody.linearVelocity;
            var horizontal = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            horizontal = Vector3.MoveTowards(horizontal, Vector3.zero, Mathf.Max(2f, movement.movementSmoothing) * Time.fixedDeltaTime);
            rootBody.linearVelocity = new Vector3(horizontal.x, currentVelocity.y, horizontal.z);
            rootBody.AddForce(Vector3.up * movement.playerGravity, ForceMode.Acceleration);
            if (IsPhysicallyGrounded() && rootBody.linearVelocity.y < movement.groundedMaxFallSpeed)
            {
                var clampedVelocity = rootBody.linearVelocity;
                clampedVelocity.y = movement.groundedMaxFallSpeed;
                rootBody.linearVelocity = clampedVelocity;
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
            ConfigurePhysicalRoot();
            SetPhysicalRootEnabled(true);
            IgnoreSelfPhysicsCollisions();
            looseBody?.Initialize();
            SnapRootToGround();
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

                RefreshCameraAttachment(true);
            }

            ApplyVisualState();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void ConfigurePhysicalRoot()
        {
            if (rootBody == null)
            {
                rootBody = GetComponent<Rigidbody>();
            }

            if (rootBody == null)
            {
                rootBody = gameObject.AddComponent<Rigidbody>();
            }

            if (physicalCollider == null)
            {
                physicalCollider = GetComponent<CapsuleCollider>();
            }

            if (physicalCollider == null)
            {
                physicalCollider = gameObject.AddComponent<CapsuleCollider>();
            }

            if (controller != null)
            {
                physicalCollider.center = controller.center;
                physicalCollider.height = controller.height;
                physicalCollider.radius = controller.radius;
            }

            FitPhysicalColliderToVisibleBody();
            if (controller != null)
            {
                controller.center = physicalCollider.center;
                controller.height = physicalCollider.height;
                controller.radius = physicalCollider.radius;
                controller.enabled = false;
            }

            rootBody.mass = 70f;
            rootBody.useGravity = false;
            rootBody.interpolation = RigidbodyInterpolation.Interpolate;
            rootBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rootBody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            SetSupportedLimbCollidersEnabled(false);
        }

        private void FitPhysicalColliderToVisibleBody()
        {
            if (physicalCollider == null || !TryGetVisibleBodyBounds(out var bounds))
            {
                return;
            }

            var localCenter = transform.InverseTransformPoint(bounds.center);
            var height = Mathf.Max(1.2f, bounds.size.y);
            var radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.38f, 0.22f, 0.45f);
            physicalCollider.center = new Vector3(localCenter.x, Mathf.Max(height * 0.5f, radius), localCenter.z);
            physicalCollider.height = Mathf.Max(height, radius * 2f);
            physicalCollider.radius = radius;
        }

        private bool TryGetVisibleBodyBounds(out Bounds bounds)
        {
            var renderers = bodyRenderers != null && bodyRenderers.Length > 0
                ? bodyRenderers
                : GetComponentsInChildren<Renderer>(true);
            var found = false;
            bounds = default;
            for (var i = 0; i < renderers.Length; i++)
            {
                var bodyRenderer = renderers[i];
                if (bodyRenderer == null)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = bodyRenderer.bounds;
                    found = true;
                    continue;
                }

                bounds.Encapsulate(bodyRenderer.bounds);
            }

            return found;
        }

        private void SetPhysicalRootEnabled(bool enabled)
        {
            if (rootBody == null)
            {
                return;
            }

            if (enabled)
            {
                rootBody.isKinematic = false;
                rootBody.detectCollisions = true;
                if (physicalCollider != null)
                {
                    physicalCollider.enabled = true;
                }

                rootBody.position = transform.position;
                rootBody.rotation = transform.rotation;
                rootBody.linearVelocity = Vector3.zero;
                rootBody.angularVelocity = Vector3.zero;
                rootBody.WakeUp();
                return;
            }

            if (!rootBody.isKinematic)
            {
                rootBody.linearVelocity = Vector3.zero;
                rootBody.angularVelocity = Vector3.zero;
            }

            if (physicalCollider != null)
            {
                physicalCollider.enabled = false;
            }

            rootBody.detectCollisions = false;
            rootBody.isKinematic = true;
        }

        private void SnapRootToGround()
        {
            if (physicalCollider == null)
            {
                return;
            }

            var localBottom = physicalCollider.center.y - physicalCollider.height * 0.5f;
            var currentBottomWorld = transform.TransformPoint(new Vector3(physicalCollider.center.x, localBottom, physicalCollider.center.z)).y;
            var probeOrigin = transform.position + Vector3.up * GroundSnapProbeHeight;
            var probeDistance = GroundSnapProbeHeight + GroundSnapProbeDistance;
            if (!TryFindGroundNearBodyBottom(probeOrigin, probeDistance, currentBottomWorld, GroundSnapMaxUpwardCorrection, out var groundY))
            {
                return;
            }

            var desiredBottomWorld = groundY + GroundSnapSkin;
            var verticalOffset = desiredBottomWorld - currentBottomWorld;
            if (Mathf.Abs(verticalOffset) < 0.01f)
            {
                return;
            }

            var groundedPosition = transform.position + Vector3.up * verticalOffset;
            transform.position = groundedPosition;
            if (rootBody == null)
            {
                return;
            }

            rootBody.position = groundedPosition;
            if (!rootBody.isKinematic)
            {
                var velocity = rootBody.linearVelocity;
                rootBody.linearVelocity = new Vector3(velocity.x, Mathf.Min(velocity.y, 0f), velocity.z);
            }
        }

        private bool TryFindGroundNearBodyBottom(Vector3 probeOrigin, float probeDistance, float referenceBottomY, float maxUpwardCorrection, out float groundY)
        {
            groundY = 0f;
            var hits = UnityEngine.Physics.RaycastAll(probeOrigin, Vector3.down, probeDistance, ~0, QueryTriggerInteraction.Ignore);
            var foundGround = false;
            var closestToReference = float.MaxValue;
            var highestAllowedGroundY = referenceBottomY + maxUpwardCorrection;
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null ||
                    hit.collider.transform.IsChildOf(transform) ||
                    hit.normal.y < MinGroundNormalY ||
                    hit.point.y > highestAllowedGroundY)
                {
                    continue;
                }

                var distanceToReference = Mathf.Abs(hit.point.y - referenceBottomY);
                if (distanceToReference >= closestToReference)
                {
                    continue;
                }

                closestToReference = distanceToReference;
                groundY = hit.point.y;
                foundGround = true;
            }

            return foundGround;
        }

        private void IgnoreSelfPhysicsCollisions()
        {
            if (physicalCollider == null)
            {
                return;
            }

            var colliders = GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var childCollider = colliders[i];
                if (childCollider == null || childCollider == physicalCollider)
                {
                    continue;
                }

                UnityEngine.Physics.IgnoreCollision(physicalCollider, childCollider, true);
            }
        }

        private void SetSupportedLimbCollidersEnabled(bool enabled)
        {
            if (!enabled)
            {
                reportedSupportedColliderLeak = false;
            }

            var colliders = GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var childCollider = colliders[i];
                if (childCollider == null || childCollider == physicalCollider || childCollider.transform == transform)
                {
                    continue;
                }

                if (childCollider.isTrigger)
                {
                    continue;
                }

                childCollider.enabled = enabled;
            }
        }

        private void ReportSupportedColliderLeaks()
        {
            if (reportedSupportedColliderLeak ||
                activeRagdoll != null && activeRagdoll.enabled && activeRagdoll.State == BodyDriveState.Limp)
            {
                return;
            }

            var colliders = GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var childCollider = colliders[i];
                if (childCollider == null ||
                    childCollider == physicalCollider ||
                    childCollider.transform == transform ||
                    childCollider.isTrigger ||
                    !childCollider.enabled)
                {
                    continue;
                }

                reportedSupportedColliderLeak = true;
                Debug.LogWarning($"[PlayerCollision] Non-root collider '{childCollider.name}' is enabled during supported movement. This can wedge the player and should be disabled unless the player is limp ragdolled.", childCollider);
                return;
            }
        }

        private bool IsPhysicallyGrounded()
        {
            return Time.time >= ignoreGroundContactsUntil && Time.time <= groundedUntilTime;
        }

        private void OnCollisionStay(Collision collision)
        {
            if (Time.time < ignoreGroundContactsUntil)
            {
                return;
            }

            for (var i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.y > 0.45f)
                {
                    groundedUntilTime = Time.time + 0.1f;
                    lastGroundNormal = collision.GetContact(i).normal;
                    return;
                }
            }
        }

        private void ToggleBodyState()
        {
            if (activeRagdoll == null)
            {
                activeRagdoll = GetComponent<ActiveRagdollController>();
            }

            if (activeRagdoll == null)
            {
                looseBody?.ToggleLimpMode();
                return;
            }

            activeRagdoll.enabled = true;
            if (!activeRagdoll.EnsureInitialized())
            {
                activeRagdoll.enabled = false;
                looseBody?.ToggleLimpMode();
                return;
            }

            if (activeRagdoll.State == BodyDriveState.Limp)
            {
                BeginDelayedRecovery();
                return;
            }

            activeRagdoll.SetState(BodyDriveState.Limp);
            recoveryPending = false;
            SetSupportedLimbCollidersEnabled(true);
            var currentVelocity = rootBody != null ? rootBody.linearVelocity : Vector3.zero;
            var currentAngularVelocity = rootBody != null ? rootBody.angularVelocity : Vector3.zero;
            activeRagdoll.ApplyLimpVelocity(currentVelocity, currentAngularVelocity, transform.forward * Mathf.Max(1.5f, currentVelocity.magnitude * 0.35f));
            SetPhysicalRootEnabled(false);
            cameraFollowVelocity = Vector3.zero;
            RefreshCameraAttachment(true);
        }

        private void BeginDelayedRecovery()
        {
            if (recoveryPending)
            {
                return;
            }

            recoveryPending = true;
            recoverAtTime = Time.time + ManualRecoveryDelaySeconds;
            Debug.Log($"[Recovery] Manual recovery queued for {ManualRecoveryDelaySeconds:0.0}s from live ragdoll pose.", this);
        }

        private void UpdatePendingRecovery()
        {
            if (!recoveryPending || Time.time < recoverAtTime)
            {
                return;
            }

            recoveryPending = false;
            RecoverFromCurrentRagdollPose();
        }

        private void RecoverFromCurrentRagdollPose()
        {
            if (activeRagdoll == null)
            {
                return;
            }

            velocity = Vector3.zero;
            pendingMouseDelta = Vector2.zero;
            if (!activeRagdoll.TryGetCurrentRagdollCenterOfMass(out var liveCenterOfMass))
            {
                Debug.LogWarning("Recovery blocked because no live ragdoll center of mass was available. Staying ragdolled instead of falling back to the old root position.", this);
                return;
            }

            var liePose = activeRagdoll.GetCurrentLiePose();
            var recoveryRotation = transform.rotation;

            Debug.Log($"[Recovery] Live COM={liveCenterOfMass} oldRoot={transform.position} unchangedYaw={recoveryRotation.eulerAngles.y:0.0} liePose={liePose}", this);
            if (!TryResolveRecoveryRootPosition(liveCenterOfMass, out var recoveryRootPosition))
            {
                Debug.LogWarning($"[Recovery] Blocked: no valid ground under live COM {liveCenterOfMass}. Staying ragdolled.", this);
                return;
            }

            Debug.Log($"[Recovery] Resolved root={recoveryRootPosition} from live COM={liveCenterOfMass}", this);

            if (!IsStandingCapsuleClear(recoveryRootPosition))
            {
                Debug.LogWarning($"[Recovery] Blocked: standing capsule would overlap nearby geometry at {recoveryRootPosition}. Staying ragdolled instead of launching the player upward.", this);
                return;
            }

            BeginRecoveryCameraBlend();
            visualLookYaw = 0f;
            activeRagdoll.FreezeRagdollPoseForRecovery(recoveryRootPosition, recoveryRotation);
            targetYaw = recoveryRotation.eulerAngles.y;
            SetPhysicalRootEnabled(true);
            transform.position = recoveryRootPosition;
            if (rootBody != null)
            {
                rootBody.position = recoveryRootPosition;
            }

            ignoreGroundContactsUntil = Time.time + 0.05f;
            BeginPostRecoveryWatch(recoveryRootPosition, recoveryRotation);
            activeRagdoll.ReleaseRuntimeRagdollComponents();
            SetSupportedLimbCollidersEnabled(false);
            looseBody?.ResetAfterRagdoll(physicalCollider, true);
            activeRagdoll.enabled = false;
            cameraFollowVelocity = Vector3.zero;
            RefreshCameraAttachment();
        }

        private bool TryResolveRecoveryRootPosition(Vector3 liveCenterOfMass, out Vector3 recoveryPosition)
        {
            recoveryPosition = new Vector3(liveCenterOfMass.x, liveCenterOfMass.y, liveCenterOfMass.z);
            var probeOrigin = liveCenterOfMass + Vector3.up * RecoveryCenterRayStartOffset;
            var hits = UnityEngine.Physics.RaycastAll(probeOrigin, Vector3.down, RecoveryCenterRayDistance, ~0, QueryTriggerInteraction.Ignore);
            var foundGround = false;
            var closestDistance = float.MaxValue;
            var groundY = 0f;
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null)
                {
                    continue;
                }

                var isSelf = hit.collider.transform.IsChildOf(transform);
                var isTooSteep = hit.normal.y < MinGroundNormalY;
                var isAboveCom = hit.point.y > liveCenterOfMass.y + RecoveryMaxUpwardCorrection;
                Debug.Log($"[Recovery] Ray hit '{hit.collider.name}' point={hit.point} distance={hit.distance:0.000} normalY={hit.normal.y:0.000} self={isSelf} tooSteep={isTooSteep} aboveCom={isAboveCom}", this);
                if (isSelf || isTooSteep || isAboveCom || hit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hit.distance;
                groundY = hit.point.y;
                foundGround = true;
            }

            if (foundGround)
            {
                var localBottom = physicalCollider != null ? physicalCollider.center.y - physicalCollider.height * 0.5f : 0f;
                recoveryPosition.y = groundY + GroundSnapSkin - localBottom;
                return true;
            }

            return false;
        }

        private void BeginRecoveryCameraBlend()
        {
            if (playerCamera == null)
            {
                return;
            }

            recoveryCameraStartPosition = playerCamera.transform.position;
            recoveryCameraStartRotation = playerCamera.transform.rotation;
            recoveryCameraBlendStartedAt = Time.time;
            recoveryCameraBlendActive = true;
        }

        private void ApplyRecoveryCameraBlend()
        {
            if (!recoveryCameraBlendActive || playerCamera == null)
            {
                return;
            }

            var blend01 = Mathf.Clamp01((Time.time - recoveryCameraBlendStartedAt) / RecoveryCameraBlendSeconds);
            var eased = blend01 * blend01 * (3f - 2f * blend01);
            var targetPosition = playerCamera.transform.position;
            var targetRotation = playerCamera.transform.rotation;
            playerCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(recoveryCameraStartPosition, targetPosition, eased),
                Quaternion.Slerp(recoveryCameraStartRotation, targetRotation, eased));
            if (blend01 >= 1f)
            {
                recoveryCameraBlendActive = false;
            }
        }

        private void BeginPostRecoveryWatch(Vector3 expectedRoot, Quaternion recoveryRotation)
        {
            postRecoveryExpectedRoot = expectedRoot;
            postRecoveryWatchStartedAt = Time.time;
            postRecoveryNextLogAt = Time.time;
            postRecoveryAnchorUntil = Time.time + PostRecoveryAnchorSeconds;
            postRecoveryWatchActive = true;
            Debug.Log($"[Recovery] Handoff start expectedRoot={expectedRoot} actualRoot={transform.position} preservedYaw={recoveryRotation.eulerAngles.y:0.0} rootBody={(rootBody != null ? rootBody.position : transform.position)}", this);
        }

        private void UpdatePostRecoveryWatch()
        {
            if (!postRecoveryWatchActive)
            {
                return;
            }

            var elapsed = Time.time - postRecoveryWatchStartedAt;
            if (Time.time < postRecoveryAnchorUntil)
            {
                transform.position = postRecoveryExpectedRoot;
                if (rootBody != null)
                {
                    rootBody.position = postRecoveryExpectedRoot;
                    rootBody.linearVelocity = Vector3.zero;
                    rootBody.angularVelocity = Vector3.zero;
                }
            }

            var rootPosition = transform.position;
            var rootBodyPosition = rootBody != null ? rootBody.position : rootPosition;
            var drift = Vector3.Distance(rootPosition, postRecoveryExpectedRoot);
            if (Time.time >= postRecoveryNextLogAt || elapsed >= PostRecoveryWatchSeconds)
            {
                var status = drift > PostRecoveryDriftWarnDistance ? "DRIFT" : "ok";
                Debug.Log($"[Recovery] Handoff {status} t={elapsed:0.00} root={rootPosition} expected={postRecoveryExpectedRoot} drift={drift:0.000} rootBody={rootBodyPosition}", this);
                postRecoveryNextLogAt = Time.time + PostRecoveryLogInterval;
            }

            if (elapsed < PostRecoveryWatchSeconds)
            {
                return;
            }

            postRecoveryWatchActive = false;
            Debug.Log($"[Recovery] Handoff complete root={rootPosition} expected={postRecoveryExpectedRoot} drift={drift:0.000}", this);
        }

        private bool IsStandingCapsuleClear(Vector3 rootPosition)
        {
            if (physicalCollider == null)
            {
                return true;
            }

            var radius = Mathf.Max(0.01f, physicalCollider.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z));
            var height = Mathf.Max(physicalCollider.height * transform.lossyScale.y, radius * 2f);
            var center = rootPosition + transform.rotation * physicalCollider.center;
            var halfSegment = Mathf.Max(0f, height * 0.5f - radius);
            var pointA = center + Vector3.up * halfSegment;
            var pointB = center - Vector3.up * halfSegment;
            var overlaps = UnityEngine.Physics.OverlapCapsule(pointA, pointB, radius * 0.96f, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < overlaps.Length; i++)
            {
                var overlap = overlaps[i];
                if (overlap == null || overlap.transform.IsChildOf(transform) || IsIgnoredRecoveryOverlap(overlap))
                {
                    continue;
                }

                Debug.LogWarning($"[Recovery] Capsule blocked by '{overlap.name}' at root={rootPosition}.", overlap);
                return false;
            }

            Debug.Log($"[Recovery] Capsule clear root={rootPosition} center={center} pointA={pointA} pointB={pointB} radius={radius:0.000}", this);
            return true;
        }

        private static bool IsIgnoredRecoveryOverlap(Collider overlap)
        {
            if (overlap.GetComponentInParent<SpawnableRagdoll>() != null)
            {
                return true;
            }

            if (overlap.GetComponentInParent<PlayerController>() != null)
            {
                return true;
            }

            return false;
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

            if (!TryFindGrabbedRagdoll(out var body, out var ragdoll))
            {
                return;
            }

            grabbedBody = body;
            grabbedRagdoll = ragdoll;
            lastGrabbedTargetPosition = playerCamera.transform.position + playerCamera.transform.forward * Balance.interaction.grabbedHoldDistance;
            grabbedTargetVelocity = Vector3.zero;
            grabbedRagdoll.SetGrabbed(true);
        }

        private bool TryFindGrabbedRagdoll(out Rigidbody body, out SpawnableRagdoll ragdoll)
        {
            body = null;
            ragdoll = null;

            if (playerCamera == null || Balance == null)
            {
                return false;
            }

            var hits = UnityEngine.Physics.RaycastAll(
                playerCamera.transform.position,
                playerCamera.transform.forward,
                Balance.interaction.interactRange,
                ~0,
                QueryTriggerInteraction.Ignore);
            if (hits.Length == 0)
            {
                return false;
            }

            Array.Sort(hits, static (left, right) => left.distance.CompareTo(right.distance));
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                ragdoll = hit.rigidbody != null
                    ? hit.rigidbody.GetComponentInParent<SpawnableRagdoll>()
                    : hit.collider.GetComponentInParent<SpawnableRagdoll>();
                if (ragdoll == null)
                {
                    continue;
                }

                body = hit.rigidbody;
                if (body == null && !ragdoll.TryGetClosestRigidbody(hit.point, out body))
                {
                    ragdoll = null;
                    continue;
                }

                return body != null;
            }

            return false;
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
            if (rootBody != null)
            {
                rootBody.position = targetPosition;
            }
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
                activeRagdoll.enabled = false;
            }

            SetPhysicalRootEnabled(!newValue);

            ApplyVisualState();
        }

        private void OnRuntimeSettingsChanged()
        {
            if (playerCamera != null && Balance != null)
            {
                cameraFollowVelocity = Vector3.zero;
                RefreshCameraAttachment(true);
            }
        }

        private void RefreshCameraAttachment(bool forceSnap = false)
        {
            if (playerCamera == null || Balance == null)
            {
                return;
            }

            if (looseBody != null && looseBody.AttachCamera(playerCamera, cameraPitch, Balance))
            {
                cameraFollowVelocity = Vector3.zero;
                ApplyRecoveryCameraBlend();
                return;
            }

            if (cameraAnchor == null)
            {
                var anchorObject = new GameObject("CameraAnchor");
                cameraAnchor = anchorObject.transform;
                cameraAnchor.SetParent(transform, false);
                forceSnap = true;
            }

            if (playerCamera.transform.parent != cameraAnchor)
            {
                playerCamera.transform.SetParent(cameraAnchor, false);
                playerCamera.transform.localPosition = Vector3.zero;
                playerCamera.transform.localRotation = Quaternion.identity;
                forceSnap = true;
            }

            var targetPosition = transform.TransformPoint(Balance.playerCamera.supportedLocalOffset);
            var targetRotation = Quaternion.Euler(cameraPitch, transform.eulerAngles.y, 0f);
            if (activeRagdoll != null && activeRagdoll.IsAvailable && activeRagdoll.TryGetCameraTargetPose(cameraPitch, out var ragdollTargetPosition, out var ragdollTargetRotation))
            {
                targetPosition = ragdollTargetPosition;
                targetRotation = ragdollTargetRotation;
            }

            if (forceSnap || activeRagdoll == null || activeRagdoll.State != BodyDriveState.Limp)
            {
                cameraAnchor.SetPositionAndRotation(targetPosition, targetRotation);
                cameraFollowVelocity = Vector3.zero;
                ApplyRecoveryCameraBlend();
                return;
            }

            cameraAnchor.position = Vector3.SmoothDamp(
                cameraAnchor.position,
                targetPosition,
                ref cameraFollowVelocity,
                1f / Mathf.Max(0.01f, Balance.playerCamera.limpFollowPositionSharpness));
            cameraAnchor.rotation = Quaternion.Slerp(
                cameraAnchor.rotation,
                targetRotation,
                1f - Mathf.Exp(-Mathf.Max(0.01f, Balance.playerCamera.limpFollowRotationSharpness) * Time.deltaTime));
            ApplyRecoveryCameraBlend();
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

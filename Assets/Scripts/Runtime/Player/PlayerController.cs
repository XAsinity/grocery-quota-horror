using System;
using System.Collections.Generic;
using GroceryQuotaHorror.Core;
using GroceryQuotaHorror.Data;
using GroceryQuotaHorror.Interaction;
using GroceryQuotaHorror.Monsters;
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

        private struct ImpactRecoveryState
        {
            public bool Active;
            public float Severity01;
            public float AccumulatedKnockout01;
            public float PendingImpactDamage01;
            public float EarliestRecoveryTime;
            public float StableHoldSeconds;

            public void Reset()
            {
                Active = false;
                Severity01 = 0f;
                AccumulatedKnockout01 = 0f;
                PendingImpactDamage01 = 0f;
                EarliestRecoveryTime = 0f;
                StableHoldSeconds = 0f;
            }
        }

        private readonly struct ImpactCollisionEvent
        {
            public ImpactCollisionEvent(Vector3 velocity, Vector3 point, Collider sourceCollider, float sourceMass)
            {
                Velocity = velocity;
                Point = point;
                SourceCollider = sourceCollider;
                SourceMass = sourceMass;
            }

            public Vector3 Velocity { get; }
            public Vector3 Point { get; }
            public Collider SourceCollider { get; }
            public float SourceMass { get; }
        }

        private enum RagdollCause
        {
            Manual,
            Impact,
            MonsterThrow
        }

        private readonly struct RagdollRequest
        {
            public RagdollRequest(
                RagdollCause cause,
                Vector3 linearVelocity,
                Vector3 angularVelocity,
                Vector3 impulse,
                Vector3 impactPoint,
                bool autoRecover,
                float recoveryDelaySeconds,
                float impactSeverity01,
                bool applyCooldown,
                float cooldownSeconds,
                bool resetExistingLimpVelocity)
            {
                Cause = cause;
                LinearVelocity = linearVelocity;
                AngularVelocity = angularVelocity;
                Impulse = impulse;
                ImpactPoint = impactPoint;
                AutoRecover = autoRecover;
                RecoveryDelaySeconds = recoveryDelaySeconds;
                ImpactSeverity01 = impactSeverity01;
                ApplyCooldown = applyCooldown;
                CooldownSeconds = cooldownSeconds;
                ResetExistingLimpVelocity = resetExistingLimpVelocity;
            }

            public RagdollCause Cause { get; }
            public Vector3 LinearVelocity { get; }
            public Vector3 AngularVelocity { get; }
            public Vector3 Impulse { get; }
            public Vector3 ImpactPoint { get; }
            public bool AutoRecover { get; }
            public float RecoveryDelaySeconds { get; }
            public float ImpactSeverity01 { get; }
            public bool ApplyCooldown { get; }
            public float CooldownSeconds { get; }
            public bool ResetExistingLimpVelocity { get; }
        }

        private readonly NetworkVariable<bool> isDowned = new();
        private readonly NetworkVariable<bool> isHidden = new();

        private CharacterController controller;
        private Rigidbody rootBody;
        private CapsuleCollider physicalCollider;
        private Camera playerCamera;
        private PlayerImpactOverlay impactOverlay;
        private Vector3 velocity;
        private Vector3 smoothedMoveInput;
        private ItemPickup heldItem;
        private Rigidbody grabbedBody;
        private SpawnableRagdoll grabbedRagdoll;
        private Vector3 grabbedLocalPoint;
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
        private ImpactRecoveryState impactRecovery;
        private float recoverAtTime;
        private float nextAllowedImpactRagdollTime;
        private float nextRecoveryStabilityDebugAt;
        private int nextRecoveryHandoffSequence;
        private int lastRequestedRecoveryHandoffSequence;
        private int lastServerRecoveryHandoffSequence;
        private int lastHandledRecoveryCorrectionSequence;
        private int aiDragPossessionCount;
        private const float GroundSnapSkin = 0f;
        private const float GroundSnapProbeHeight = 2f;
        private const float GroundSnapProbeDistance = 8f;
        private const float GroundSnapMaxUpwardCorrection = 0.22f;
        private const float GroundProbeStickDistance = 0.34f;
        private const float GroundProbePenetrationTolerance = 0.06f;
        private const float GroundProbeMaxCorrectionPerStep = 0.08f;
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
        private const float RecoveryStabilityDebugInterval = 1f;
        private const float ImpactRecoveryStabilityGraceSeconds = 2.25f;
        private const float ImpactRecoveryAngularGraceMultiplier = 2.25f;
        private const float NetworkRecoveryMaxRootComHorizontalDistance = 1.75f;
        private const float NetworkRecoveryMaxRootGroundDelta = 0.75f;
        private const float NetworkRecoveryMaxHostRootDistance = 80f;
        private const float NetworkRecoveryCorrectionDistance = 0.25f;

        public bool IsDowned => isDowned.Value;
        public bool IsHidden => isHidden.Value;
        public ItemPickup HeldItem => heldItem;
        public string Prompt => IsDowned ? "Revive teammate" : string.Empty;
        public bool IsRagdolled => looseBody != null && looseBody.IsLimp || IsActiveRagdollLimp;
        public Vector3 CurrentBodyPosition => TryGetLiveRagdollPose(out var position, out _) ? position : transform.position;
        public Quaternion CurrentBodyRotation => TryGetLiveRagdollPose(out _, out var rotation) ? rotation : transform.rotation;
        public Vector3 AiTargetPosition => CurrentBodyPosition;
        public bool IsAiDragPossessed => aiDragPossessionCount > 0;

        private bool HasLocalAuthority => !IsSpawned || IsOwner;
        private GameBalanceProfile Balance => GameRuntime.Balance;
        private bool IsActiveRagdollLimp => activeRagdoll != null &&
                                            activeRagdoll.enabled &&
                                            activeRagdoll.IsAvailable &&
                                            activeRagdoll.State == BodyDriveState.Limp;

        public bool TryBeginImpactRagdoll(Vector3 impactVelocity, Vector3 impactPoint, Collider source, float sourceMassOverride = -1f)
        {
            if (Balance == null || IsDowned)
            {
                return false;
            }

            var ragdoll = Balance.ragdoll;
            var speed = impactVelocity.magnitude;
            if (speed < ragdoll.impactVelocityThreshold)
            {
                return false;
            }

            if (!TryResolveVisibleImpactPoint(impactPoint, ragdoll.impactContactPadding, out var resolvedImpactPoint, out var hitHeight01))
            {
                return false;
            }

            var direction = impactVelocity.sqrMagnitude > 0.0001f ? impactVelocity.normalized : transform.forward;
            if (source != null && source.attachedRigidbody != null)
            {
                var sourceToPlayer = transform.position - source.bounds.center;
                sourceToPlayer.y = 0f;
                if (sourceToPlayer.sqrMagnitude > 0.0001f && Vector3.Dot(direction, sourceToPlayer.normalized) < 0f)
                {
                    direction = sourceToPlayer.normalized;
                }
            }

            var sourceMass = sourceMassOverride > 0f ? sourceMassOverride : source != null && source.attachedRigidbody != null ? source.attachedRigidbody.mass : 1f;
            var sourceMassFactor = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0.01f, sourceMass) / Mathf.Max(1f, ragdoll.impactMassReference)), 0.18f, 2.2f);
            var localImpulseMassFactor = Mathf.Clamp(sourceMassFactor, 0.55f, 2.2f);
            var lowHit01 = Mathf.InverseLerp(0.48f, 0.16f, hitHeight01);
            var wholeBodyVelocityShare = Mathf.Lerp(ragdoll.impactWholeBodyVelocityShare, ragdoll.impactLowHitVelocityShare, lowHit01);
            var transferredVelocity = Vector3.ClampMagnitude(
                direction * speed * ragdoll.impactVelocityTransfer * sourceMassFactor + Vector3.up * Mathf.Min(speed * 0.06f * sourceMassFactor, 1.6f),
                ragdoll.impactMaxTransferredSpeed);
            var impulseMagnitude = Mathf.Min(speed * ragdoll.impactImpulseMultiplier * localImpulseMassFactor, ragdoll.impactMaxImpulse);
            var impulse = direction * impulseMagnitude * Mathf.Lerp(1f, ragdoll.impactLowHitImpulseMultiplier, lowHit01);
            var angularVelocity = rootBody != null ? rootBody.angularVelocity : Vector3.zero;
            var wholeBodyVelocity = transferredVelocity * wholeBodyVelocityShare;
            var severity01 = CalculateImpactRecoverySeverity(speed, sourceMass, impulseMagnitude, transferredVelocity.magnitude, ragdoll);
            var recoveryDelay = Mathf.Lerp(ragdoll.impactRecoveryMinDelay, ragdoll.impactRecoveryMaxDelay, severity01);
            return TryBeginRagdoll(new RagdollRequest(
                RagdollCause.Impact,
                wholeBodyVelocity,
                angularVelocity,
                impulse,
                resolvedImpactPoint,
                true,
                recoveryDelay,
                severity01,
                true,
                ragdoll.impactCooldown,
                false));
        }

        public bool TryBeginPrototypeMonsterThrow(Vector3 throwDirection, Vector3 impactPoint, Collider source)
        {
            if (Balance == null || IsDowned)
            {
                return false;
            }

            if (!TryResolveVisibleImpactPoint(impactPoint, Balance.ragdoll.impactContactPadding, out var resolvedImpactPoint, out _))
            {
                resolvedImpactPoint = transform.position + Vector3.up;
            }

            var flatDirection = Vector3.ProjectOnPlane(throwDirection, Vector3.up);
            if (flatDirection.sqrMagnitude < 0.0001f)
            {
                flatDirection = transform.forward;
            }

            flatDirection.Normalize();
            var monster = Balance.monster;
            var throwVelocity = flatDirection * monster.prototypeThrowSpeed + Vector3.up * monster.prototypeThrowUpwardVelocity;
            var throwImpulse = (flatDirection + Vector3.up * 0.35f).normalized * monster.prototypeThrowImpulse;
            var angularVelocity = rootBody != null ? rootBody.angularVelocity + Vector3.Cross(Vector3.up, flatDirection) * 6f : Vector3.Cross(Vector3.up, flatDirection) * 6f;
            var severity01 = Mathf.Clamp01(monster.prototypeThrowSeverity);
            var recoveryDelay = Mathf.Lerp(Balance.ragdoll.impactRecoveryMinDelay, Balance.ragdoll.impactRecoveryMaxDelay, severity01);
            var didBeginRagdoll = TryBeginRagdoll(new RagdollRequest(
                RagdollCause.MonsterThrow,
                throwVelocity,
                angularVelocity,
                throwImpulse,
                resolvedImpactPoint,
                true,
                recoveryDelay,
                severity01,
                true,
                Balance.ragdoll.impactCooldown,
                true));
            if (didBeginRagdoll)
            {
                Debug.Log($"[MonsterPrototype] Monster throw applied velocity={throwVelocity} impulse={throwImpulse.magnitude:0.0} severity={severity01:0.00}.", this);
            }

            return didBeginRagdoll;
        }

        private static float CalculateImpactRecoverySeverity(float speed, float sourceMass, float impulseMagnitude, float transferredSpeed, RagdollTuning ragdoll)
        {
            var speed01 = Mathf.Clamp01(speed / Mathf.Max(0.01f, ragdoll.impactRecoverySeverityReferenceSpeed));
            var mass01 = Mathf.Clamp01(Mathf.Sqrt(Mathf.Max(0.01f, sourceMass) / Mathf.Max(1f, ragdoll.impactRecoverySeverityMassReference)));
            var impulse01 = Mathf.Clamp01(impulseMagnitude / Mathf.Max(0.01f, ragdoll.impactMaxImpulse));
            var transfer01 = Mathf.Clamp01(transferredSpeed / Mathf.Max(0.01f, ragdoll.impactMaxTransferredSpeed));
            return Mathf.Clamp01(speed01 * 0.45f + mass01 * 0.2f + impulse01 * 0.25f + transfer01 * 0.1f);
        }

        private bool TryResolveVisibleImpactPoint(Vector3 rawPoint, float contactPadding, out Vector3 resolvedPoint, out float height01)
        {
            resolvedPoint = rawPoint;
            height01 = 0.5f;

            if (!TryGetVisibleBodyBounds(out var bodyBounds))
            {
                return true;
            }

            height01 = Mathf.InverseLerp(bodyBounds.min.y, bodyBounds.max.y, rawPoint.y);
            var closestBoundsPoint = bodyBounds.ClosestPoint(rawPoint);
            var boundsDistance = Vector3.Distance(rawPoint, closestBoundsPoint);
            if (boundsDistance > Mathf.Max(0.02f, contactPadding))
            {
                return false;
            }

            resolvedPoint = closestBoundsPoint;
            return true;
        }

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
            UpdateLooseBodyGrabTarget();
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
            HandleMonsterSpawnInput();
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
            UpdateLooseBodyGrabTarget();
            var targetPosition = playerCamera.transform.position + playerCamera.transform.forward * interaction.grabbedHoldDistance;
            if (looseBody != null && looseBody.TryGetGrabAnchorPosition(out var handAnchor))
            {
                targetPosition = Vector3.Lerp(targetPosition, handAnchor, 0.72f);
            }

            grabbedTargetVelocity = (targetPosition - lastGrabbedTargetPosition) / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            lastGrabbedTargetPosition = targetPosition;

            var grabbedPoint = grabbedBody.transform.TransformPoint(grabbedLocalPoint);
            var toTarget = targetPosition - grabbedPoint;
            var desiredAcceleration = new Vector3(toTarget.x, toTarget.y * ragdoll.ragdollHeldVerticalAssist, toTarget.z);
            var force = desiredAcceleration * ragdoll.ragdollHoldForce - grabbedBody.GetPointVelocity(grabbedPoint) * ragdoll.ragdollHoldDamping;
            grabbedBody.AddForceAtPosition(Vector3.ClampMagnitude(force, ragdoll.ragdollHeldMaxForce), grabbedPoint, ForceMode.Acceleration);
            grabbedBody.angularVelocity *= 0.94f;
        }

        public bool TryGetLiveBodyPosition(out Vector3 position)
        {
            if (TryGetLiveRagdollPose(out position, out _))
            {
                return true;
            }

            position = transform.position;
            return false;
        }

        public bool TryGetLiveRagdollPose(out Vector3 position, out Quaternion rotation)
        {
            position = transform.position;
            rotation = transform.rotation;
            if (activeRagdoll == null ||
                !activeRagdoll.enabled ||
                !activeRagdoll.IsAvailable ||
                activeRagdoll.State != BodyDriveState.Limp ||
                !activeRagdoll.TryGetCurrentRagdollCenterOfMass(out var liveCenterOfMass))
            {
                return false;
            }

            position = liveCenterOfMass;
            rotation = activeRagdoll.GetCurrentRagdollYaw();
            return true;
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

            cameraPitch = ClampFirstPersonPitch(cameraPitch - mouseY * movement.mouseTurnRate * Time.deltaTime, movement);
        }

        private static float ClampFirstPersonPitch(float pitch, PlayerMovementTuning movement)
        {
            var camera = GameRuntime.Balance != null ? GameRuntime.Balance.playerCamera : null;
            var fallbackLimit = movement != null ? Mathf.Abs(movement.maxLookPitch) : 80f;
            var configuredLimit = movement != null ? Mathf.Abs(movement.maxLookPitch) : fallbackLimit;
            var lookUpLimit = Mathf.Min(configuredLimit, camera != null ? camera.firstPersonLookUpLimit : 72f);
            var lookDownLimit = Mathf.Min(configuredLimit, camera != null ? camera.firstPersonLookDownLimit : 58f);
            return Mathf.Clamp(pitch, -lookUpLimit, lookDownLimit);
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
            RefreshGroundProbe();
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
            if (grounded)
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

        private void RefreshGroundProbe()
        {
            if (physicalCollider == null || rootBody == null || Time.time < ignoreGroundContactsUntil)
            {
                return;
            }

            var localBottom = physicalCollider.center.y - physicalCollider.height * 0.5f;
            var bottomWorld = transform.TransformPoint(new Vector3(physicalCollider.center.x, localBottom, physicalCollider.center.z));
            var probeOrigin = bottomWorld + Vector3.up * 0.55f;
            var hits = UnityEngine.Physics.RaycastAll(probeOrigin, Vector3.down, 0.55f + GroundProbeStickDistance, ~0, QueryTriggerInteraction.Ignore);
            var bestGap = float.MaxValue;
            var bestNormal = Vector3.up;
            var found = false;
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || hit.normal.y < MinGroundNormalY)
                {
                    continue;
                }

                var gap = bottomWorld.y - hit.point.y;
                if (gap < -GroundProbePenetrationTolerance || gap > GroundProbeStickDistance || Mathf.Abs(gap) >= Mathf.Abs(bestGap))
                {
                    continue;
                }

                bestGap = gap;
                bestNormal = hit.normal;
                found = true;
            }

            if (!found)
            {
                return;
            }

            groundedUntilTime = Time.time + 0.1f;
            lastGroundNormal = bestNormal;
            if (bestGap > 0.025f && rootBody.linearVelocity.y <= 0.2f)
            {
                var correction = Vector3.down * Mathf.Min(bestGap, GroundProbeMaxCorrectionPerStep);
                transform.position += correction;
                rootBody.position = transform.position;
            }
            else if (bestGap < -GroundProbePenetrationTolerance * 0.5f)
            {
                var correction = Vector3.up * Mathf.Min(-bestGap, GroundProbeMaxCorrectionPerStep);
                transform.position += correction;
                rootBody.position = transform.position;
            }
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
            if (impactOverlay == null)
            {
                impactOverlay = GetComponent<PlayerImpactOverlay>();
            }

            if (impactOverlay == null)
            {
                impactOverlay = gameObject.AddComponent<PlayerImpactOverlay>();
            }

            SnapRootToGround();
            playerCamera = GetComponentInChildren<Camera>(true);
            if (playerCamera == null)
            {
                Debug.LogError("Player prefab is missing its child Main Camera. Head-attached first-person camera cannot initialize.", this);
                return;
            }

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

            if (controller == null)
            {
                FitPhysicalColliderToVisibleBody();
            }

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
            var localBottom = transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)).y;
            var height = Mathf.Max(1.2f, bounds.size.y);
            var radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.38f, 0.22f, 0.45f);
            physicalCollider.center = new Vector3(localCenter.x, Mathf.Max(localBottom + height * 0.5f, radius), localCenter.z);
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
            groundedUntilTime = Time.time + 0.1f;
            lastGroundNormal = Vector3.up;
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
            HandleExternalImpactCollision(collision);

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

        private void OnCollisionEnter(Collision collision)
        {
            HandleExternalImpactCollision(collision, true);
        }

        public void HandleExternalImpactCollision(Collision collision, bool allowFallImpact = false)
        {
            if (!TryBuildImpactCollisionEvent(collision, allowFallImpact, out var impactEvent))
            {
                return;
            }

            TryBeginImpactRagdoll(
                impactEvent.Velocity,
                impactEvent.Point,
                impactEvent.SourceCollider,
                impactEvent.SourceMass);
        }

        private bool TryBuildImpactCollisionEvent(Collision collision, bool allowStaticSurfaceImpact, out ImpactCollisionEvent impactEvent)
        {
            impactEvent = default;
            if (collision == null || Balance == null || !Balance.ragdoll.impactWorldCollisionEnabled)
            {
                return false;
            }

            var ragdoll = Balance.ragdoll;
            var sourceBody = collision.rigidbody;
            if (sourceBody != null)
            {
                if (sourceBody == rootBody || sourceBody.transform.IsChildOf(transform) || sourceBody.mass < ragdoll.impactMinimumSourceMass)
                {
                    return false;
                }

                var sourceCollider = collision.collider != null ? collision.collider : sourceBody.GetComponent<Collider>();
                if (sourceCollider != null && sourceCollider.GetComponentInParent<PlayerController>() == this)
                {
                    return false;
                }

                var impactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
                var rigidbodyImpactVelocity = ResolveExternalImpactVelocity(collision, sourceBody, impactPoint);
                impactEvent = new ImpactCollisionEvent(rigidbodyImpactVelocity, impactPoint, sourceCollider, sourceBody.mass);
                return true;
            }

            if (!allowStaticSurfaceImpact || !ragdoll.impactFallRagdollEnabled || collision.contactCount <= 0)
            {
                return false;
            }

            var bestSupportNormal = Vector3.zero;
            var staticImpactPoint = collision.GetContact(0).point;
            for (var i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                if (contact.normal.y <= bestSupportNormal.y)
                {
                    continue;
                }

                bestSupportNormal = contact.normal;
                staticImpactPoint = contact.point;
            }

            if (bestSupportNormal.y < MinGroundNormalY)
            {
                return false;
            }

            var collisionSpeed = collision.relativeVelocity.magnitude;
            var downwardSpeed = Mathf.Max(0f, -Vector3.Dot(collision.relativeVelocity, bestSupportNormal));
            var impactSpeed = Mathf.Max(collisionSpeed, downwardSpeed) * Mathf.Max(0.01f, ragdoll.impactFallVelocityMultiplier);
            if (impactSpeed < ragdoll.impactFallVelocityThreshold)
            {
                return false;
            }

            var staticSurfaceImpactVelocity = bestSupportNormal.normalized * impactSpeed;
            if (TryGetVisibleBodyBounds(out var bodyBounds))
            {
                staticImpactPoint = bodyBounds.ClosestPoint(staticImpactPoint);
            }

            impactEvent = new ImpactCollisionEvent(
                staticSurfaceImpactVelocity,
                staticImpactPoint,
                collision.collider,
                Mathf.Max(1f, ragdoll.impactFallEffectiveMass));
            return true;
        }

        private Vector3 ResolveExternalImpactVelocity(Collision collision, Rigidbody sourceBody, Vector3 impactPoint)
        {
            var sourceVelocity = sourceBody.GetPointVelocity(impactPoint);
            var playerVelocity = rootBody != null && !rootBody.isKinematic
                ? rootBody.GetPointVelocity(impactPoint)
                : Vector3.zero;
            var pointVelocity = sourceVelocity - playerVelocity;
            return pointVelocity.sqrMagnitude >= collision.relativeVelocity.sqrMagnitude
                ? pointVelocity
                : collision.relativeVelocity;
        }

        private void EnsureImpactCollisionRelays()
        {
            var colliders = GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var childCollider = colliders[i];
                if (childCollider == null || childCollider == physicalCollider || childCollider.transform == transform || childCollider.isTrigger)
                {
                    continue;
                }

                if (!childCollider.TryGetComponent<PlayerImpactCollisionRelay>(out var relay) || relay == null)
                {
                    relay = childCollider.gameObject.AddComponent<PlayerImpactCollisionRelay>();
                }

                relay.SetOwner(this);
            }
        }

        private void ToggleBodyState()
        {
            if (IsRagdolled)
            {
                BeginDelayedRecovery();
                return;
            }

            var currentVelocity = rootBody != null ? rootBody.linearVelocity : Vector3.zero;
            var currentAngularVelocity = rootBody != null ? rootBody.angularVelocity : Vector3.zero;
            TryBeginRagdoll(new RagdollRequest(
                RagdollCause.Manual,
                currentVelocity,
                currentAngularVelocity,
                transform.forward * Mathf.Max(1.5f, currentVelocity.magnitude * 0.35f),
                transform.position + Vector3.up,
                false,
                ManualRecoveryDelaySeconds,
                0f,
                false,
                0f,
                false));
        }

        private bool TryBeginRagdoll(in RagdollRequest request)
        {
            if (Balance == null || IsDowned)
            {
                return false;
            }

            if (request.ApplyCooldown && Time.time < nextAllowedImpactRagdollTime)
            {
                return false;
            }

            if (request.ApplyCooldown)
            {
                nextAllowedImpactRagdollTime = Time.time + Mathf.Max(0f, request.CooldownSeconds);
            }

            if (activeRagdoll == null)
            {
                activeRagdoll = GetComponent<ActiveRagdollController>();
            }

            if (activeRagdoll != null)
            {
                activeRagdoll.enabled = true;
                if (activeRagdoll.EnsureInitialized())
                {
                    var wasAlreadyLimp = activeRagdoll.State == BodyDriveState.Limp;
                    if (!wasAlreadyLimp)
                    {
                        activeRagdoll.SetState(BodyDriveState.Limp);
                    }

                    recoveryPending = false;
                    EnsureImpactCollisionRelays();
                    SetSupportedLimbCollidersEnabled(true);
                    ApplyRagdollRequest(request, wasAlreadyLimp);
                    SetPhysicalRootEnabled(false);
                    cameraFollowVelocity = Vector3.zero;
                    RefreshCameraAttachment(true);
                    if (request.AutoRecover)
                    {
                        BeginImpactRecovery(request.ImpactSeverity01, request.RecoveryDelaySeconds);
                    }

                    return true;
                }

                activeRagdoll.enabled = false;
            }

            looseBody?.EnterLimpMode();
            if (request.AutoRecover)
            {
                BeginImpactRecovery(request.ImpactSeverity01, request.RecoveryDelaySeconds);
            }

            return looseBody != null && looseBody.IsLimp;
        }

        private void ApplyRagdollRequest(in RagdollRequest request, bool wasAlreadyLimp)
        {
            var shouldResetExistingLimpVelocity = request.ResetExistingLimpVelocity || request.Cause == RagdollCause.MonsterThrow;
            if (wasAlreadyLimp && !shouldResetExistingLimpVelocity)
            {
                activeRagdoll.AddLimpImpulseAtPoint(request.Impulse, request.ImpactPoint);
                return;
            }

            activeRagdoll.ApplyLimpImpact(request.LinearVelocity, request.AngularVelocity, request.Impulse, request.ImpactPoint);
        }

        private void BeginDelayedRecovery()
        {
            BeginDelayedRecovery(ManualRecoveryDelaySeconds);
        }

        private void BeginDelayedRecovery(float delaySeconds)
        {
            if (recoveryPending)
            {
                return;
            }

            impactRecovery.Reset();
            recoveryPending = true;
            recoverAtTime = Time.time + Mathf.Max(0.05f, delaySeconds);
            Debug.Log($"[Recovery] Recovery queued for {Mathf.Max(0.05f, delaySeconds):0.0}s from live ragdoll pose.", this);
        }

        private void BeginImpactRecovery(float severity01, float delaySeconds)
        {
            var ragdoll = Balance != null ? Balance.ragdoll : null;
            severity01 = Mathf.Clamp01(severity01);
            var accumulated01 = severity01;
            if (ragdoll != null && impactRecovery.Active)
            {
                accumulated01 = ragdoll.impactKnockoutAccumulationEnabled
                    ? impactRecovery.AccumulatedKnockout01 + severity01 * Mathf.Max(0f, ragdoll.impactKnockoutAccumulationGain)
                    : Mathf.Max(impactRecovery.AccumulatedKnockout01, severity01);
            }

            var maxAccumulated01 = ragdoll != null ? Mathf.Max(0.01f, ragdoll.impactKnockoutAccumulationMax) : 1f;
            accumulated01 = Mathf.Clamp(accumulated01, 0f, maxAccumulated01);
            var effectiveSeverity01 = Mathf.Clamp01(Mathf.Max(severity01, accumulated01));
            var delay = ragdoll != null
                ? Mathf.Lerp(ragdoll.impactRecoveryMinDelay, ragdoll.impactRecoveryMaxDelay, effectiveSeverity01)
                : Mathf.Max(0.05f, delaySeconds);

            impactRecovery.Active = true;
            impactRecovery.Severity01 = Mathf.Max(impactRecovery.Severity01, effectiveSeverity01);
            impactRecovery.AccumulatedKnockout01 = accumulated01;
            impactRecovery.PendingImpactDamage01 = Mathf.Max(impactRecovery.PendingImpactDamage01, accumulated01);
            impactRecovery.EarliestRecoveryTime = Mathf.Max(impactRecovery.EarliestRecoveryTime, Time.time + delay);
            impactRecovery.StableHoldSeconds = 0f;
            recoveryPending = true;
            recoverAtTime = impactRecovery.EarliestRecoveryTime;
            nextRecoveryStabilityDebugAt = recoverAtTime;
            impactOverlay?.ShowImpact(impactRecovery.Severity01, ragdoll);
            Debug.Log($"[Recovery] Impact recovery queued hitSeverity={severity01:0.00} accumulated={accumulated01:0.00} effective={impactRecovery.Severity01:0.00} earliest={delay:0.00}s pendingDamage01={impactRecovery.PendingImpactDamage01:0.00}.", this);
        }

        private void UpdatePendingRecovery()
        {
            if (!recoveryPending)
            {
                return;
            }

            if (IsAiDragPossessed)
            {
                return;
            }

            if (impactRecovery.Active)
            {
                if (!IsImpactRecoveryReady())
                {
                    return;
                }
            }
            else if (Time.time < recoverAtTime)
            {
                return;
            }

            recoveryPending = false;
            if (activeRagdoll == null || activeRagdoll.State != BodyDriveState.Limp)
            {
                if (looseBody != null && looseBody.IsLimp)
                {
                    looseBody.BeginRecovery();
                }

                ClearImpactRecoveryVisuals();
                return;
            }

            RecoverFromCurrentRagdollPose();
        }

        private bool IsImpactRecoveryReady()
        {
            if (Balance == null || IsAiDragPossessed || Time.time < impactRecovery.EarliestRecoveryTime)
            {
                return false;
            }

            if (activeRagdoll == null || !activeRagdoll.enabled || !activeRagdoll.IsAvailable)
            {
                return true;
            }

            if (!activeRagdoll.TryGetRagdollMotionStats(out var stats))
            {
                impactRecovery.StableHoldSeconds = 0f;
                return false;
            }

            var ragdoll = Balance.ragdoll;
            var hasSettledLinearMotion =
                stats.AverageLinearSpeed <= ragdoll.impactRecoveryMaxAverageSpeed &&
                stats.MaxLinearSpeed <= ragdoll.impactRecoveryMaxBodySpeed &&
                stats.CenterOfMassDriftSpeed <= ragdoll.impactRecoveryMaxComDriftSpeed;
            var hasGroundSupport = HasRecoveryGroundSupport(stats.CenterOfMass, ragdoll.impactRecoveryGroundProbeDistance);
            var stable =
                hasSettledLinearMotion &&
                stats.AverageAngularSpeed <= ragdoll.impactRecoveryMaxAngularSpeed &&
                hasGroundSupport;
            var graceReady =
                hasSettledLinearMotion &&
                hasGroundSupport &&
                stats.AverageAngularSpeed <= ragdoll.impactRecoveryMaxAngularSpeed * ImpactRecoveryAngularGraceMultiplier &&
                Time.time >= impactRecovery.EarliestRecoveryTime + ImpactRecoveryStabilityGraceSeconds;

            impactRecovery.StableHoldSeconds = stable
                ? impactRecovery.StableHoldSeconds + Time.deltaTime
                : 0f;
            if (graceReady)
            {
                Debug.Log($"[Recovery] Grace recovery allowed avgAngular={stats.AverageAngularSpeed:0.00}/{ragdoll.impactRecoveryMaxAngularSpeed:0.00} after waiting {Time.time - impactRecovery.EarliestRecoveryTime:0.00}s past earliest recovery.", this);
                impactRecovery.StableHoldSeconds = ragdoll.impactRecoveryStableHoldSeconds;
                return true;
            }

            if (!stable && Time.time >= nextRecoveryStabilityDebugAt)
            {
                Debug.Log($"[Recovery] Waiting for stability avgSpeed={stats.AverageLinearSpeed:0.00}/{ragdoll.impactRecoveryMaxAverageSpeed:0.00} maxSpeed={stats.MaxLinearSpeed:0.00}/{ragdoll.impactRecoveryMaxBodySpeed:0.00} avgAngular={stats.AverageAngularSpeed:0.00}/{ragdoll.impactRecoveryMaxAngularSpeed:0.00} comDrift={stats.CenterOfMassDriftSpeed:0.00}/{ragdoll.impactRecoveryMaxComDriftSpeed:0.00}.", this);
                nextRecoveryStabilityDebugAt = Time.time + RecoveryStabilityDebugInterval;
            }

            return impactRecovery.StableHoldSeconds >= ragdoll.impactRecoveryStableHoldSeconds;
        }

        private bool HasRecoveryGroundSupport(Vector3 centerOfMass, float probeDistance)
        {
            var hits = UnityEngine.Physics.RaycastAll(
                centerOfMass + Vector3.up * RecoveryCenterRayStartOffset,
                Vector3.down,
                Mathf.Max(0.1f, probeDistance),
                ~0,
                QueryTriggerInteraction.Ignore);
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider != null && !hit.collider.transform.IsChildOf(transform) && hit.normal.y >= MinGroundNormalY)
                {
                    return true;
                }
            }

            return false;
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
                RequeueBlockedRecovery();
                return;
            }

            var liePose = activeRagdoll.GetCurrentLiePose();
            var recoveryRotation = transform.rotation;

            Debug.Log($"[Recovery] Live COM={liveCenterOfMass} oldRoot={transform.position} unchangedYaw={recoveryRotation.eulerAngles.y:0.0} liePose={liePose}", this);
            if (!TryResolveRecoveryRootPosition(liveCenterOfMass, out var recoveryRootPosition))
            {
                Debug.LogWarning($"[Recovery] Blocked: no valid ground under live COM {liveCenterOfMass}. Staying ragdolled.", this);
                RequeueBlockedRecovery();
                return;
            }

            Debug.Log($"[Recovery] Resolved root={recoveryRootPosition} from live COM={liveCenterOfMass}", this);

            if (!IsStandingCapsuleClear(recoveryRootPosition))
            {
                Debug.LogWarning($"[Recovery] Blocked: standing capsule would overlap nearby geometry at {recoveryRootPosition}. Staying ragdolled instead of launching the player upward.", this);
                RequeueBlockedRecovery();
                return;
            }

            var sequence = ++nextRecoveryHandoffSequence;
            lastRequestedRecoveryHandoffSequence = sequence;
            ApplyRecoveryHandoff(recoveryRootPosition, recoveryRotation, true, "predicted");
            SendRecoveryHandoffForHostValidation(recoveryRootPosition, recoveryRotation.eulerAngles.y, liveCenterOfMass, sequence);
        }

        private void ApplyRecoveryHandoff(Vector3 recoveryRootPosition, Quaternion recoveryRotation, bool finalizeRagdoll, string source)
        {
            BeginRecoveryCameraBlend();
            visualLookYaw = 0f;
            targetYaw = recoveryRotation.eulerAngles.y;

            if (finalizeRagdoll && activeRagdoll != null && activeRagdoll.enabled && activeRagdoll.IsAvailable)
            {
                activeRagdoll.FreezeRagdollPoseForRecovery(recoveryRootPosition, recoveryRotation);
            }

            transform.SetPositionAndRotation(recoveryRootPosition, recoveryRotation);
            SetPhysicalRootEnabled(true);
            transform.SetPositionAndRotation(recoveryRootPosition, recoveryRotation);
            if (rootBody != null)
            {
                rootBody.position = recoveryRootPosition;
                rootBody.rotation = recoveryRotation;
                rootBody.linearVelocity = Vector3.zero;
                rootBody.angularVelocity = Vector3.zero;
            }

            ignoreGroundContactsUntil = Time.time + 0.05f;
            if (HasLocalAuthority)
            {
                BeginPostRecoveryWatch(recoveryRootPosition, recoveryRotation);
            }

            if (finalizeRagdoll)
            {
                activeRagdoll?.ReleaseRuntimeRagdollComponents();
                SetSupportedLimbCollidersEnabled(false);
                if (activeRagdoll != null)
                {
                    activeRagdoll.enabled = false;
                }

                ClearImpactRecoveryVisuals();
            }

            looseBody?.ResetAfterRagdoll(physicalCollider, true);
            cameraFollowVelocity = Vector3.zero;
            RefreshCameraAttachment();
            Debug.Log($"[Recovery] Applied {source} handoff root={recoveryRootPosition} yaw={recoveryRotation.eulerAngles.y:0.0}.", this);
        }

        private void SendRecoveryHandoffForHostValidation(Vector3 recoveryRootPosition, float yaw, Vector3 liveCenterOfMass, int sequence)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                return;
            }

            if (IsServer)
            {
                lastServerRecoveryHandoffSequence = Mathf.Max(lastServerRecoveryHandoffSequence, sequence);
                return;
            }

            RequestRecoveryHandoffServerRpc(recoveryRootPosition, yaw, liveCenterOfMass, sequence);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestRecoveryHandoffServerRpc(Vector3 requestedRoot, float requestedYaw, Vector3 submittedCenterOfMass, int sequence, RpcParams rpcParams = default)
        {
            if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            if (sequence <= lastServerRecoveryHandoffSequence)
            {
                Debug.LogWarning($"[Recovery] Ignored stale recovery handoff sequence={sequence} last={lastServerRecoveryHandoffSequence} from client={OwnerClientId}.", this);
                return;
            }

            lastServerRecoveryHandoffSequence = sequence;
            var accepted = TryValidateRecoveryHandoff(requestedRoot, requestedYaw, submittedCenterOfMass, out var approvedRoot, out var approvedYaw, out var reason);
            var approvedRotation = Quaternion.Euler(0f, approvedYaw, 0f);
            ApplyRecoveryHandoff(approvedRoot, approvedRotation, false, accepted ? "host-approved" : "host-corrected");
            ConfirmRecoveryHandoffClientRpc(sequence, accepted, approvedRoot, approvedYaw);

            if (accepted)
            {
                Debug.Log($"[Recovery] Host approved recovery sequence={sequence} client={OwnerClientId} root={approvedRoot} yaw={approvedYaw:0.0}.", this);
            }
            else
            {
                Debug.LogWarning($"[Recovery] Host corrected recovery sequence={sequence} client={OwnerClientId} reason={reason} requested={requestedRoot} approved={approvedRoot}.", this);
            }
        }

        [Rpc(SendTo.Owner)]
        private void ConfirmRecoveryHandoffClientRpc(int sequence, bool accepted, Vector3 approvedRoot, float approvedYaw)
        {
            if (!IsOwner || sequence < lastRequestedRecoveryHandoffSequence || sequence <= lastHandledRecoveryCorrectionSequence)
            {
                return;
            }

            lastHandledRecoveryCorrectionSequence = sequence;
            var approvedRotation = Quaternion.Euler(0f, approvedYaw, 0f);
            var correctionDistance = Vector3.Distance(transform.position, approvedRoot);
            var yawDelta = Quaternion.Angle(transform.rotation, approvedRotation);
            if (accepted && correctionDistance <= NetworkRecoveryCorrectionDistance && yawDelta <= 1f)
            {
                Debug.Log($"[Recovery] Host confirmed recovery sequence={sequence} root={approvedRoot} yaw={approvedYaw:0.0}.", this);
                return;
            }

            ApplyRecoveryHandoff(approvedRoot, approvedRotation, false, accepted ? "host-confirmed-correction" : "host-rejected-correction");
            if (accepted)
            {
                Debug.Log($"[Recovery] Host confirmation adjusted recovery sequence={sequence} distance={correctionDistance:0.000} yawDelta={yawDelta:0.0}.", this);
            }
            else
            {
                Debug.LogWarning($"[Recovery] Host rejected predicted recovery sequence={sequence}; corrected to root={approvedRoot} yaw={approvedYaw:0.0}.", this);
            }
        }

        private bool TryValidateRecoveryHandoff(Vector3 requestedRoot, float requestedYaw, Vector3 submittedCenterOfMass, out Vector3 approvedRoot, out float approvedYaw, out string reason)
        {
            approvedRoot = transform.position;
            approvedYaw = transform.eulerAngles.y;
            reason = string.Empty;

            if (IsDowned)
            {
                reason = "player is downed";
                return false;
            }

            if (IsHidden)
            {
                reason = "player is hidden";
                return false;
            }

            var requestedRootToCom = new Vector2(requestedRoot.x - submittedCenterOfMass.x, requestedRoot.z - submittedCenterOfMass.z).magnitude;
            if (requestedRootToCom > NetworkRecoveryMaxRootComHorizontalDistance)
            {
                reason = $"root/com horizontal delta {requestedRootToCom:0.00} exceeds {NetworkRecoveryMaxRootComHorizontalDistance:0.00}";
                return false;
            }

            if (Vector3.Distance(transform.position, requestedRoot) > NetworkRecoveryMaxHostRootDistance)
            {
                reason = $"root too far from host-known root distance={Vector3.Distance(transform.position, requestedRoot):0.00}";
                return false;
            }

            if (!TryResolveRecoveryRootPosition(submittedCenterOfMass, out var hostResolvedRoot))
            {
                reason = "no valid ground under submitted center of mass";
                return false;
            }

            if (Vector3.Distance(hostResolvedRoot, requestedRoot) > NetworkRecoveryMaxRootGroundDelta)
            {
                reason = $"requested root differs from host ground resolve by {Vector3.Distance(hostResolvedRoot, requestedRoot):0.00}";
                return false;
            }

            var previousRotation = transform.rotation;
            transform.rotation = Quaternion.Euler(0f, requestedYaw, 0f);
            var capsuleClear = IsStandingCapsuleClear(requestedRoot);
            transform.rotation = previousRotation;
            if (!capsuleClear)
            {
                reason = "standing capsule blocked";
                return false;
            }

            approvedRoot = requestedRoot;
            approvedYaw = requestedYaw;
            return true;
        }

        private void RequeueBlockedRecovery()
        {
            recoveryPending = true;
            recoverAtTime = Time.time + 0.25f;
            if (impactRecovery.Active)
            {
                impactRecovery.StableHoldSeconds = 0f;
                impactRecovery.EarliestRecoveryTime = Mathf.Max(impactRecovery.EarliestRecoveryTime, recoverAtTime);
            }
        }

        private void ClearImpactRecoveryVisuals()
        {
            impactOverlay?.Clear(Balance != null ? Balance.ragdoll.impactOverlayFadeOutSeconds : 1.25f);
            impactRecovery.Reset();
        }

        public void BeginAiDragPossession()
        {
            aiDragPossessionCount++;
            if (impactRecovery.Active)
            {
                impactRecovery.StableHoldSeconds = 0f;
            }
        }

        public void EndAiDragPossession()
        {
            aiDragPossessionCount = Mathf.Max(0, aiDragPossessionCount - 1);
            if (impactRecovery.Active)
            {
                impactRecovery.StableHoldSeconds = 0f;
            }
        }

        public bool TryApplyAiDragForce(Vector3 force, Vector3 worldPoint, ForceMode mode = ForceMode.Force)
        {
            return activeRagdoll != null &&
                   activeRagdoll.enabled &&
                   activeRagdoll.IsAvailable &&
                   activeRagdoll.State == BodyDriveState.Limp &&
                   activeRagdoll.TryAddLimpForceAtPoint(force, worldPoint, mode);
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

        private void HandleMonsterSpawnInput()
        {
            if (!Input.GetKeyDown(KeyCode.M))
            {
                return;
            }

            var content = GameRuntime.Content;
            if (content == null || content.monsterPrefab == null)
            {
                Debug.LogWarning("[MonsterPrototype] Cannot spawn monster because GameRuntime.Content or monsterPrefab is missing.", this);
                return;
            }

            var forward = playerCamera != null ? playerCamera.transform.forward : transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = transform.forward;
            }

            forward.Normalize();
            var spawnPosition = transform.position + forward * 4f;
            spawnPosition.y = transform.position.y;
            var monsterObject = Instantiate(content.monsterPrefab, spawnPosition, Quaternion.LookRotation(-forward, Vector3.up));
            if (!monsterObject.TryGetComponent<MonsterController>(out var monster) || monster == null)
            {
                Debug.LogWarning("[MonsterPrototype] Spawned monster prefab has no MonsterController.", monsterObject);
                return;
            }

            var definitionIndex = content.monsterPool.Count > 0 ? 0 : -1;
            monster.Initialize(definitionIndex, content, Array.Empty<Transform>());
            var networkObject = monsterObject.GetComponent<NetworkObject>();
            if (networkObject != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsServer && !networkObject.IsSpawned)
            {
                networkObject.Spawn(true);
            }

            Debug.Log($"[MonsterPrototype] Spawned monster with M at {spawnPosition}.", monsterObject);
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
            ball.AddComponent<GroceryQuotaHorror.Physics.PhysicsImpactAudio>();
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

            if (!TryFindGrabbedRagdoll(out var body, out var ragdoll, out var grabPoint))
            {
                return;
            }

            grabbedBody = body;
            grabbedRagdoll = ragdoll;
            grabbedLocalPoint = grabbedBody.transform.InverseTransformPoint(grabPoint);
            lastGrabbedTargetPosition = playerCamera.transform.position + playerCamera.transform.forward * Balance.interaction.grabbedHoldDistance;
            grabbedTargetVelocity = Vector3.zero;
            grabbedRagdoll.SetGrabbed(true);
            SetGrabbedRagdollPlayerCollisionIgnored(true);
            UpdateLooseBodyGrabTarget();
        }

        private void UpdateLooseBodyGrabTarget()
        {
            if (looseBody == null)
            {
                return;
            }

            if (grabbedBody == null)
            {
                looseBody.ClearGrabTarget();
                return;
            }

            looseBody.SetGrabTarget(grabbedBody.transform.TransformPoint(grabbedLocalPoint));
        }

        private bool TryFindGrabbedRagdoll(out Rigidbody body, out SpawnableRagdoll ragdoll, out Vector3 grabPoint)
        {
            body = null;
            ragdoll = null;
            grabPoint = Vector3.zero;

            if (playerCamera == null || Balance == null)
            {
                return false;
            }

            var hits = UnityEngine.Physics.SphereCastAll(
                playerCamera.transform.position,
                0.22f,
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

                grabPoint = hit.point;
                if ((grabPoint - body.worldCenterOfMass).sqrMagnitude < 0.0001f)
                {
                    grabPoint = body.worldCenterOfMass;
                }

                return body != null;
            }

            return false;
        }

        private void ReleaseGrabbedBody(bool applyThrow)
        {
            if (grabbedRagdoll != null)
            {
                SetGrabbedRagdollPlayerCollisionIgnored(false);
                grabbedRagdoll.SetGrabbed(false);
            }

            if (grabbedBody != null && applyThrow && playerCamera != null && Balance != null)
            {
                grabbedBody.linearVelocity = grabbedTargetVelocity + playerCamera.transform.forward * Balance.interaction.throwImpulse;
            }

            grabbedBody = null;
            grabbedRagdoll = null;
            grabbedLocalPoint = Vector3.zero;
            grabbedTargetVelocity = Vector3.zero;
            looseBody?.ClearGrabTarget();
        }

        private void SetGrabbedRagdollPlayerCollisionIgnored(bool ignored)
        {
            if (grabbedRagdoll == null)
            {
                return;
            }

            var playerColliders = GetComponentsInChildren<Collider>(true);
            var grabbedColliders = grabbedRagdoll.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < playerColliders.Length; i++)
            {
                var playerCollider = playerColliders[i];
                if (playerCollider == null)
                {
                    continue;
                }

                for (var j = 0; j < grabbedColliders.Length; j++)
                {
                    var grabbedCollider = grabbedColliders[j];
                    if (grabbedCollider != null && grabbedCollider != playerCollider)
                    {
                        UnityEngine.Physics.IgnoreCollision(playerCollider, grabbedCollider, ignored);
                    }
                }
            }
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

            if (looseBody != null && looseBody.AttachCamera(playerCamera, cameraPitch, transform.eulerAngles.y + visualLookYaw, Balance))
            {
                cameraFollowVelocity = Vector3.zero;
                ApplyRecoveryCameraBlend();
                return;
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

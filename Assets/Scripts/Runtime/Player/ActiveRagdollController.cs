using System;
using System.Collections.Generic;
using GroceryQuotaHorror.Data;
using UnityEngine;

namespace GroceryQuotaHorror.Player
{
    public enum RagdollLiePose
    {
        Unknown,
        FaceUp,
        FaceDown,
        Side
    }

    public readonly struct RagdollMotionStats
    {
        public RagdollMotionStats(Vector3 centerOfMass, float averageLinearSpeed, float maxLinearSpeed, float averageAngularSpeed, float centerOfMassDriftSpeed)
        {
            CenterOfMass = centerOfMass;
            AverageLinearSpeed = averageLinearSpeed;
            MaxLinearSpeed = maxLinearSpeed;
            AverageAngularSpeed = averageAngularSpeed;
            CenterOfMassDriftSpeed = centerOfMassDriftSpeed;
        }

        public Vector3 CenterOfMass { get; }
        public float AverageLinearSpeed { get; }
        public float MaxLinearSpeed { get; }
        public float AverageAngularSpeed { get; }
        public float CenterOfMassDriftSpeed { get; }
    }

    [DefaultExecutionOrder(-100)]
    public sealed class ActiveRagdollController : MonoBehaviour
    {
        [Serializable]
        private struct BoneConfig
        {
            public HumanBodyBones bone;
            public HumanBodyBones parentBone;
            public PrimitiveType colliderType;
            public Vector3 colliderCenter;
            public Vector3 colliderSize;
            public float mass;
            public HumanBodyBones childHint;
        }

        [SerializeField] private Transform modelRoot;
        [SerializeField] private float jointSwingLimit = 25f;
        [SerializeField] private float jointTwistLimit = 20f;

        private static readonly BoneConfig[] BoneConfigs =
        {
            new() { bone = HumanBodyBones.Hips, parentBone = HumanBodyBones.LastBone, colliderType = PrimitiveType.Cube, colliderCenter = new Vector3(0f, 0.04f, 0f), colliderSize = new Vector3(0.3f, 0.2f, 0.24f), mass = 2.6f, childHint = HumanBodyBones.Spine },
            new() { bone = HumanBodyBones.Spine, parentBone = HumanBodyBones.Hips, colliderType = PrimitiveType.Cube, colliderCenter = new Vector3(0f, 0.1f, 0f), colliderSize = new Vector3(0.3f, 0.24f, 0.24f), mass = 2f, childHint = HumanBodyBones.Chest },
            new() { bone = HumanBodyBones.Chest, parentBone = HumanBodyBones.Spine, colliderType = PrimitiveType.Cube, colliderCenter = new Vector3(0f, 0.11f, 0.02f), colliderSize = new Vector3(0.36f, 0.28f, 0.28f), mass = 2.2f, childHint = HumanBodyBones.Head },
            new() { bone = HumanBodyBones.Head, parentBone = HumanBodyBones.Chest, colliderType = PrimitiveType.Sphere, colliderCenter = new Vector3(0f, 0.08f, 0f), colliderSize = new Vector3(0.18f, 0.18f, 0.18f), mass = 1f },
            new() { bone = HumanBodyBones.LeftShoulder, parentBone = HumanBodyBones.Chest, colliderType = PrimitiveType.Sphere, colliderCenter = new Vector3(-0.04f, 0f, 0f), colliderSize = new Vector3(0.12f, 0.12f, 0.12f), mass = 0.3f, childHint = HumanBodyBones.LeftUpperArm },
            new() { bone = HumanBodyBones.LeftUpperArm, parentBone = HumanBodyBones.LeftShoulder, colliderType = PrimitiveType.Capsule, colliderCenter = new Vector3(-0.12f, 0f, 0f), colliderSize = new Vector3(0.09f, 0.28f, 0.09f), mass = 0.7f, childHint = HumanBodyBones.LeftLowerArm },
            new() { bone = HumanBodyBones.LeftLowerArm, parentBone = HumanBodyBones.LeftUpperArm, colliderType = PrimitiveType.Capsule, colliderCenter = new Vector3(-0.14f, 0f, 0f), colliderSize = new Vector3(0.1f, 0.28f, 0.1f), mass = 0.6f, childHint = HumanBodyBones.LeftHand },
            new() { bone = HumanBodyBones.LeftHand, parentBone = HumanBodyBones.LeftLowerArm, colliderType = PrimitiveType.Sphere, colliderCenter = new Vector3(-0.04f, 0f, 0f), colliderSize = new Vector3(0.1f, 0.1f, 0.1f), mass = 0.25f },
            new() { bone = HumanBodyBones.RightShoulder, parentBone = HumanBodyBones.Chest, colliderType = PrimitiveType.Sphere, colliderCenter = new Vector3(0.04f, 0f, 0f), colliderSize = new Vector3(0.12f, 0.12f, 0.12f), mass = 0.3f, childHint = HumanBodyBones.RightUpperArm },
            new() { bone = HumanBodyBones.RightUpperArm, parentBone = HumanBodyBones.RightShoulder, colliderType = PrimitiveType.Capsule, colliderCenter = new Vector3(0.12f, 0f, 0f), colliderSize = new Vector3(0.09f, 0.28f, 0.09f), mass = 0.7f, childHint = HumanBodyBones.RightLowerArm },
            new() { bone = HumanBodyBones.RightLowerArm, parentBone = HumanBodyBones.RightUpperArm, colliderType = PrimitiveType.Capsule, colliderCenter = new Vector3(0.14f, 0f, 0f), colliderSize = new Vector3(0.1f, 0.28f, 0.1f), mass = 0.6f, childHint = HumanBodyBones.RightHand },
            new() { bone = HumanBodyBones.RightHand, parentBone = HumanBodyBones.RightLowerArm, colliderType = PrimitiveType.Sphere, colliderCenter = new Vector3(0.04f, 0f, 0f), colliderSize = new Vector3(0.1f, 0.1f, 0.1f), mass = 0.25f },
            new() { bone = HumanBodyBones.LeftUpperLeg, parentBone = HumanBodyBones.Hips, colliderType = PrimitiveType.Capsule, colliderCenter = new Vector3(0f, -0.2f, 0f), colliderSize = new Vector3(0.1f, 0.42f, 0.1f), mass = 1.2f, childHint = HumanBodyBones.LeftLowerLeg },
            new() { bone = HumanBodyBones.LeftLowerLeg, parentBone = HumanBodyBones.LeftUpperLeg, colliderType = PrimitiveType.Capsule, colliderCenter = new Vector3(0f, -0.18f, 0f), colliderSize = new Vector3(0.08f, 0.38f, 0.08f), mass = 1f, childHint = HumanBodyBones.LeftFoot },
            new() { bone = HumanBodyBones.RightUpperLeg, parentBone = HumanBodyBones.Hips, colliderType = PrimitiveType.Capsule, colliderCenter = new Vector3(0f, -0.2f, 0f), colliderSize = new Vector3(0.1f, 0.42f, 0.1f), mass = 1.2f, childHint = HumanBodyBones.RightLowerLeg },
            new() { bone = HumanBodyBones.RightLowerLeg, parentBone = HumanBodyBones.RightUpperLeg, colliderType = PrimitiveType.Capsule, colliderCenter = new Vector3(0f, -0.18f, 0f), colliderSize = new Vector3(0.08f, 0.38f, 0.08f), mass = 1f, childHint = HumanBodyBones.RightFoot },
            new() { bone = HumanBodyBones.LeftFoot, parentBone = HumanBodyBones.LeftLowerLeg, colliderType = PrimitiveType.Cube, colliderCenter = new Vector3(0f, 0f, 0.08f), colliderSize = new Vector3(0.09f, 0.08f, 0.22f), mass = 0.4f, childHint = HumanBodyBones.LeftToes },
            new() { bone = HumanBodyBones.RightFoot, parentBone = HumanBodyBones.RightLowerLeg, colliderType = PrimitiveType.Cube, colliderCenter = new Vector3(0f, 0f, 0.08f), colliderSize = new Vector3(0.09f, 0.08f, 0.22f), mass = 0.4f, childHint = HumanBodyBones.RightToes }
        };

        private static readonly HumanBodyBones[] VisualBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.UpperChest,
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.Neck,
            HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot
        };

        private readonly Dictionary<HumanBodyBones, Rigidbody> rigidBodies = new();
        private readonly Dictionary<HumanBodyBones, Quaternion> baseLocalRotations = new();
        private readonly Dictionary<HumanBodyBones, Vector3> baseLocalPositions = new();
        private readonly Dictionary<HumanBodyBones, Vector3> baseRootLocalPositions = new();
        private readonly List<Collider> ragdollColliders = new();
        private readonly List<CharacterJoint> ragdollJoints = new();
        private readonly ProceduralPoseSource proceduralPoseSource = new();

        private Animator animator;
        private Collider[] rootColliders;
        private IPlayerPoseSource poseSource;
        private BodyDriveState state = BodyDriveState.Supported;
        private bool initialized;
        private bool ragdollBuilt;
        private Vector3 lastRootPosition;
        private float recentImpact;
        private float recoveryTimer;
        private Vector3 cachedLocalMove;
        private float cachedSpeed01;
        private bool cachedSprinting;
        private bool cachedGrounded;
        private float cachedLookYaw;
        private float cachedLookPitch;
        private Vector3 hipsAnchorLocalPosition;
        private PlayerPoseContext lastSupportedContext;
        private Vector3 lastMotionStatsCenterOfMass;
        private float lastMotionStatsTime;
        private bool hasLastMotionStats;

        public BodyDriveState State => state;
        public bool IsAvailable => ragdollBuilt;
        public Transform ModelRoot => modelRoot;
        public Transform HeadTransform => GetBoneTransform(HumanBodyBones.Head);

        private void Awake()
        {
            // The controllable player no longer builds ragdoll physics on its visible skinned bones at startup.
            // Building joints directly on the render rig can stretch the mesh badly; initialize explicitly only
            // for isolated ragdoll experiments until the player gets a separate physics puppet rig.
        }

        public bool EnsureInitialized()
        {
            if (initialized)
            {
                return ragdollBuilt;
            }

            initialized = true;
            if (modelRoot == null)
            {
                modelRoot = transform.Find("VisualRoot");
            }

            if (modelRoot == null && transform.childCount > 0)
            {
                modelRoot = transform.GetChild(0);
            }

            rootColliders = GetComponents<Collider>();
            animator = modelRoot != null ? modelRoot.GetComponentInChildren<Animator>() : null;
            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning($"{name} is missing a humanoid animator for active ragdoll setup.", this);
                return false;
            }

            poseSource = proceduralPoseSource;
            CacheBaseLocalRotations();
            ragdollBuilt = BuildRagdoll();
            if (!ragdollBuilt)
            {
                Debug.LogWarning($"{name} could not finish active ragdoll setup. The player will stay in supported character mode until the rig setup succeeds.", this);
                return false;
            }

            IgnoreRootCollisions();
            IgnoreRagdollSelfCollisions();
            SetState(BodyDriveState.Supported, true);
            lastRootPosition = transform.position;
            return ragdollBuilt;
        }

        private void FixedUpdate()
        {
            if (!ragdollBuilt || animator == null || GameRuntime.Balance == null)
            {
                return;
            }

            var displacement = transform.position - lastRootPosition;
            lastRootPosition = transform.position;
            recentImpact = Mathf.Max(0f, recentImpact - Time.fixedDeltaTime * 3f);
            recentImpact = Mathf.Max(recentImpact, displacement.magnitude / Mathf.Max(Time.fixedDeltaTime, 0.0001f) * 0.004f);

            if (state == BodyDriveState.Limp)
            {
                ApplyLimpState();
                return;
            }

            var context = BuildPoseContext(Time.fixedDeltaTime, true);
            lastSupportedContext = context;
            ApplySupportedState(context, true);
            ApplySupportedDynamicLimbState(context);
        }

        private void LateUpdate()
        {
            if (!ragdollBuilt || animator == null || GameRuntime.Balance == null || state == BodyDriveState.Limp)
            {
                return;
            }

            ApplySupportedState(lastSupportedContext, false);
        }

        public void SetState(BodyDriveState nextState, bool instant = false)
        {
            if (!ragdollBuilt)
            {
                return;
            }

            if (nextState == BodyDriveState.Limp)
            {
                SyncRigidbodiesToCurrentPose();
                hasLastMotionStats = false;
            }

            state = nextState;
            recoveryTimer = instant ? 1f : 0f;
            ConfigureBodiesForState();
        }

        public void ToggleLimpState()
        {
            if (!ragdollBuilt)
            {
                return;
            }

            SetState(state == BodyDriveState.Limp ? BodyDriveState.Supported : BodyDriveState.Limp);
        }

        public void UpdateSupportedMotion(Vector3 localMove, float speed01, bool sprinting, bool grounded, float lookYaw, float lookPitch)
        {
            cachedLocalMove = localMove;
            cachedSpeed01 = speed01;
            cachedSprinting = sprinting;
            cachedGrounded = grounded;
            cachedLookYaw = lookYaw;
            cachedLookPitch = lookPitch;
        }

        public void ApplyHeadLookInfluence(float mouseX, float mouseY)
        {
            if (!ragdollBuilt || state != BodyDriveState.Limp || GameRuntime.Balance == null)
            {
                return;
            }

            var headBody = GetBoneRigidbody(HumanBodyBones.Head);
            if (headBody == null)
            {
                return;
            }

            var ragdoll = GameRuntime.Balance.ragdoll;
            var headAngularSpeed = headBody.angularVelocity.magnitude;
            var headTorqueScale = headAngularSpeed >= ragdoll.limpHeadMaxAngularSpeed
                ? 0f
                : 1f - Mathf.Clamp01(headAngularSpeed / Mathf.Max(0.01f, ragdoll.limpHeadMaxAngularSpeed));
            var torque = (transform.up * mouseX + transform.right * -mouseY) * ragdoll.headLookTorque;
            if (headTorqueScale > 0.001f)
            {
                headBody.AddTorque(torque * headTorqueScale, ForceMode.Acceleration);
                headBody.AddForce(Vector3.up * (Mathf.Abs(mouseY) * ragdoll.headLiftForce * headTorqueScale), ForceMode.Acceleration);
            }

            var neckBody = GetBoneRigidbody(HumanBodyBones.Neck);
            if (neckBody != null)
            {
                var neckAngularSpeed = neckBody.angularVelocity.magnitude;
                var neckTorqueScale = neckAngularSpeed >= ragdoll.limpNeckMaxAngularSpeed
                    ? 0f
                    : 1f - Mathf.Clamp01(neckAngularSpeed / Mathf.Max(0.01f, ragdoll.limpNeckMaxAngularSpeed));
                if (neckTorqueScale > 0.001f)
                {
                    neckBody.AddTorque(torque * ragdoll.neckLookTorqueScale * neckTorqueScale, ForceMode.Acceleration);
                }
            }

            var chestBody = GetBoneRigidbody(HumanBodyBones.UpperChest) ?? GetBoneRigidbody(HumanBodyBones.Chest);
            if (chestBody != null)
            {
                chestBody.AddTorque(torque * ragdoll.chestLookTorqueScale, ForceMode.Acceleration);
            }
        }

        public void ApplyLimpVelocity(Vector3 linearVelocity, Vector3 angularVelocity, Vector3 impulse)
        {
            if (!ragdollBuilt || state != BodyDriveState.Limp)
            {
                return;
            }

            foreach (var body in rigidBodies.Values)
            {
                if (body == null || body.isKinematic)
                {
                    continue;
                }

                body.linearVelocity = linearVelocity;
                body.angularVelocity = angularVelocity;
                if (impulse.sqrMagnitude > 0.0001f)
                {
                    body.AddForce(impulse, ForceMode.Impulse);
                }

                body.WakeUp();
            }
        }

        public void ApplyLimpImpact(Vector3 wholeBodyVelocity, Vector3 angularVelocity, Vector3 impulse, Vector3 worldPoint)
        {
            if (!ragdollBuilt || state != BodyDriveState.Limp)
            {
                return;
            }

            foreach (var body in rigidBodies.Values)
            {
                if (body == null || body.isKinematic)
                {
                    continue;
                }

                body.linearVelocity = wholeBodyVelocity;
                body.angularVelocity = angularVelocity;
                body.WakeUp();
            }

            AddLimpImpulseAtPoint(impulse, worldPoint);
        }

        public void AddLimpImpulse(Vector3 impulse)
        {
            if (!ragdollBuilt || state != BodyDriveState.Limp || impulse.sqrMagnitude < 0.0001f)
            {
                return;
            }

            foreach (var body in rigidBodies.Values)
            {
                if (body == null || body.isKinematic)
                {
                    continue;
                }

                body.AddForce(impulse, ForceMode.Impulse);
                body.WakeUp();
            }
        }

        public void AddLimpImpulseAtPoint(Vector3 impulse, Vector3 worldPoint)
        {
            if (!ragdollBuilt || state != BodyDriveState.Limp || impulse.sqrMagnitude < 0.0001f)
            {
                return;
            }

            if (!TryGetClosestRigidbody(worldPoint, out var targetBody) || targetBody == null || targetBody.isKinematic)
            {
                AddLimpImpulse(impulse);
                return;
            }

            var ragdoll = GameRuntime.Balance != null ? GameRuntime.Balance.ragdoll : null;
            var forcePointRadius = ragdoll != null ? ragdoll.impactForcePointRadius : 0.55f;
            var nearbyShare = ragdoll != null ? ragdoll.impactNearbyImpulseShare : 0.18f;
            var nearbyRadius = ragdoll != null ? ragdoll.impactNearbyImpulseRadius : 1.15f;
            var forcePoint = targetBody.position + Vector3.ClampMagnitude(worldPoint - targetBody.position, forcePointRadius);
            targetBody.AddForceAtPosition(impulse, forcePoint, ForceMode.Impulse);
            targetBody.WakeUp();

            var sharedImpulse = impulse * nearbyShare;
            foreach (var body in rigidBodies.Values)
            {
                if (body == null || body == targetBody || body.isKinematic)
                {
                    continue;
                }

                var distance = Vector3.Distance(body.worldCenterOfMass, worldPoint);
                var falloff = Mathf.Clamp01(1f - distance / Mathf.Max(0.01f, nearbyRadius));
                if (falloff <= 0f)
                {
                    continue;
                }

                body.AddForce(sharedImpulse * falloff, ForceMode.Impulse);
                body.WakeUp();
            }
        }

        public bool TryAddLimpForceAtPoint(Vector3 force, Vector3 worldPoint, ForceMode mode = ForceMode.Force)
        {
            if (!ragdollBuilt || state != BodyDriveState.Limp || force.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            if (!TryGetClosestRigidbody(worldPoint, out var targetBody) || targetBody == null || targetBody.isKinematic)
            {
                return false;
            }

            var ragdoll = GameRuntime.Balance != null ? GameRuntime.Balance.ragdoll : null;
            var forcePointRadius = ragdoll != null ? ragdoll.impactForcePointRadius : 0.55f;
            var forcePoint = targetBody.position + Vector3.ClampMagnitude(worldPoint - targetBody.position, forcePointRadius);
            targetBody.AddForceAtPosition(force, forcePoint, mode);
            targetBody.WakeUp();
            return true;
        }

        public Rigidbody GetBoneRigidbody(HumanBodyBones bone)
        {
            return rigidBodies.TryGetValue(bone, out var body) ? body : null;
        }

        public bool TryGetClosestRigidbody(Vector3 worldPoint, out Rigidbody closestBody)
        {
            closestBody = null;
            var bestDistance = float.MaxValue;
            foreach (var body in rigidBodies.Values)
            {
                if (body == null)
                {
                    continue;
                }

                var closestPoint = body.worldCenterOfMass;
                var colliders = body.GetComponents<Collider>();
                for (var i = 0; i < colliders.Length; i++)
                {
                    var collider = colliders[i];
                    if (collider == null || !collider.enabled)
                    {
                        continue;
                    }

                    var colliderPoint = collider.ClosestPoint(worldPoint);
                    if ((colliderPoint - worldPoint).sqrMagnitude < (closestPoint - worldPoint).sqrMagnitude)
                    {
                        closestPoint = colliderPoint;
                    }
                }

                var distance = (closestPoint - worldPoint).sqrMagnitude;
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                closestBody = body;
            }

            return closestBody != null;
        }

        public bool TryGetCurrentRagdollCenterOfMass(out Vector3 centerOfMass)
        {
            centerOfMass = Vector3.zero;
            var totalMass = 0f;
            foreach (var body in rigidBodies.Values)
            {
                if (body == null)
                {
                    continue;
                }

                var mass = Mathf.Max(0.01f, body.mass);
                centerOfMass += body.worldCenterOfMass * mass;
                totalMass += mass;
            }

            if (totalMass <= 0f)
            {
                return false;
            }

            centerOfMass /= totalMass;
            return true;
        }

        public bool TryGetRagdollMotionStats(out RagdollMotionStats stats)
        {
            stats = default;
            var centerOfMass = Vector3.zero;
            var totalMass = 0f;
            var stabilityLinearSpeed = 0f;
            var stabilityMaxLinearSpeed = 0f;
            var stabilityAngularSpeed = 0f;
            var stabilityBodies = 0;
            foreach (var pair in rigidBodies)
            {
                var body = pair.Value;
                if (body == null || body.isKinematic)
                {
                    continue;
                }

                var mass = Mathf.Max(0.01f, body.mass);
                centerOfMass += body.worldCenterOfMass * mass;
                totalMass += mass;

                if (!IsRecoveryStabilityBone(pair.Key))
                {
                    continue;
                }

                var currentLinearSpeed = body.linearVelocity.magnitude;
                stabilityLinearSpeed += currentLinearSpeed;
                stabilityMaxLinearSpeed = Mathf.Max(stabilityMaxLinearSpeed, currentLinearSpeed);
                stabilityAngularSpeed += body.angularVelocity.magnitude;
                stabilityBodies++;
            }

            if (stabilityBodies <= 0 || totalMass <= 0f)
            {
                return false;
            }

            centerOfMass /= totalMass;
            var now = Time.time;
            var driftSpeed = 0f;
            if (hasLastMotionStats)
            {
                driftSpeed = Vector3.Distance(centerOfMass, lastMotionStatsCenterOfMass) / Mathf.Max(0.0001f, now - lastMotionStatsTime);
            }

            lastMotionStatsCenterOfMass = centerOfMass;
            lastMotionStatsTime = now;
            hasLastMotionStats = true;
            stats = new RagdollMotionStats(
                centerOfMass,
                stabilityLinearSpeed / stabilityBodies,
                stabilityMaxLinearSpeed,
                stabilityAngularSpeed / stabilityBodies,
                driftSpeed);
            return true;
        }

        private static bool IsRecoveryStabilityBone(HumanBodyBones bone)
        {
            return bone != HumanBodyBones.Head &&
                   bone != HumanBodyBones.Neck &&
                   bone != HumanBodyBones.LeftHand &&
                   bone != HumanBodyBones.RightHand &&
                   bone != HumanBodyBones.LeftFoot &&
                   bone != HumanBodyBones.RightFoot;
        }

        public Quaternion GetCurrentRagdollYaw()
        {
            return ragdollBuilt ? CalculateCurrentBodyYaw() : Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }

        public RagdollLiePose GetCurrentLiePose()
        {
            if (!ragdollBuilt)
            {
                return RagdollLiePose.Unknown;
            }

            if (!TryGetBoneUp(HumanBodyBones.Chest, out var torsoUp) &&
                !TryGetBoneUp(HumanBodyBones.Spine, out torsoUp) &&
                !TryGetBoneUp(HumanBodyBones.Hips, out torsoUp))
            {
                return RagdollLiePose.Unknown;
            }

            var upDot = Vector3.Dot(torsoUp.normalized, Vector3.up);
            if (upDot > 0.45f)
            {
                return RagdollLiePose.FaceUp;
            }

            if (upDot < -0.45f)
            {
                return RagdollLiePose.FaceDown;
            }

            return RagdollLiePose.Side;
        }

        private bool TryGetBoneUp(HumanBodyBones boneType, out Vector3 up)
        {
            up = Vector3.zero;
            var body = GetBoneRigidbody(boneType);
            var bone = GetBoneTransform(boneType);
            if (body != null)
            {
                up = body.transform.up;
            }

            if (up.sqrMagnitude < 0.0001f && bone != null)
            {
                up = bone.up;
            }

            if (up.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            up.Normalize();
            return true;
        }

        public Transform GetBoneTransform(HumanBodyBones bone)
        {
            return animator != null ? animator.GetBoneTransform(bone) : null;
        }

        public void FreezeRagdollPoseForRecovery(Vector3 rootPosition, Quaternion rootRotation)
        {
            if (!ragdollBuilt || animator == null)
            {
                return;
            }

            var capturedBonePositions = new Dictionary<HumanBodyBones, Vector3>();
            var capturedBoneRotations = new Dictionary<HumanBodyBones, Quaternion>();
            for (var i = 0; i < VisualBones.Length; i++)
            {
                var boneType = VisualBones[i];
                var bone = animator.GetBoneTransform(boneType);
                if (bone == null)
                {
                    continue;
                }

                capturedBonePositions[boneType] = bone.position;
                capturedBoneRotations[boneType] = bone.rotation;
            }

            transform.SetPositionAndRotation(rootPosition, rootRotation);
            foreach (var pair in capturedBonePositions)
            {
                var bone = animator.GetBoneTransform(pair.Key);
                if (bone == null)
                {
                    continue;
                }

                bone.SetPositionAndRotation(pair.Value, capturedBoneRotations[pair.Key]);
            }

            for (var i = 0; i < ragdollColliders.Count; i++)
            {
                if (ragdollColliders[i] != null)
                {
                    ragdollColliders[i].enabled = false;
                }
            }

            foreach (var body in rigidBodies.Values)
            {
                if (body == null)
                {
                    continue;
                }

                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                body.isKinematic = true;
                body.useGravity = false;
                body.detectCollisions = false;
            }

            state = BodyDriveState.Supported;
            lastRootPosition = transform.position;
            UnityEngine.Physics.SyncTransforms();
        }

        private Quaternion CalculateCurrentBodyYaw()
        {
            var forward = Vector3.zero;
            var chestBody = GetBoneRigidbody(HumanBodyBones.Chest);
            var headBody = GetBoneRigidbody(HumanBodyBones.Head);
            if (chestBody != null)
            {
                forward += Vector3.ProjectOnPlane(chestBody.transform.forward, Vector3.up);
            }

            if (headBody != null)
            {
                forward += Vector3.ProjectOnPlane(headBody.transform.forward, Vector3.up) * 0.35f;
            }

            if (forward.sqrMagnitude < 0.0001f)
            {
                var chest = GetBoneTransform(HumanBodyBones.Chest);
                var hips = GetBoneTransform(HumanBodyBones.Hips);
                if (chest != null && hips != null)
                {
                    forward = Vector3.ProjectOnPlane(chest.position - hips.position, Vector3.up);
                }
            }

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = transform.forward;
            }

            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        public void ReleaseRuntimeRagdollComponents()
        {
            for (var i = 0; i < ragdollJoints.Count; i++)
            {
                if (ragdollJoints[i] != null)
                {
                    ragdollJoints[i].connectedBody = null;
                    Destroy(ragdollJoints[i]);
                }
            }

            for (var i = 0; i < ragdollColliders.Count; i++)
            {
                if (ragdollColliders[i] != null)
                {
                    Destroy(ragdollColliders[i]);
                }
            }

            foreach (var body in rigidBodies.Values)
            {
                if (body != null)
                {
                    body.isKinematic = true;
                    body.useGravity = false;
                    body.detectCollisions = false;
                    Destroy(body);
                }
            }

            ragdollJoints.Clear();
            ragdollColliders.Clear();
            rigidBodies.Clear();
            ragdollBuilt = false;
            initialized = false;
        }

        public bool TryGetCameraTargetPose(float lookPitch, out Vector3 targetPosition, out Quaternion targetRotation)
        {
            targetPosition = transform.TransformPoint(GameRuntime.Balance != null ? GameRuntime.Balance.playerCamera.supportedLocalOffset : Vector3.up * 1.55f);
            targetRotation = Quaternion.Euler(lookPitch, transform.eulerAngles.y, 0f);
            if (!ragdollBuilt || GameRuntime.Balance == null)
            {
                return false;
            }

            var camera = GameRuntime.Balance.playerCamera;
            var head = HeadTransform;
            if (head == null)
            {
                return false;
            }

            var intendedRotation = Quaternion.Euler(lookPitch, transform.eulerAngles.y, 0f);
            if (state == BodyDriveState.Limp)
            {
                targetPosition = head.TransformPoint(camera.supportedHeadLocalOffset);
                targetRotation = head.rotation;
                return true;
            }

            var fallbackPosition = transform.TransformPoint(camera.supportedLocalOffset);
            targetPosition = fallbackPosition;
            if ((head.position - transform.position).sqrMagnitude < 9f)
            {
                targetPosition.y = head.position.y + camera.supportedHeadLocalOffset.y;
            }

            targetRotation = intendedRotation;
            return true;
        }

        private PlayerPoseContext BuildPoseContext(float deltaTime, bool advanceRecovery)
        {
            if (advanceRecovery)
            {
                var recoverySeconds = Mathf.Max(0.01f, GameRuntime.Balance.playerCamera.recoveryBlendSeconds);
                recoveryTimer = Mathf.MoveTowards(recoveryTimer, 1f, deltaTime / recoverySeconds);
            }

            return new PlayerPoseContext(
                Time.time,
                cachedLocalMove,
                cachedSpeed01,
                cachedSprinting,
                cachedGrounded,
                cachedLookYaw,
                cachedLookPitch,
                recentImpact,
                recoveryTimer);
        }

        private void CacheBaseLocalRotations()
        {
            for (var i = 0; i < VisualBones.Length; i++)
            {
                var boneType = VisualBones[i];
                var bone = animator.GetBoneTransform(boneType);
                if (bone != null)
                {
                    baseLocalRotations[boneType] = bone.localRotation;
                    baseLocalPositions[boneType] = bone.localPosition;
                    baseRootLocalPositions[boneType] = transform.InverseTransformPoint(bone.position);
                }
            }

            if (baseRootLocalPositions.TryGetValue(HumanBodyBones.Hips, out var hipsLocalPosition))
            {
                hipsAnchorLocalPosition = hipsLocalPosition;
            }
            else
            {
                hipsAnchorLocalPosition = new Vector3(0f, 0.92f, 0f);
            }
        }

        private void ApplySupportedState(in PlayerPoseContext context, bool physicsPass)
        {
            var balance = GameRuntime.Balance;
            var body = balance.playerBody;
            var multiplier = state == BodyDriveState.Downed ? body.downedDriveMultiplier : body.supportedDriveMultiplier;
            var deltaTime = physicsPass ? Time.fixedDeltaTime : Time.deltaTime;
            for (var i = 0; i < VisualBones.Length; i++)
            {
                var visualBone = VisualBones[i];
                if (!baseLocalRotations.TryGetValue(visualBone, out var baseRotation))
                {
                    continue;
                }

                var bone = animator.GetBoneTransform(visualBone);
                if (bone == null)
                {
                    continue;
                }

                if (ShouldLetPhysicsDriveBone(visualBone))
                {
                    continue;
                }

                if (visualBone == HumanBodyBones.Hips)
                {
                    var hipsLocal = poseSource.GetLocalRotation(visualBone, context, baseRotation, balance);
                    bone.localRotation = Quaternion.Slerp(
                        bone.localRotation,
                        hipsLocal,
                        1f - Mathf.Exp(-body.pelvisTorque * 0.08f * multiplier * deltaTime));
                    continue;
                }

                var desiredLocal = poseSource.GetLocalRotation(visualBone, context, baseRotation, balance);
                var poseSharpness = GetPoseSharpness(visualBone, body);
                bone.localRotation = Quaternion.Slerp(
                    bone.localRotation,
                    desiredLocal,
                    1f - Mathf.Exp(-poseSharpness * multiplier * deltaTime));
            }

            if (physicsPass)
            {
                SyncKinematicBodiesToCurrentPose();
            }
        }

        private static float GetPoseSharpness(HumanBodyBones bone, PlayerBodyTuning body)
        {
            if (bone == HumanBodyBones.Head || bone == HumanBodyBones.Neck)
            {
                return body.headPoseSharpness;
            }

            if (bone == HumanBodyBones.Spine || bone == HumanBodyBones.Chest || bone == HumanBodyBones.UpperChest)
            {
                return body.spinePoseSharpness;
            }

            if (bone == HumanBodyBones.LeftUpperArm || bone == HumanBodyBones.RightUpperArm ||
                bone == HumanBodyBones.LeftLowerArm || bone == HumanBodyBones.RightLowerArm ||
                bone == HumanBodyBones.LeftHand || bone == HumanBodyBones.RightHand)
            {
                return body.armPoseSharpness;
            }

            if (bone == HumanBodyBones.LeftUpperLeg || bone == HumanBodyBones.RightUpperLeg ||
                bone == HumanBodyBones.LeftLowerLeg || bone == HumanBodyBones.RightLowerLeg ||
                bone == HumanBodyBones.LeftFoot || bone == HumanBodyBones.RightFoot)
            {
                return body.legPoseSharpness;
            }

            return Mathf.Max(body.spinePoseSharpness, body.legPoseSharpness);
        }

        private void SnapSupportedPose(in PlayerPoseContext context)
        {
            var balance = GameRuntime.Balance;
            if (balance == null)
            {
                return;
            }

            for (var i = 0; i < VisualBones.Length; i++)
            {
                var visualBone = VisualBones[i];
                if (!baseLocalRotations.TryGetValue(visualBone, out var baseRotation))
                {
                    continue;
                }

                var bone = animator.GetBoneTransform(visualBone);
                if (bone == null)
                {
                    continue;
                }

                bone.localRotation = poseSource.GetLocalRotation(visualBone, context, baseRotation, balance);
            }
        }

        private void RestoreBonePositions()
        {
            for (var i = 0; i < VisualBones.Length; i++)
            {
                var visualBone = VisualBones[i];
                if (!baseLocalPositions.TryGetValue(visualBone, out var parentLocalPosition))
                {
                    continue;
                }

                var bone = animator.GetBoneTransform(visualBone);
                if (bone == null)
                {
                    continue;
                }

                bone.localPosition = parentLocalPosition;
            }
        }

        private void SyncRigidbodiesToCurrentPose()
        {
            foreach (var pair in rigidBodies)
            {
                var body = pair.Value;
                var bone = GetBoneTransform(pair.Key);
                if (body == null || bone == null)
                {
                    continue;
                }

                body.position = bone.position;
                body.rotation = bone.rotation;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                body.Sleep();
            }

            UnityEngine.Physics.SyncTransforms();
        }

        private void SyncKinematicBodiesToCurrentPose()
        {
            foreach (var pair in rigidBodies)
            {
                var body = pair.Value;
                var bone = GetBoneTransform(pair.Key);
                if (body == null || bone == null || !body.isKinematic)
                {
                    continue;
                }

                body.position = bone.position;
                body.rotation = bone.rotation;
            }

            UnityEngine.Physics.SyncTransforms();
        }

        private void ApplySupportedDynamicLimbState(in PlayerPoseContext context)
        {
            var body = GameRuntime.Balance.playerBody;
            for (var i = 0; i < BoneConfigs.Length; i++)
            {
                var boneType = BoneConfigs[i].bone;
                if (!IsSupportedDynamicBone(boneType) || !rigidBodies.TryGetValue(boneType, out var rigidbody) || rigidbody == null || rigidbody.isKinematic)
                {
                    continue;
                }

                if (!baseLocalRotations.TryGetValue(boneType, out var baseRotation))
                {
                    continue;
                }

                var bone = animator.GetBoneTransform(boneType);
                if (bone == null)
                {
                    continue;
                }

                var desiredLocal = poseSource.GetLocalRotation(boneType, context, baseRotation, GameRuntime.Balance);
                var targetWorldRotation = bone.parent != null
                    ? bone.parent.rotation * desiredLocal
                    : transform.rotation * desiredLocal;
                var spring = IsArmRootBone(boneType) ? body.supportedUpperArmSpring : body.supportedLowerArmSpring;
                var damping = IsArmRootBone(boneType) ? body.supportedUpperArmDamping : body.supportedLowerArmDamping;
                ApplyRotationDrive(rigidbody, targetWorldRotation, spring, damping);
            }
        }

        private static void ApplyRotationDrive(Rigidbody rigidbody, Quaternion targetRotation, float spring, float damping)
        {
            var delta = targetRotation * Quaternion.Inverse(rigidbody.rotation);
            delta.ToAngleAxis(out var angle, out var axis);
            if (float.IsNaN(axis.x) || axis == Vector3.zero)
            {
                return;
            }

            if (angle > 180f)
            {
                angle -= 360f;
            }

            var torque = axis.normalized * (angle * Mathf.Deg2Rad * spring) - rigidbody.angularVelocity * damping;
            rigidbody.AddTorque(torque, ForceMode.Acceleration);
        }

        private void ApplyLimpState()
        {
            var ragdoll = GameRuntime.Balance != null ? GameRuntime.Balance.ragdoll : null;
            foreach (var body in rigidBodies.Values)
            {
                if (body == null)
                {
                    continue;
                }

                body.linearDamping = 0.08f;
                body.angularDamping = 0.04f;

                var bone = GetBoneForRigidbody(body);
                if (ragdoll == null || bone == null)
                {
                    continue;
                }

                if (bone == HumanBodyBones.Head)
                {
                    body.angularDamping = Mathf.Max(body.angularDamping, ragdoll.limpHeadAngularDamping);
                    body.angularVelocity = Vector3.ClampMagnitude(body.angularVelocity * 0.9f, ragdoll.limpHeadMaxAngularSpeed);
                    continue;
                }

                if (bone == HumanBodyBones.Neck)
                {
                    body.angularDamping = Mathf.Max(body.angularDamping, ragdoll.limpNeckAngularDamping);
                    body.angularVelocity = Vector3.ClampMagnitude(body.angularVelocity * 0.92f, ragdoll.limpNeckMaxAngularSpeed);
                }
            }
        }

        private void ConfigureBodiesForState()
        {
            if (!ragdollBuilt)
            {
                return;
            }

            var enableAllPhysics = state == BodyDriveState.Limp;
            foreach (var pair in rigidBodies)
            {
                var body = pair.Value;
                if (body == null)
                {
                    continue;
                }

                var enablePhysics = enableAllPhysics || ShouldKeepBoneDynamicInSupportedState(pair.Key);
                var keepCollisionShape = enableAllPhysics;
                if (!enablePhysics)
                {
                    if (!body.isKinematic)
                    {
                        body.linearVelocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }
                }

                body.isKinematic = !enablePhysics;
                body.useGravity = enablePhysics;
                body.detectCollisions = keepCollisionShape;

                if (enablePhysics)
                {
                    body.linearDamping = 0.08f;
                    body.angularDamping = 0.04f;
                    body.WakeUp();
                }
                else if (state == BodyDriveState.Downed)
                {
                    body.linearDamping = GameRuntime.Balance.playerBody.downedLinearDamping;
                    body.angularDamping = GameRuntime.Balance.playerBody.downedAngularDamping;
                }
                else
                {
                    body.linearDamping = GameRuntime.Balance.playerBody.supportedLinearDamping;
                    body.angularDamping = GameRuntime.Balance.playerBody.supportedAngularDamping;
                }
            }

            for (var i = 0; i < ragdollColliders.Count; i++)
            {
                if (ragdollColliders[i] != null)
                {
                    ragdollColliders[i].enabled = enableAllPhysics;
                }
            }

            if (animator != null)
            {
                animator.enabled = false;
            }
        }

        private bool ShouldLetPhysicsDriveBone(HumanBodyBones bone)
        {
            return state != BodyDriveState.Limp &&
                   ShouldKeepBoneDynamicInSupportedState(bone) &&
                   rigidBodies.TryGetValue(bone, out var body) &&
                   body != null &&
                   !body.isKinematic;
        }

        private static bool ShouldKeepBoneDynamicInSupportedState(HumanBodyBones bone)
        {
            return IsSupportedDynamicBone(bone);
        }

        private HumanBodyBones? GetBoneForRigidbody(Rigidbody rigidbody)
        {
            if (rigidbody == null)
            {
                return null;
            }

            foreach (var pair in rigidBodies)
            {
                if (pair.Value == rigidbody)
                {
                    return pair.Key;
                }
            }

            return null;
        }

        private static bool IsArmRootBone(HumanBodyBones bone)
        {
            return bone == HumanBodyBones.LeftShoulder ||
                   bone == HumanBodyBones.RightShoulder ||
                   bone == HumanBodyBones.LeftUpperArm ||
                   bone == HumanBodyBones.RightUpperArm;
        }

        private static bool IsSupportedDynamicBone(HumanBodyBones bone)
        {
            return bone == HumanBodyBones.LeftUpperArm ||
                   bone == HumanBodyBones.LeftLowerArm ||
                   bone == HumanBodyBones.LeftHand ||
                   bone == HumanBodyBones.RightUpperArm ||
                   bone == HumanBodyBones.RightLowerArm ||
                   bone == HumanBodyBones.RightHand;
        }

        private static bool IsSupportedCollisionBone(HumanBodyBones bone)
        {
            return IsSupportedDynamicBone(bone) ||
                   bone == HumanBodyBones.Hips ||
                   bone == HumanBodyBones.Spine ||
                   bone == HumanBodyBones.Chest ||
                   bone == HumanBodyBones.Head ||
                   bone == HumanBodyBones.LeftUpperLeg ||
                   bone == HumanBodyBones.RightUpperLeg;
        }

        private bool BuildRagdoll()
        {
            var balance = GameRuntime.Balance;
            if (balance != null)
            {
                jointSwingLimit = balance.ragdoll.jointSwingLimit;
                jointTwistLimit = balance.ragdoll.jointTwistLimit;
            }

            for (var i = 0; i < BoneConfigs.Length; i++)
            {
                if (!CreateBoneBody(BoneConfigs[i]))
                {
                    return false;
                }
            }

            for (var i = 0; i < BoneConfigs.Length; i++)
            {
                if (BoneConfigs[i].parentBone == HumanBodyBones.LastBone)
                {
                    continue;
                }

                ConnectJoint(BoneConfigs[i]);
            }

            return rigidBodies.Count > 0;
        }

        private bool CreateBoneBody(BoneConfig config)
        {
            var bone = animator.GetBoneTransform(config.bone);
            if (bone == null)
            {
                return true;
            }

            var body = EnsureRigidbody(bone, config.bone);
            if (body == null)
            {
                return false;
            }

            body.mass = config.mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.solverIterations = 12;
            body.solverVelocityIterations = 4;
            body.maxAngularVelocity = 80f;

            CreateCollider(bone, config);
            rigidBodies[config.bone] = body;
            return true;
        }

        private void ConnectJoint(BoneConfig config)
        {
            var bone = animator.GetBoneTransform(config.bone);
            var parent = animator.GetBoneTransform(config.parentBone);
            if (bone == null || parent == null)
            {
                return;
            }

            var connectedBody = parent.GetComponent<Rigidbody>();
            if (connectedBody == null)
            {
                return;
            }

            if (!bone.TryGetComponent<CharacterJoint>(out var joint) || joint == null)
            {
                joint = bone.gameObject.AddComponent<CharacterJoint>();
            }

            joint.connectedBody = connectedBody;
            joint.autoConfigureConnectedAnchor = true;
            joint.enablePreprocessing = false;
            joint.enableProjection = true;
            joint.swingAxis = Vector3.forward;
            joint.lowTwistLimit = BuildSoftLimit(-jointTwistLimit);
            joint.highTwistLimit = BuildSoftLimit(jointTwistLimit);
            joint.swing1Limit = BuildSoftLimit(jointSwingLimit);
            joint.swing2Limit = BuildSoftLimit(jointSwingLimit);
            if (!ragdollJoints.Contains(joint))
            {
                ragdollJoints.Add(joint);
            }
        }

        private void CreateCollider(Transform bone, BoneConfig config)
        {
            var child = GetReferenceChild(config);
            switch (config.colliderType)
            {
                case PrimitiveType.Sphere:
                {
                    if (!bone.TryGetComponent<SphereCollider>(out var collider) || collider == null)
                    {
                        collider = bone.gameObject.AddComponent<SphereCollider>();
                    }

                    collider.center = config.colliderCenter;
                    collider.radius = Mathf.Max(config.colliderSize.x, config.colliderSize.y, config.colliderSize.z) * 0.5f;
                    RegisterRagdollCollider(collider);
                    break;
                }
                case PrimitiveType.Capsule:
                {
                    if (!bone.TryGetComponent<CapsuleCollider>(out var collider) || collider == null)
                    {
                        collider = bone.gameObject.AddComponent<CapsuleCollider>();
                    }

                    FitCapsuleToChild(bone, child, config, collider);
                    RegisterRagdollCollider(collider);
                    break;
                }
                default:
                {
                    if (!bone.TryGetComponent<BoxCollider>(out var collider) || collider == null)
                    {
                        collider = bone.gameObject.AddComponent<BoxCollider>();
                    }

                    FitBoxToChild(bone, child, config, collider);
                    RegisterRagdollCollider(collider);
                    break;
                }
            }
        }

        private void RegisterRagdollCollider(Collider collider)
        {
            if (collider != null && !ragdollColliders.Contains(collider))
            {
                ragdollColliders.Add(collider);
            }
        }

        private void IgnoreRootCollisions()
        {
            if (rootColliders == null || rootColliders.Length == 0)
            {
                return;
            }

            for (var i = 0; i < ragdollColliders.Count; i++)
            {
                var ragdollCollider = ragdollColliders[i];
                if (ragdollCollider == null)
                {
                    continue;
                }

                for (var rootIndex = 0; rootIndex < rootColliders.Length; rootIndex++)
                {
                    var rootCollider = rootColliders[rootIndex];
                    if (rootCollider != null && rootCollider != ragdollCollider)
                    {
                        UnityEngine.Physics.IgnoreCollision(rootCollider, ragdollCollider, true);
                    }
                }
            }
        }

        private void IgnoreRagdollSelfCollisions()
        {
            for (var i = 0; i < ragdollColliders.Count; i++)
            {
                var first = ragdollColliders[i];
                if (first == null)
                {
                    continue;
                }

                for (var j = i + 1; j < ragdollColliders.Count; j++)
                {
                    var second = ragdollColliders[j];
                    if (second != null && second != first)
                    {
                        UnityEngine.Physics.IgnoreCollision(first, second, true);
                    }
                }
            }
        }

        private Transform GetReferenceChild(BoneConfig config)
        {
            if (config.childHint != HumanBodyBones.LastBone)
            {
                var hinted = animator.GetBoneTransform(config.childHint);
                if (hinted != null)
                {
                    return hinted;
                }
            }

            var bone = animator.GetBoneTransform(config.bone);
            return bone != null && bone.childCount > 0 ? bone.GetChild(0) : null;
        }

        private static void FitCapsuleToChild(Transform bone, Transform child, BoneConfig config, CapsuleCollider collider)
        {
            if (child == null)
            {
                collider.center = config.colliderCenter;
                collider.radius = Mathf.Min(config.colliderSize.x, config.colliderSize.z) * 0.5f;
                collider.height = config.colliderSize.y;
                collider.direction = 1;
                return;
            }

            var localChild = bone.InverseTransformPoint(child.position);
            var axis = DominantAxis(localChild);
            var distance = Mathf.Max(localChild.magnitude, 0.05f);
            var radius = Mathf.Max(Mathf.Min(config.colliderSize.x, config.colliderSize.z) * 0.5f, distance * 0.12f);
            collider.direction = axis;
            collider.center = localChild * 0.5f;
            collider.radius = radius;
            collider.height = Mathf.Max(distance + radius * 2f, radius * 2.5f);
        }

        private static void FitBoxToChild(Transform bone, Transform child, BoneConfig config, BoxCollider collider)
        {
            if (child == null)
            {
                collider.center = config.colliderCenter;
                collider.size = config.colliderSize;
                return;
            }

            var localChild = bone.InverseTransformPoint(child.position);
            var axis = DominantAxis(localChild);
            var distance = Mathf.Max(localChild.magnitude, 0.04f);
            var size = config.colliderSize;
            size[axis] = Mathf.Max(distance, size[axis]);
            collider.center = localChild * 0.5f;
            collider.size = size;
        }

        private static int DominantAxis(Vector3 vector)
        {
            var absolute = new Vector3(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));
            if (absolute.x > absolute.y && absolute.x > absolute.z)
            {
                return 0;
            }

            return absolute.z > absolute.y ? 2 : 1;
        }

        private static SoftJointLimit BuildSoftLimit(float limit)
        {
            return new SoftJointLimit
            {
                limit = limit,
                bounciness = 0f,
                contactDistance = 0f
            };
        }

        private static Rigidbody EnsureRigidbody(Transform bone, HumanBodyBones boneName)
        {
            if (bone == null)
            {
                return null;
            }

            if (bone.TryGetComponent<Rigidbody>(out var existingBody) && existingBody != null)
            {
                return existingBody;
            }

            try
            {
                var createdBody = bone.gameObject.AddComponent<Rigidbody>();
                if (createdBody == null)
                {
                    Debug.LogError($"Failed to create a Rigidbody for active ragdoll bone '{boneName}' on '{bone.name}'.", bone);
                    return null;
                }

                return createdBody;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Exception while creating a Rigidbody for active ragdoll bone '{boneName}' on '{bone.name}': {exception.Message}", bone);
                return null;
            }
        }
    }
}

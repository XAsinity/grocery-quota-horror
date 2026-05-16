using System;
using System.Collections.Generic;
using GroceryQuotaHorror.Data;
using UnityEngine;

namespace GroceryQuotaHorror.Player
{
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
            new() { bone = HumanBodyBones.Hips, parentBone = HumanBodyBones.LastBone, colliderType = PrimitiveType.Cube, colliderCenter = new Vector3(0f, 0.04f, 0f), colliderSize = new Vector3(0.24f, 0.18f, 0.18f), mass = 2.6f, childHint = HumanBodyBones.Spine },
            new() { bone = HumanBodyBones.Spine, parentBone = HumanBodyBones.Hips, colliderType = PrimitiveType.Cube, colliderCenter = new Vector3(0f, 0.09f, 0f), colliderSize = new Vector3(0.24f, 0.22f, 0.18f), mass = 2f, childHint = HumanBodyBones.Chest },
            new() { bone = HumanBodyBones.Chest, parentBone = HumanBodyBones.Spine, colliderType = PrimitiveType.Cube, colliderCenter = new Vector3(0f, 0.1f, 0f), colliderSize = new Vector3(0.28f, 0.24f, 0.2f), mass = 2.2f, childHint = HumanBodyBones.Head },
            new() { bone = HumanBodyBones.Head, parentBone = HumanBodyBones.Chest, colliderType = PrimitiveType.Sphere, colliderCenter = new Vector3(0f, 0.08f, 0f), colliderSize = new Vector3(0.18f, 0.18f, 0.18f), mass = 1f },
            new() { bone = HumanBodyBones.LeftUpperArm, parentBone = HumanBodyBones.Chest, colliderType = PrimitiveType.Capsule, colliderCenter = new Vector3(-0.12f, 0f, 0f), colliderSize = new Vector3(0.09f, 0.28f, 0.09f), mass = 0.7f, childHint = HumanBodyBones.LeftLowerArm },
            new() { bone = HumanBodyBones.LeftLowerArm, parentBone = HumanBodyBones.LeftUpperArm, colliderType = PrimitiveType.Capsule, colliderCenter = new Vector3(-0.14f, 0f, 0f), colliderSize = new Vector3(0.08f, 0.26f, 0.08f), mass = 0.6f, childHint = HumanBodyBones.LeftHand },
            new() { bone = HumanBodyBones.RightUpperArm, parentBone = HumanBodyBones.Chest, colliderType = PrimitiveType.Capsule, colliderCenter = new Vector3(0.12f, 0f, 0f), colliderSize = new Vector3(0.09f, 0.28f, 0.09f), mass = 0.7f, childHint = HumanBodyBones.RightLowerArm },
            new() { bone = HumanBodyBones.RightLowerArm, parentBone = HumanBodyBones.RightUpperArm, colliderType = PrimitiveType.Capsule, colliderCenter = new Vector3(0.14f, 0f, 0f), colliderSize = new Vector3(0.08f, 0.26f, 0.08f), mass = 0.6f, childHint = HumanBodyBones.RightHand },
            new() { bone = HumanBodyBones.LeftUpperLeg, parentBone = HumanBodyBones.Hips, colliderType = PrimitiveType.Capsule, colliderCenter = new Vector3(0f, -0.2f, 0f), colliderSize = new Vector3(0.1f, 0.42f, 0.1f), mass = 1.2f, childHint = HumanBodyBones.LeftLowerLeg },
            new() { bone = HumanBodyBones.LeftLowerLeg, parentBone = HumanBodyBones.LeftUpperLeg, colliderType = PrimitiveType.Capsule, colliderCenter = new Vector3(0f, -0.18f, 0f), colliderSize = new Vector3(0.08f, 0.38f, 0.08f), mass = 1f, childHint = HumanBodyBones.LeftFoot },
            new() { bone = HumanBodyBones.RightUpperLeg, parentBone = HumanBodyBones.Hips, colliderType = PrimitiveType.Capsule, colliderCenter = new Vector3(0f, -0.2f, 0f), colliderSize = new Vector3(0.1f, 0.42f, 0.1f), mass = 1.2f, childHint = HumanBodyBones.RightLowerLeg },
            new() { bone = HumanBodyBones.RightLowerLeg, parentBone = HumanBodyBones.RightUpperLeg, colliderType = PrimitiveType.Capsule, colliderCenter = new Vector3(0f, -0.18f, 0f), colliderSize = new Vector3(0.08f, 0.38f, 0.08f), mass = 1f, childHint = HumanBodyBones.RightFoot },
            new() { bone = HumanBodyBones.LeftFoot, parentBone = HumanBodyBones.LeftLowerLeg, colliderType = PrimitiveType.Cube, colliderCenter = new Vector3(0f, 0f, 0.08f), colliderSize = new Vector3(0.09f, 0.08f, 0.22f), mass = 0.4f, childHint = HumanBodyBones.LeftToes },
            new() { bone = HumanBodyBones.RightFoot, parentBone = HumanBodyBones.RightLowerLeg, colliderType = PrimitiveType.Cube, colliderCenter = new Vector3(0f, 0f, 0.08f), colliderSize = new Vector3(0.09f, 0.08f, 0.22f), mass = 0.4f, childHint = HumanBodyBones.RightToes }
        };

        private readonly Dictionary<HumanBodyBones, Rigidbody> rigidBodies = new();
        private readonly Dictionary<HumanBodyBones, Quaternion> baseLocalRotations = new();
        private readonly Dictionary<HumanBodyBones, Vector3> baseLocalPositions = new();
        private readonly List<Collider> ragdollColliders = new();
        private readonly ProceduralPoseSource proceduralPoseSource = new();

        private Animator animator;
        private CharacterController rootController;
        private IPlayerPoseSource poseSource;
        private BodyDriveState state = BodyDriveState.Supported;
        private bool ragdollBuilt;
        private Vector3 lastRootPosition;
        private float recentImpact;
        private float recoveryTimer;
        private Vector3 cachedLocalMove;
        private float cachedSpeed01;
        private bool cachedSprinting;
        private bool cachedGrounded;
        private float cachedLookPitch;
        private Vector3 hipsAnchorLocalPosition;

        public BodyDriveState State => state;
        public bool IsAvailable => ragdollBuilt;
        public Transform ModelRoot => modelRoot;
        public Transform HeadTransform => GetBoneTransform(HumanBodyBones.Head);

        private void Awake()
        {
            if (modelRoot == null)
            {
                modelRoot = transform.Find("VisualRoot");
            }

            if (modelRoot == null && transform.childCount > 0)
            {
                modelRoot = transform.GetChild(0);
            }

            rootController = GetComponent<CharacterController>();
            animator = modelRoot != null ? modelRoot.GetComponentInChildren<Animator>() : null;
            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning($"{name} is missing a humanoid animator for active ragdoll setup.", this);
                return;
            }

            poseSource = proceduralPoseSource;
            CacheBaseLocalRotations();
            ragdollBuilt = BuildRagdoll();
            if (!ragdollBuilt)
            {
                Debug.LogWarning($"{name} could not finish active ragdoll setup. The player will stay in supported character mode until the rig setup succeeds.", this);
                return;
            }

            IgnoreRootCollisions();
            SetState(BodyDriveState.Supported, true);
            lastRootPosition = transform.position;
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
            }
        }

        private void LateUpdate()
        {
            if (!ragdollBuilt || animator == null || GameRuntime.Balance == null || state == BodyDriveState.Limp)
            {
                return;
            }

            recoveryTimer = Mathf.MoveTowards(recoveryTimer, 1f, Time.deltaTime * 1.8f);
            var context = new PlayerPoseContext(
                Time.time,
                cachedLocalMove,
                cachedSpeed01,
                cachedSprinting,
                cachedGrounded,
                cachedLookPitch,
                recentImpact,
                recoveryTimer);

            ApplySupportedState(context);
        }

        public void SetState(BodyDriveState nextState, bool instant = false)
        {
            if (!ragdollBuilt)
            {
                return;
            }

            state = nextState;
            recoveryTimer = instant || nextState == BodyDriveState.Supported ? 1f : 0f;
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

        public void UpdateSupportedMotion(Vector3 localMove, float speed01, bool sprinting, bool grounded, float lookPitch)
        {
            cachedLocalMove = localMove;
            cachedSpeed01 = speed01;
            cachedSprinting = sprinting;
            cachedGrounded = grounded;
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
            var torque = (transform.up * mouseX + transform.right * -mouseY) * ragdoll.headLookTorque;
            headBody.AddTorque(torque, ForceMode.Acceleration);
            headBody.AddForce(Vector3.up * (Mathf.Abs(mouseY) * ragdoll.headLiftForce), ForceMode.Acceleration);
        }

        public Rigidbody GetBoneRigidbody(HumanBodyBones bone)
        {
            return rigidBodies.TryGetValue(bone, out var body) ? body : null;
        }

        public Transform GetBoneTransform(HumanBodyBones bone)
        {
            return animator != null ? animator.GetBoneTransform(bone) : null;
        }

        private void CacheBaseLocalRotations()
        {
            for (var i = 0; i < BoneConfigs.Length; i++)
            {
                var bone = animator.GetBoneTransform(BoneConfigs[i].bone);
                if (bone != null)
                {
                    baseLocalRotations[BoneConfigs[i].bone] = bone.localRotation;
                    baseLocalPositions[BoneConfigs[i].bone] = transform.InverseTransformPoint(bone.position);
                }
            }

            if (baseLocalPositions.TryGetValue(HumanBodyBones.Hips, out var hipsLocalPosition))
            {
                hipsAnchorLocalPosition = hipsLocalPosition;
            }
            else
            {
                hipsAnchorLocalPosition = new Vector3(0f, 0.92f, 0f);
            }
        }

        private void ApplySupportedState(in PlayerPoseContext context)
        {
            var balance = GameRuntime.Balance;
            var body = balance.playerBody;
            var multiplier = state == BodyDriveState.Downed ? body.downedDriveMultiplier : body.supportedDriveMultiplier;
            var poseSharpness = state == BodyDriveState.Downed ? 10f : 18f;
            var hips = GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null)
            {
                var targetLocalPosition = hipsAnchorLocalPosition + poseSource.GetPelvisOffset(context, balance);
                var currentLocalPosition = transform.InverseTransformPoint(hips.position);
                var nextLocalPosition = Vector3.Lerp(
                    currentLocalPosition,
                    targetLocalPosition,
                    1f - Mathf.Exp(-body.pelvisFollowForce * 0.02f * Time.deltaTime));
                hips.position = transform.TransformPoint(nextLocalPosition);
            }

            for (var i = 0; i < BoneConfigs.Length; i++)
            {
                if (!baseLocalRotations.TryGetValue(BoneConfigs[i].bone, out var baseRotation))
                {
                    continue;
                }

                var bone = animator.GetBoneTransform(BoneConfigs[i].bone);
                if (bone == null)
                {
                    continue;
                }

                if (BoneConfigs[i].bone == HumanBodyBones.Hips)
                {
                    var hipsLocal = poseSource.GetLocalRotation(BoneConfigs[i].bone, context, baseRotation, balance);
                    bone.localRotation = Quaternion.Slerp(
                        bone.localRotation,
                        hipsLocal,
                        1f - Mathf.Exp(-body.pelvisTorque * 0.04f * multiplier * Time.deltaTime));
                    continue;
                }

                var desiredLocal = poseSource.GetLocalRotation(BoneConfigs[i].bone, context, baseRotation, balance);
                bone.localRotation = Quaternion.Slerp(
                    bone.localRotation,
                    desiredLocal,
                    1f - Mathf.Exp(-poseSharpness * multiplier * Time.deltaTime));
            }
        }

        private void ApplyLimpState()
        {
            foreach (var body in rigidBodies.Values)
            {
                if (body == null)
                {
                    continue;
                }

                body.linearDamping = 0.08f;
                body.angularDamping = 0.04f;
            }
        }

        private void ConfigureBodiesForState()
        {
            if (!ragdollBuilt)
            {
                return;
            }

            foreach (var body in rigidBodies.Values)
            {
                if (body == null)
                {
                    continue;
                }

                body.isKinematic = state != BodyDriveState.Limp;
                body.useGravity = state == BodyDriveState.Limp;
                body.detectCollisions = true;
                if (state == BodyDriveState.Limp)
                {
                    body.linearDamping = 0.08f;
                    body.angularDamping = 0.04f;
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

            if (animator != null)
            {
                animator.enabled = false;
            }
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
            joint.enableProjection = true;
            joint.swingAxis = Vector3.forward;
            joint.lowTwistLimit = BuildSoftLimit(-jointTwistLimit);
            joint.highTwistLimit = BuildSoftLimit(jointTwistLimit);
            joint.swing1Limit = BuildSoftLimit(jointSwingLimit);
            joint.swing2Limit = BuildSoftLimit(jointSwingLimit);
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
            if (rootController == null)
            {
                return;
            }

            for (var i = 0; i < ragdollColliders.Count; i++)
            {
                if (ragdollColliders[i] != null)
                {
                    UnityEngine.Physics.IgnoreCollision(rootController, ragdollColliders[i], true);
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

using System;
using System.Collections.Generic;
using GroceryQuotaHorror.Data;
using UnityEngine;

namespace GroceryQuotaHorror.Physics
{
    public sealed class SpawnableRagdoll : MonoBehaviour
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
        [SerializeField] private float spawnHeightOffset = 0.95f;
        [SerializeField] private float jointSwingLimit = 25f;
        [SerializeField] private float jointTwistLimit = 20f;
        [SerializeField] private bool startAsRagdoll = true;

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
        private readonly List<Collider> ragdollColliders = new();
        private Animator animator;
        private bool isRagdollActive;

        public bool IsRagdollActive => isRagdollActive;

        private void Awake()
        {
            if (modelRoot == null && transform.childCount > 0)
            {
                modelRoot = transform.GetChild(0);
            }

            animator = modelRoot != null ? modelRoot.GetComponentInChildren<Animator>() : null;
            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning($"{name} could not build a ragdoll because the model is missing a humanoid Animator.", this);
                return;
            }

            if (startAsRagdoll)
            {
                transform.position += Vector3.up * spawnHeightOffset;
            }

            var balance = GameRuntime.Balance;
            if (balance != null)
            {
                jointSwingLimit = balance.ragdoll.jointSwingLimit;
                jointTwistLimit = balance.ragdoll.jointTwistLimit;
            }

            BuildRagdoll();
            SetRagdollActive(startAsRagdoll);
        }

        public void SetGrabbed(bool grabbed)
        {
            var tuning = GameRuntime.Balance;
            foreach (var body in rigidBodies.Values)
            {
                if (body == null)
                {
                    continue;
                }

                body.useGravity = true;
                body.linearDamping = grabbed
                    ? (tuning != null ? tuning.ragdoll.ragdollHeldLinearDamping : 1.1f)
                    : 0.2f;
                body.angularDamping = grabbed
                    ? (tuning != null ? tuning.ragdoll.ragdollHeldAngularDamping : 0.45f)
                    : 0.05f;
            }
        }

        public void SetRagdollActive(bool active)
        {
            isRagdollActive = active;
            if (animator != null)
            {
                animator.enabled = !active;
            }

            foreach (var collider in ragdollColliders)
            {
                if (collider == null)
                {
                    continue;
                }

                collider.enabled = active;
            }

            foreach (var body in rigidBodies.Values)
            {
                if (body == null)
                {
                    continue;
                }

                body.isKinematic = !active;
                body.useGravity = active;
                body.detectCollisions = active;
                if (!active)
                {
                    if (!body.isKinematic)
                    {
                        body.linearVelocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }
                }
            }
        }

        public Transform GetBoneTransform(HumanBodyBones bone)
        {
            return animator != null ? animator.GetBoneTransform(bone) : null;
        }

        public Rigidbody GetBoneRigidbody(HumanBodyBones bone)
        {
            return rigidBodies.TryGetValue(bone, out var body) ? body : null;
        }

        public bool TryGetClosestRigidbody(Vector3 worldPoint, out Rigidbody closestBody)
        {
            closestBody = null;
            var closestDistance = float.MaxValue;
            foreach (var body in rigidBodies.Values)
            {
                if (body == null || body.isKinematic)
                {
                    continue;
                }

                var distance = (body.worldCenterOfMass - worldPoint).sqrMagnitude;
                if (distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = distance;
                closestBody = body;
            }

            if (closestBody != null)
            {
                return true;
            }

            foreach (var body in rigidBodies.Values)
            {
                if (body == null)
                {
                    continue;
                }

                var distance = (body.worldCenterOfMass - worldPoint).sqrMagnitude;
                if (distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = distance;
                closestBody = body;
            }

            return closestBody != null;
        }

        public void ApplyWholeBodyVelocity(Vector3 linearVelocity, Vector3 angularVelocity, Vector3 impulse)
        {
            foreach (var body in rigidBodies.Values)
            {
                if (body == null || body.isKinematic)
                {
                    continue;
                }

                body.linearVelocity = linearVelocity;
                body.angularVelocity = angularVelocity;
                body.AddForce(impulse, ForceMode.Impulse);
            }
        }

        private void BuildRagdoll()
        {
            foreach (var config in BoneConfigs)
            {
                try
                {
                    CreateBoneBody(config);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to build ragdoll body for bone {config.bone}: {ex.Message}", this);
                }
            }

            foreach (var config in BoneConfigs)
            {
                if (config.parentBone == HumanBodyBones.LastBone)
                {
                    continue;
                }

                ConnectJoint(config);
            }
        }

        private void CreateBoneBody(BoneConfig config)
        {
            var bone = animator.GetBoneTransform(config.bone);
            if (bone == null)
            {
                return;
            }

            RemovePrimitiveVisuals(bone);

            var body = bone.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = bone.gameObject.AddComponent<Rigidbody>();
            }

            body.mass = config.mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = 0.2f;
            body.angularDamping = 0.05f;

            CreateCollider(bone, config);
            rigidBodies[config.bone] = body;
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

            var joint = bone.GetComponent<CharacterJoint>();
            if (joint == null)
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
                    var collider = bone.GetComponent<SphereCollider>();
                    if (collider == null)
                    {
                        collider = bone.gameObject.AddComponent<SphereCollider>();
                    }

                    if (collider == null)
                    {
                        return;
                    }

                    collider.center = config.colliderCenter;
                    collider.radius = Mathf.Max(config.colliderSize.x, config.colliderSize.y, config.colliderSize.z) * 0.5f;
                    RegisterRagdollCollider(collider);
                    break;
                }
                case PrimitiveType.Capsule:
                {
                    var collider = bone.GetComponent<CapsuleCollider>();
                    if (collider == null)
                    {
                        collider = bone.gameObject.AddComponent<CapsuleCollider>();
                    }

                    if (collider == null)
                    {
                        return;
                    }

                    FitCapsuleToChild(bone, child, config, collider);
                    RegisterRagdollCollider(collider);
                    break;
                }
                default:
                {
                    var collider = bone.GetComponent<BoxCollider>();
                    if (collider == null)
                    {
                        collider = bone.gameObject.AddComponent<BoxCollider>();
                    }

                    if (collider == null)
                    {
                        return;
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

            return animator.GetBoneTransform(config.bone)?.childCount > 0
                ? animator.GetBoneTransform(config.bone).GetChild(0)
                : null;
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

        private static void RemovePrimitiveVisuals(Transform bone)
        {
            var renderer = bone.GetComponent<Renderer>();
            var filter = bone.GetComponent<MeshFilter>();
            if (renderer != null && filter != null)
            {
                renderer.enabled = false;
            }
        }
    }
}

using System.Collections.Generic;
using GroceryQuotaHorror.Data;
using UnityEngine;

namespace GroceryQuotaHorror.Player
{
    public sealed class PlayerLooseBodyController : MonoBehaviour
    {
        private enum LooseBodyMode
        {
            Supported,
            Limp,
            Recovering
        }

        private static readonly HumanBodyBones[] DrivenBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.UpperChest,
            HumanBodyBones.Neck,
            HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot
        };

        [SerializeField] private Transform modelRoot;

        private readonly Dictionary<HumanBodyBones, Quaternion> baseRotations = new();
        private readonly Dictionary<HumanBodyBones, Vector3> basePositions = new();
        private readonly Dictionary<HumanBodyBones, Quaternion> postRagdollStartRotations = new();
        private readonly Dictionary<HumanBodyBones, Vector3> postRagdollStartPositions = new();
        private readonly Dictionary<HumanBodyBones, Transform> bones = new();
        private Animator animator;
        private Transform cameraSocket;
        private Vector3 lastRootPosition;
        private Vector3 lastLocalVelocity;
        private Vector3 leftUpperArmDirection = Vector3.down;
        private Vector3 rightUpperArmDirection = Vector3.down;
        private Vector3 leftLowerArmDirection = Vector3.down;
        private Vector3 rightLowerArmDirection = Vector3.down;
        private Vector3 leftUpperArmVelocity;
        private Vector3 rightUpperArmVelocity;
        private Vector3 leftLowerArmVelocity;
        private Vector3 rightLowerArmVelocity;
        private Vector3 baseModelLocalPosition;
        private float strideClock;
        private bool relaxedMode;
        private LooseBodyMode mode;
        private float recoveryStartedAt;
        private float postRagdollBlendStartedAt;
        private float visualCollapse;
        private float jumpPulse;
        private float landingPulse;
        private const float RecoverySeconds = 1.25f;
        private const float PostRagdollBlendSeconds = 0.85f;

        public bool IsAvailable => animator != null && animator.isHuman;
        public bool BlocksHorizontalMovement => mode == LooseBodyMode.Limp || mode == LooseBodyMode.Recovering;

        public void Initialize(Transform explicitModelRoot = null)
        {
            if (explicitModelRoot != null)
            {
                modelRoot = explicitModelRoot;
            }

            if (modelRoot == null)
            {
                modelRoot = transform.Find("VisualRoot");
            }

            if (modelRoot == null && transform.childCount > 0)
            {
                modelRoot = transform.GetChild(0);
            }

            animator = modelRoot != null ? modelRoot.GetComponentInChildren<Animator>(true) : null;
            bones.Clear();

            if (animator == null || !animator.isHuman)
            {
                return;
            }

            animator.applyRootMotion = false;
            animator.enabled = false;

            for (var i = 0; i < DrivenBones.Length; i++)
            {
                var boneType = DrivenBones[i];
                var bone = animator.GetBoneTransform(boneType);
                if (bone == null)
                {
                    continue;
                }

                bones[boneType] = bone;
                if (!baseRotations.ContainsKey(boneType))
                {
                    baseRotations[boneType] = bone.localRotation;
                }

                if (!basePositions.ContainsKey(boneType))
                {
                    basePositions[boneType] = bone.localPosition;
                }
            }

            lastRootPosition = transform.position;
            lastLocalVelocity = Vector3.zero;
            ResetArmSimulation();
            baseModelLocalPosition = modelRoot.localPosition;
            EnsureCameraSocket();
        }

        public void RestoreReferenceBonePositions()
        {
            foreach (var pair in basePositions)
            {
                if (!bones.TryGetValue(pair.Key, out var bone) || bone == null)
                {
                    continue;
                }

                bone.localPosition = pair.Value;
            }
        }

        public void CapturePostRagdollBlendStart()
        {
            postRagdollStartRotations.Clear();
            postRagdollStartPositions.Clear();

            foreach (var pair in bones)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                postRagdollStartRotations[pair.Key] = pair.Value.localRotation;
                postRagdollStartPositions[pair.Key] = pair.Value.localPosition;
            }

            postRagdollBlendStartedAt = Time.time;
        }

        public void ResetAfterRagdoll(CapsuleCollider rootCollider, bool snapUpright = false)
        {
            var visualRootBefore = modelRoot != null ? modelRoot.position : Vector3.zero;
            mode = snapUpright ? LooseBodyMode.Supported : LooseBodyMode.Recovering;
            recoveryStartedAt = Time.time;
            visualCollapse = 0f;
            jumpPulse = 0f;
            landingPulse = 0f;
            relaxedMode = true;
            ResetArmSimulation();
            if (snapUpright)
            {
                RestoreReferenceBonePositions();
                postRagdollStartRotations.Clear();
                postRagdollStartPositions.Clear();
            }

            ApplyModelCollapseOffset();

            lastRootPosition = transform.position;
            if (modelRoot != null)
            {
                var visualShift = modelRoot.position - visualRootBefore;
                Debug.Log($"[Recovery] Loose body reset root={transform.position} visualBefore={visualRootBefore} visualAfter={modelRoot.position} visualShift={visualShift}", this);
            }
        }

        public void NotifyJump()
        {
            jumpPulse = 1f;
            landingPulse = 0f;
        }

        public void NotifyLanding(float fallSpeed)
        {
            landingPulse = Mathf.Clamp01(fallSpeed / 8f);
            jumpPulse = 0f;
        }

        public bool AttachCamera(Camera targetCamera, float pitch, GameBalanceProfile balance)
        {
            if (targetCamera == null || balance == null || !IsAvailable)
            {
                return false;
            }

            EnsureCameraSocket();
            if (cameraSocket == null)
            {
                return false;
            }

            cameraSocket.localPosition = balance.playerCamera.supportedHeadLocalOffset;
            cameraSocket.localRotation = Quaternion.identity;

            if (targetCamera.transform.parent != cameraSocket)
            {
                targetCamera.transform.SetParent(cameraSocket, false);
            }

            targetCamera.transform.localPosition = Vector3.zero;
            targetCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            return true;
        }

        public void ToggleLimpMode()
        {
            if (mode == LooseBodyMode.Limp)
            {
                mode = LooseBodyMode.Recovering;
                recoveryStartedAt = Time.time;
                return;
            }

            if (mode == LooseBodyMode.Recovering)
            {
                return;
            }

            mode = LooseBodyMode.Limp;
        }

        public void ApplySupportedPose(Vector3 localMove, float speed01, bool sprinting, bool grounded, float lookYaw, float lookPitch, GameBalanceProfile balance)
        {
            if (balance == null || !IsAvailable)
            {
                return;
            }

            var deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            var worldVelocity = (transform.position - lastRootPosition) / deltaTime;
            lastRootPosition = transform.position;

            var body = balance.playerBody;
            var movement = balance.playerMovement;
            var postRagdollBlend = ConsumePostRagdollBlend();
            if (mode == LooseBodyMode.Limp)
            {
                visualCollapse = Mathf.MoveTowards(visualCollapse, 1f, deltaTime * 3.2f);
                ApplyModelCollapseOffset();
                ApplyLimpPose(lookYaw, lookPitch, body);
                return;
            }

            if (mode == LooseBodyMode.Recovering)
            {
                var recovery01 = Mathf.Clamp01((Time.time - recoveryStartedAt) / RecoverySeconds);
                visualCollapse = Mathf.MoveTowards(visualCollapse, 0f, deltaTime / RecoverySeconds);
                ApplyModelCollapseOffset();
                ApplyRecoveringPose(lookYaw, lookPitch, body, recovery01);
                ApplyPostRagdollPositionBlend(1f - recovery01);
                ApplyPostRagdollBlend(1f - recovery01);
                if (recovery01 >= 1f)
                {
                    mode = LooseBodyMode.Supported;
                    visualCollapse = 0f;
                    ResetArmSimulation();
                    RestoreReferenceBonePositions();
                    ApplyModelCollapseOffset();
                }

                return;
            }

            visualCollapse = Mathf.MoveTowards(visualCollapse, 0f, deltaTime * 6f);
            jumpPulse = Mathf.MoveTowards(jumpPulse, 0f, deltaTime * 5.5f);
            landingPulse = Mathf.MoveTowards(landingPulse, 0f, deltaTime * Mathf.Max(0.1f, movement.landingRecoverySpeed));
            ApplyModelCollapseOffset();

            var moveAmount = Mathf.Clamp01(speed01);
            var strideSpeed = Mathf.Lerp(2.2f, sprinting ? 6.5f : 4.5f, moveAmount);
            strideClock += deltaTime * strideSpeed;
            var stride = Mathf.Sin(strideClock) * moveAmount;
            var oppositeStride = Mathf.Sin(strideClock + Mathf.PI) * moveAmount;
            var leanForward = -localMove.z * (body.supportedMoveLean + (sprinting ? body.sprintLeanBonus : 0f));
            var leanSide = localMove.x * body.supportedMoveLean;
            var localVelocity = transform.InverseTransformDirection(worldVelocity);
            var localAcceleration = (localVelocity - lastLocalVelocity) / deltaTime;
            lastLocalVelocity = localVelocity;
            var impactLag = Mathf.Clamp(Vector3.Dot(localVelocity, Vector3.forward), -5f, 5f);
            var lateralLag = Mathf.Clamp(localVelocity.x, -5f, 5f);
            var relaxed = relaxedMode ? 0.55f : 1f;
            var armRelaxed = relaxedMode ? 1.25f : 1f;
            var legRelaxed = relaxedMode ? 0.72f : 0.92f;
            var landingCompression = landingPulse * movement.landingCompression;
            var jumpExtension = jumpPulse * 0.65f;

            Drive(HumanBodyBones.Hips, new Vector3(leanForward * 0.2f + landingCompression * 10f - jumpExtension * 3f, 0f, -leanSide * 0.15f), body.pelvisTorque * 0.08f);
            Drive(HumanBodyBones.Spine, new Vector3(leanForward * 0.25f + lookPitch * body.chestPitchWeight * 0.45f + landingCompression * 16f, lookYaw * body.chestYawWeight * 1.25f, -leanSide * 0.18f), body.spinePoseSharpness * 0.75f);
            Drive(HumanBodyBones.Chest, new Vector3(leanForward * 0.35f + lookPitch * body.chestPitchWeight * 0.65f + landingCompression * 12f, lookYaw * body.chestYawWeight * 1.55f, -leanSide * 0.22f), body.spinePoseSharpness * 0.75f);
            Drive(HumanBodyBones.UpperChest, new Vector3(lookPitch * body.chestPitchWeight * 0.75f, lookYaw * body.chestYawWeight * 1.75f, -leanSide * 0.16f), body.spinePoseSharpness * 0.7f);
            Drive(HumanBodyBones.Neck, new Vector3(lookPitch * body.headPitchWeight * 0.7f, lookYaw * body.headYawWeight * 0.95f, 0f), body.headPoseSharpness * 0.85f);
            Drive(HumanBodyBones.Head, new Vector3(lookPitch * body.headPitchWeight * 0.75f, lookYaw * body.headYawWeight * 1.1f, -leanSide * 0.08f), body.headPoseSharpness * 0.85f);

            var lateralArmSway = lateralLag * body.armDragDegrees * 0.35f;

            var velocityDrag = new Vector3(
                Mathf.Clamp(-localVelocity.x * 0.42f, -2.2f, 2.2f),
                0f,
                Mathf.Clamp(-localVelocity.z * 0.34f, -1.8f, 1.8f));
            var accelerationDrag = new Vector3(
                Mathf.Clamp(-localAcceleration.x * 0.08f, -1.6f, 1.6f),
                Mathf.Clamp(-localAcceleration.y * 0.04f, -0.8f, 0.8f),
                Mathf.Clamp(-localAcceleration.z * 0.08f, -1.4f, 1.4f));
            var inertialDrag = transform.TransformDirection(velocityDrag + accelerationDrag);
            var gravityBias = Vector3.down * 0.72f;
            var leftUpperTarget = ConstrainArmOutsideBody((gravityBias - transform.right * 0.12f + inertialDrag).normalized, -1f);
            var rightUpperTarget = ConstrainArmOutsideBody((gravityBias + transform.right * 0.12f + inertialDrag).normalized, 1f);
            SimulateArmDirection(ref leftUpperArmDirection, ref leftUpperArmVelocity, leftUpperTarget, 8.5f, 0.08f, deltaTime, -1f, 75f);
            SimulateArmDirection(ref rightUpperArmDirection, ref rightUpperArmVelocity, rightUpperTarget, 8.5f, 0.08f, deltaTime, 1f, 75f);
            SimulateArmDirection(ref leftLowerArmDirection, ref leftLowerArmVelocity, (leftUpperArmDirection * 0.16f + Vector3.down * 0.42f + inertialDrag * 1.25f).normalized, 7f, 0.06f, deltaTime, -1f, 90f);
            SimulateArmDirection(ref rightLowerArmDirection, ref rightLowerArmVelocity, (rightUpperArmDirection * 0.16f + Vector3.down * 0.42f + inertialDrag * 1.25f).normalized, 7f, 0.06f, deltaTime, 1f, 90f);
            Drive(HumanBodyBones.LeftShoulder, new Vector3(0f, lookYaw * 0.01f, body.shoulderRollDegrees * armRelaxed * 0.65f + lateralArmSway * 0.08f), body.armPoseSharpness * 0.22f);
            Drive(HumanBodyBones.RightShoulder, new Vector3(0f, lookYaw * 0.01f, -body.shoulderRollDegrees * armRelaxed * 0.65f + lateralArmSway * 0.08f), body.armPoseSharpness * 0.22f);
            DriveLimbTowardWorldDirection(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, leftUpperArmDirection, body.armPoseSharpness * 8.5f);
            DriveLimbTowardWorldDirection(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, rightUpperArmDirection, body.armPoseSharpness * 8.5f);
            DriveLimbTowardWorldDirection(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, leftLowerArmDirection, body.armPoseSharpness * 7f);
            DriveLimbTowardWorldDirection(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, rightLowerArmDirection, body.armPoseSharpness * 7f);
            Drive(HumanBodyBones.LeftHand, new Vector3(0f, lateralArmSway * 0.04f, body.handRelaxDegrees * 0.5f), body.armPoseSharpness * 0.2f);
            Drive(HumanBodyBones.RightHand, new Vector3(0f, lateralArmSway * 0.04f, -body.handRelaxDegrees * 0.5f), body.armPoseSharpness * 0.2f);

            var legStrength = legRelaxed;
            var legSwing = body.legStrideDegrees * legStrength;
            Drive(HumanBodyBones.LeftUpperLeg, new Vector3(stride * legSwing + landingCompression * 16f - jumpExtension * 8f, 0f, -leanSide * 0.1f), body.legPoseSharpness * 0.72f);
            Drive(HumanBodyBones.RightUpperLeg, new Vector3(oppositeStride * legSwing + landingCompression * 16f - jumpExtension * 8f, 0f, -leanSide * 0.1f), body.legPoseSharpness * 0.72f);
            Drive(HumanBodyBones.LeftLowerLeg, new Vector3(Mathf.Max(0f, -stride * legSwing * 0.75f) + landingCompression * 34f - jumpExtension * 10f, 0f, 0f), body.legPoseSharpness * 0.68f);
            Drive(HumanBodyBones.RightLowerLeg, new Vector3(Mathf.Max(0f, -oppositeStride * legSwing * 0.75f) + landingCompression * 34f - jumpExtension * 10f, 0f, 0f), body.legPoseSharpness * 0.68f);
            Drive(HumanBodyBones.LeftFoot, new Vector3(grounded ? landingCompression * -10f : body.airborneTuckDegrees, 0f, 0f), body.legPoseSharpness * 0.55f);
            Drive(HumanBodyBones.RightFoot, new Vector3(grounded ? landingCompression * -10f : body.airborneTuckDegrees, 0f, 0f), body.legPoseSharpness * 0.55f);
            ApplyPostRagdollBlend(postRagdollBlend);
        }

        private void ApplyLimpPose(float lookYaw, float lookPitch, PlayerBodyTuning body)
        {
            Drive(HumanBodyBones.Hips, new Vector3(12f, 0f, 78f), body.spinePoseSharpness * 0.7f);
            Drive(HumanBodyBones.Spine, new Vector3(10f, lookYaw * 0.08f, 62f), body.spinePoseSharpness * 0.55f);
            Drive(HumanBodyBones.Chest, new Vector3(6f, lookYaw * 0.1f, 52f), body.spinePoseSharpness * 0.55f);
            Drive(HumanBodyBones.UpperChest, new Vector3(0f, lookYaw * 0.12f, 32f), body.spinePoseSharpness * 0.5f);
            Drive(HumanBodyBones.Neck, new Vector3(lookPitch * 0.18f - 8f, lookYaw * 0.18f, -8f), body.headPoseSharpness * 0.4f);
            Drive(HumanBodyBones.Head, new Vector3(lookPitch * 0.18f - 6f, lookYaw * 0.22f, -10f), body.headPoseSharpness * 0.4f);

            Drive(HumanBodyBones.LeftShoulder, new Vector3(6f, 0f, -24f), body.armPoseSharpness * 0.12f);
            Drive(HumanBodyBones.RightShoulder, new Vector3(6f, 0f, 24f), body.armPoseSharpness * 0.12f);
            DriveBestArmPose(HumanBodyBones.LeftUpperArm, 24f, -6f, -18f, body.armPoseSharpness * 0.1f, 0.9f);
            DriveBestArmPose(HumanBodyBones.RightUpperArm, 24f, 6f, 18f, body.armPoseSharpness * 0.1f, 0.9f);
            Drive(HumanBodyBones.LeftLowerArm, new Vector3(16f, 0f, -12f), body.armPoseSharpness * 0.08f);
            Drive(HumanBodyBones.RightLowerArm, new Vector3(16f, 0f, 12f), body.armPoseSharpness * 0.08f);
            Drive(HumanBodyBones.LeftHand, new Vector3(0f, 0f, -10f), body.armPoseSharpness * 0.04f);
            Drive(HumanBodyBones.RightHand, new Vector3(0f, 0f, 10f), body.armPoseSharpness * 0.04f);

            Drive(HumanBodyBones.LeftUpperLeg, new Vector3(8f, 0f, -18f), body.legPoseSharpness * 0.45f);
            Drive(HumanBodyBones.RightUpperLeg, new Vector3(-6f, 0f, 16f), body.legPoseSharpness * 0.45f);
            Drive(HumanBodyBones.LeftLowerLeg, new Vector3(8f, 0f, 0f), body.legPoseSharpness * 0.35f);
            Drive(HumanBodyBones.RightLowerLeg, new Vector3(12f, 0f, 0f), body.legPoseSharpness * 0.35f);
        }

        private void ApplyRecoveringPose(float lookYaw, float lookPitch, PlayerBodyTuning body, float recovery01)
        {
            var crouch = 1f - recovery01;
            Drive(HumanBodyBones.Hips, new Vector3(30f * crouch, 0f, 0f), body.spinePoseSharpness);
            Drive(HumanBodyBones.Spine, new Vector3(42f * crouch + lookPitch * body.chestPitchWeight * 0.25f, lookYaw * body.chestYawWeight * 0.5f, 0f), body.spinePoseSharpness);
            Drive(HumanBodyBones.Chest, new Vector3(38f * crouch + lookPitch * body.chestPitchWeight * 0.35f, lookYaw * body.chestYawWeight * 0.75f, 0f), body.spinePoseSharpness);
            Drive(HumanBodyBones.Head, new Vector3(lookPitch * body.headPitchWeight * 0.5f, lookYaw * body.headYawWeight * 0.6f, 0f), body.headPoseSharpness);
            Drive(HumanBodyBones.LeftUpperLeg, new Vector3(22f * crouch, 0f, -4f), body.legPoseSharpness);
            Drive(HumanBodyBones.RightUpperLeg, new Vector3(22f * crouch, 0f, 4f), body.legPoseSharpness);
            Drive(HumanBodyBones.LeftLowerLeg, new Vector3(28f * crouch, 0f, 0f), body.legPoseSharpness);
            Drive(HumanBodyBones.RightLowerLeg, new Vector3(28f * crouch, 0f, 0f), body.legPoseSharpness);
        }

        private void EnsureCameraSocket()
        {
            if (cameraSocket != null)
            {
                return;
            }

            if (!bones.TryGetValue(HumanBodyBones.Head, out var head) || head == null)
            {
                return;
            }

            var socketObject = new GameObject("PermanentHeadCameraSocket");
            cameraSocket = socketObject.transform;
            cameraSocket.SetParent(head, false);
            cameraSocket.localPosition = Vector3.zero;
            cameraSocket.localRotation = Quaternion.identity;
        }

        private void Drive(HumanBodyBones boneType, Vector3 eulerOffset, float sharpness)
        {
            if (!bones.TryGetValue(boneType, out var bone) || !baseRotations.TryGetValue(boneType, out var baseRotation))
            {
                return;
            }

            var target = baseRotation * Quaternion.Euler(eulerOffset);
            var t = 1f - Mathf.Exp(-Mathf.Max(0.01f, sharpness) * Time.deltaTime);
            bone.localRotation = Quaternion.Slerp(bone.localRotation, target, t);
        }

        private float ConsumePostRagdollBlend()
        {
            if (postRagdollStartRotations.Count == 0)
            {
                return 0f;
            }

            var blend01 = Mathf.Clamp01((Time.time - postRagdollBlendStartedAt) / PostRagdollBlendSeconds);
            var eased = blend01 * blend01 * (3f - 2f * blend01);
            if (blend01 >= 1f)
            {
                postRagdollStartRotations.Clear();
            }

            return 1f - eased;
        }

        private void ApplyPostRagdollBlend(float sourceWeight)
        {
            if (sourceWeight <= 0f)
            {
                return;
            }

            foreach (var pair in postRagdollStartRotations)
            {
                if (!bones.TryGetValue(pair.Key, out var bone) || bone == null)
                {
                    continue;
                }

                bone.localRotation = Quaternion.Slerp(bone.localRotation, pair.Value, sourceWeight);
            }
        }

        private void ApplyPostRagdollPositionBlend(float sourceWeight)
        {
            if (sourceWeight <= 0f)
            {
                return;
            }

            foreach (var pair in basePositions)
            {
                if (!bones.TryGetValue(pair.Key, out var bone) || bone == null)
                {
                    continue;
                }

                if (!postRagdollStartPositions.TryGetValue(pair.Key, out var startPosition))
                {
                    continue;
                }

                bone.localPosition = Vector3.Lerp(pair.Value, startPosition, sourceWeight);
            }
        }

        private void ApplyModelCollapseOffset()
        {
            if (modelRoot == null)
            {
                return;
            }

            modelRoot.localPosition = baseModelLocalPosition + Vector3.down * (visualCollapse * 0.72f);
        }

        private void ResetArmSimulation()
        {
            leftUpperArmDirection = (Vector3.down - transform.right * 0.24f).normalized;
            rightUpperArmDirection = (Vector3.down + transform.right * 0.24f).normalized;
            leftLowerArmDirection = Vector3.down;
            rightLowerArmDirection = Vector3.down;
            leftUpperArmVelocity = Vector3.zero;
            rightUpperArmVelocity = Vector3.zero;
            leftLowerArmVelocity = Vector3.zero;
            rightLowerArmVelocity = Vector3.zero;
        }

        private void SimulateArmDirection(ref Vector3 direction, ref Vector3 velocity, Vector3 targetDirection, float spring, float damping, float deltaTime, float side, float maxVelocity)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = targetDirection.sqrMagnitude > 0.0001f ? targetDirection.normalized : Vector3.down;
            }

            if (targetDirection.sqrMagnitude < 0.0001f)
            {
                targetDirection = Vector3.down;
            }

            var acceleration = (targetDirection.normalized - direction.normalized) * spring + Vector3.down * 6f;
            velocity += acceleration * deltaTime;
            velocity *= Mathf.Exp(-damping * deltaTime);
            velocity = Vector3.ClampMagnitude(velocity, maxVelocity);

            direction = ConstrainArmOutsideBody((direction + velocity * deltaTime).normalized, side);
            velocity = Vector3.ProjectOnPlane(velocity, direction);
        }

        private Vector3 ConstrainArmOutsideBody(Vector3 direction, float side)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                return Vector3.down;
            }

            direction.Normalize();
            var outward = transform.right * side;
            var outwardDot = Vector3.Dot(direction, outward);
            var downDot = Vector3.Dot(direction, Vector3.down);
            if (outwardDot < 0.16f && downDot < 0.94f)
            {
                direction = (direction + outward * (0.16f - outwardDot) * 2.5f).normalized;
            }

            if (Vector3.Dot(direction, Vector3.up) > 0.05f)
            {
                direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
                direction = (direction + Vector3.down * 0.15f).normalized;
            }

            return direction;
        }

        private void DriveLimbTowardWorldDirection(HumanBodyBones boneType, HumanBodyBones childType, Vector3 targetDirection, float sharpness)
        {
            if (!bones.TryGetValue(boneType, out var bone) ||
                !bones.TryGetValue(childType, out var child) ||
                bone == null ||
                child == null ||
                bone.parent == null)
            {
                return;
            }

            var currentDirection = child.position - bone.position;
            if (currentDirection.sqrMagnitude < 0.0001f || targetDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var worldDelta = Quaternion.FromToRotation(currentDirection.normalized, targetDirection.normalized);
            var targetWorldRotation = worldDelta * bone.rotation;
            var targetLocalRotation = Quaternion.Inverse(bone.parent.rotation) * targetWorldRotation;
            var t = 1f - Mathf.Exp(-Mathf.Max(0.01f, sharpness) * Time.deltaTime);
            bone.localRotation = Quaternion.Slerp(bone.localRotation, targetLocalRotation, t);
        }

        private void DriveBestArmPose(HumanBodyBones boneType, float x, float y, float z, float sharpness, float hangWeight)
        {
            if (!bones.TryGetValue(boneType, out var bone) || !baseRotations.TryGetValue(boneType, out var baseRotation))
            {
                return;
            }

            var procedural = baseRotation * Quaternion.Euler(x, y, z);
            var target = Quaternion.Slerp(procedural, GetArmHangLocalRotation(boneType, bone), Mathf.Clamp01(hangWeight));
            var t = 1f - Mathf.Exp(-Mathf.Max(0.01f, sharpness) * Time.deltaTime);
            bone.localRotation = Quaternion.Slerp(bone.localRotation, target, t);
        }

        private Quaternion GetArmHangLocalRotation(HumanBodyBones boneType, Transform upperArm)
        {
            var childType = boneType == HumanBodyBones.LeftUpperArm ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
            if (!bones.TryGetValue(childType, out var lowerArm) || lowerArm == null || upperArm.parent == null)
            {
                return upperArm.localRotation;
            }

            var currentDirection = (lowerArm.position - upperArm.position).normalized;
            if (currentDirection.sqrMagnitude < 0.0001f)
            {
                return upperArm.localRotation;
            }

            var side = boneType == HumanBodyBones.LeftUpperArm ? -1f : 1f;
            var desiredDirection = (Vector3.down + transform.right * side * 0.08f + transform.forward * 0.04f).normalized;
            var worldDelta = Quaternion.FromToRotation(currentDirection, desiredDirection);
            var targetWorldRotation = worldDelta * upperArm.rotation;
            return Quaternion.Inverse(upperArm.parent.rotation) * targetWorldRotation;
        }

        private bool TryGetRendererBounds(out Bounds bounds)
        {
            var renderers = modelRoot != null ? modelRoot.GetComponentsInChildren<Renderer>(true) : System.Array.Empty<Renderer>();
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return true;
        }
    }
}

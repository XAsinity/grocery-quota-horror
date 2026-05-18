using GroceryQuotaHorror.Data;
using UnityEngine;

namespace GroceryQuotaHorror.Player
{
    public sealed class ProceduralPoseSource : IPlayerPoseSource
    {
        public Vector3 GetPelvisOffset(in PlayerPoseContext context, GameBalanceProfile balance)
        {
            var body = balance.playerBody;
            var bobWave = Mathf.Sin(context.Time * body.supportedBobFrequency) * body.supportedBobAmplitude * context.Speed01;
            var recoveryLift = (1f - context.Recovery01) * body.supportedBobAmplitude;
            return new Vector3(
                -context.LocalMove.x * body.stumbleResponse,
                bobWave + recoveryLift,
                0f);
        }

        public Quaternion GetLocalRotation(HumanBodyBones bone, in PlayerPoseContext context, Quaternion baseLocalRotation, GameBalanceProfile balance)
        {
            var body = balance.playerBody;
            var stride = Mathf.Sin(context.Time * (body.supportedBobFrequency * 0.6f)) * context.Speed01;
            var armSwing = stride * body.armSwingDegrees;
            var legSwing = stride * body.legStrideDegrees;
            var leanForward = -context.LocalMove.z * (body.supportedMoveLean + (context.Sprinting ? body.sprintLeanBonus : 0f));
            var leanSide = context.LocalMove.x * body.supportedMoveLean;
            var recovery = Mathf.Lerp(0.4f, 1f, context.Recovery01);

            switch (bone)
            {
                case HumanBodyBones.Hips:
                    return baseLocalRotation * Quaternion.Euler(leanForward * 0.2f * recovery, 0f, -leanSide * 0.15f * recovery);
                case HumanBodyBones.Spine:
                case HumanBodyBones.Chest:
                case HumanBodyBones.UpperChest:
                    return baseLocalRotation * Quaternion.Euler(
                        leanForward * 0.35f * recovery + context.LookPitch * body.chestPitchWeight * recovery,
                        context.LookYaw * body.chestYawWeight * recovery,
                        -leanSide * 0.2f * recovery);
                case HumanBodyBones.LeftShoulder:
                    return baseLocalRotation * Quaternion.Euler(
                        0f,
                        context.LookYaw * body.shoulderYawWeight * recovery,
                        body.shoulderRollDegrees + leanSide * 0.08f * recovery);
                case HumanBodyBones.RightShoulder:
                    return baseLocalRotation * Quaternion.Euler(
                        0f,
                        context.LookYaw * body.shoulderYawWeight * recovery,
                        -body.shoulderRollDegrees + leanSide * 0.08f * recovery);
                case HumanBodyBones.Neck:
                case HumanBodyBones.Head:
                    return baseLocalRotation * Quaternion.Euler(
                        context.LookPitch * body.headPitchWeight * recovery,
                        context.LookYaw * body.headYawWeight * recovery,
                        -leanSide * 0.1f * recovery);
                case HumanBodyBones.LeftUpperArm:
                    return baseLocalRotation * Quaternion.Euler(
                        -armSwing * 0.65f - context.LocalMove.z * body.armDragDegrees,
                        context.LookYaw * 0.28f,
                        body.armHangDegrees + leanSide * 0.32f + context.LocalMove.z * 0.18f);
                case HumanBodyBones.RightUpperArm:
                    return baseLocalRotation * Quaternion.Euler(
                        armSwing * 0.65f - context.LocalMove.z * body.armDragDegrees,
                        context.LookYaw * 0.28f,
                        -body.armHangDegrees + leanSide * 0.32f - context.LocalMove.z * 0.18f);
                case HumanBodyBones.LeftLowerArm:
                    return baseLocalRotation * Quaternion.Euler(
                        body.armLowerBendDegrees - armSwing * 0.45f,
                        0f,
                        body.handRelaxDegrees * 0.55f);
                case HumanBodyBones.RightLowerArm:
                    return baseLocalRotation * Quaternion.Euler(
                        body.armLowerBendDegrees + armSwing * 0.45f,
                        0f,
                        -body.handRelaxDegrees * 0.55f);
                case HumanBodyBones.LeftHand:
                    return baseLocalRotation * Quaternion.Euler(0f, 0f, body.handRelaxDegrees);
                case HumanBodyBones.RightHand:
                    return baseLocalRotation * Quaternion.Euler(0f, 0f, -body.handRelaxDegrees);
                case HumanBodyBones.LeftUpperLeg:
                    return baseLocalRotation * Quaternion.Euler(legSwing, 0f, -leanSide * 0.12f);
                case HumanBodyBones.RightUpperLeg:
                    return baseLocalRotation * Quaternion.Euler(-legSwing, 0f, -leanSide * 0.12f);
                case HumanBodyBones.LeftLowerLeg:
                    return baseLocalRotation * Quaternion.Euler(Mathf.Max(0f, -legSwing * 0.75f), 0f, 0f);
                case HumanBodyBones.RightLowerLeg:
                    return baseLocalRotation * Quaternion.Euler(Mathf.Max(0f, legSwing * 0.75f), 0f, 0f);
                case HumanBodyBones.LeftFoot:
                case HumanBodyBones.RightFoot:
                    return baseLocalRotation * Quaternion.Euler(context.Grounded ? 0f : body.airborneTuckDegrees, 0f, 0f);
                default:
                    return baseLocalRotation;
            }
        }
    }
}

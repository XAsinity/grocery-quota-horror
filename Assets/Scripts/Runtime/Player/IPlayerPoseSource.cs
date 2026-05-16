using GroceryQuotaHorror.Data;
using UnityEngine;

namespace GroceryQuotaHorror.Player
{
    public readonly struct PlayerPoseContext
    {
        public PlayerPoseContext(
            float time,
            Vector3 localMove,
            float speed01,
            bool sprinting,
            bool grounded,
            float lookPitch,
            float recentImpact,
            float recovery01)
        {
            Time = time;
            LocalMove = localMove;
            Speed01 = speed01;
            Sprinting = sprinting;
            Grounded = grounded;
            LookPitch = lookPitch;
            RecentImpact = recentImpact;
            Recovery01 = recovery01;
        }

        public float Time { get; }
        public Vector3 LocalMove { get; }
        public float Speed01 { get; }
        public bool Sprinting { get; }
        public bool Grounded { get; }
        public float LookPitch { get; }
        public float RecentImpact { get; }
        public float Recovery01 { get; }
    }

    public interface IPlayerPoseSource
    {
        Vector3 GetPelvisOffset(in PlayerPoseContext context, GameBalanceProfile balance);
        Quaternion GetLocalRotation(HumanBodyBones bone, in PlayerPoseContext context, Quaternion baseLocalRotation, GameBalanceProfile balance);
    }
}

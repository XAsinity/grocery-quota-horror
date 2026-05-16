using UnityEngine;

namespace GroceryQuotaHorror.Data
{
    public static class GameplayTuningRuntime
    {
        private const string ResourceName = "GameplayTuning";

        private static GameplayTuning cached;

        public static GameplayTuning Current
        {
            get
            {
                if (GameRuntime.Balance != null)
                {
                    return null;
                }

                if (cached == null)
                {
                    cached = Resources.Load<GameplayTuning>(ResourceName);
                }

                return cached;
            }
        }

        public static void ApplyGlobalPhysics()
        {
            if (GameRuntime.Balance != null)
            {
                GameRuntime.ApplyGlobalPhysics();
            }
            else if (Current != null)
            {
                UnityEngine.Physics.gravity = Current.worldGravity;
            }
        }
    }
}

using GroceryQuotaHorror.Data;
using UnityEngine;

namespace GroceryQuotaHorror.Physics
{
    public sealed class RagdollHazardTestRig : MonoBehaviour
    {
        [Tooltip("Hammer prefab to spawn. Position and runtime physics values come from GameBalanceProfile > Prototype Hazards.")]
        [SerializeField] private GameObject swingingHammerPrefab;
        [Tooltip("Fallback hammer spawn position when no balance profile is active.")]
        [SerializeField] private Vector3 swingingBlockPosition = new(-3.8f, 2.4f, 205f);
        [Tooltip("Fallback ball shooter spawn position when no balance profile is active.")]
        [SerializeField] private Vector3 ballShooterPosition = new(5.5f, 1.15f, 201.5f);
        [Tooltip("Fallback target point the ball shooter aims at when no balance profile is active.")]
        [SerializeField] private Vector3 ballShooterTarget = new(0f, 1.15f, 205f);

        private void Start()
        {
            CreateSwingingBlock();
            CreateBallShooter();
        }

        private void CreateSwingingBlock()
        {
            var block = swingingHammerPrefab != null
                ? Instantiate(swingingHammerPrefab)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = swingingHammerPrefab != null ? "Prototype Swinging Ragdoll Hammer" : "Prototype Swinging Ragdoll Block";
            var hazards = GetHazards();
            block.transform.position = hazards.testHammerPosition;
            if (swingingHammerPrefab == null)
            {
                block.transform.localScale = hazards.fallbackHammerScale;
            }

            if (block.GetComponent<SwingingImpactBlock>() == null)
            {
                block.AddComponent<SwingingImpactBlock>();
            }
        }

        private void CreateBallShooter()
        {
            var hazards = GetHazards();
            var shooter = new GameObject("Prototype Ragdoll Ball Shooter");
            var shotDirection = hazards.testBallShooterTarget - hazards.testBallShooterPosition;
            if (shotDirection.sqrMagnitude < 0.0001f)
            {
                shotDirection = Vector3.forward;
            }

            shooter.transform.SetPositionAndRotation(hazards.testBallShooterPosition, Quaternion.LookRotation(shotDirection.normalized, Vector3.up));
            shooter.AddComponent<BallShooter>();
        }

        private PrototypeHazardTuning GetHazards()
        {
            return GameRuntime.Balance != null
                ? GameRuntime.Balance.hazards
                : new PrototypeHazardTuning
                {
                    testHammerPosition = swingingBlockPosition,
                    testBallShooterPosition = ballShooterPosition,
                    testBallShooterTarget = ballShooterTarget
                };
        }
    }
}

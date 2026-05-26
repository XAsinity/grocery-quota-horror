using GroceryQuotaHorror.Data;
using UnityEngine;

namespace GroceryQuotaHorror.Physics
{
    public sealed class BallShooter : MonoBehaviour
    {
        [Tooltip("When enabled, all ball-shooter values come from GameBalanceProfile > Prototype Hazards.")]
        [SerializeField] private bool useBalanceProfile = true;
        [Tooltip("Fallback seconds before the first ball is fired.")]
        [SerializeField] private float initialDelay = 0.75f;
        [Tooltip("Fallback seconds between fired balls.")]
        [SerializeField] private float fireInterval = 2.2f;
        [Tooltip("Fallback initial ball speed.")]
        [SerializeField] private float ballSpeed = 16f;
        [Tooltip("Fallback mass of fired balls.")]
        [SerializeField] private float ballMass = 2f;
        [Tooltip("Fallback visual/collider scale of fired balls.")]
        [SerializeField] private float ballScale = 0.42f;
        [Tooltip("Fallback seconds before fired balls are destroyed.")]
        [SerializeField] private float ballLifetime = 6f;

        private float nextFireTime;

        private void Start()
        {
            nextFireTime = Time.time + GetHazards().ballInitialDelay;
        }

        private void Update()
        {
            if (Time.time < nextFireTime)
            {
                return;
            }

            var hazards = GetHazards();
            nextFireTime = Time.time + hazards.ballFireInterval;
            FireBall();
        }

        private void FireBall()
        {
            var hazards = GetHazards();
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Impact Test Ball";
            ball.transform.position = transform.position;
            ball.transform.localScale = Vector3.one * hazards.ballScale;

            var body = ball.AddComponent<Rigidbody>();
            body.mass = hazards.ballMass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = transform.forward * hazards.ballSpeed;
            ball.AddComponent<PhysicsImpactAudio>();

            Destroy(ball, hazards.ballLifetime);
        }

        private PrototypeHazardTuning GetHazards()
        {
            return useBalanceProfile && GameRuntime.Balance != null
                ? GameRuntime.Balance.hazards
                : new PrototypeHazardTuning
                {
                    ballInitialDelay = initialDelay,
                    ballFireInterval = fireInterval,
                    ballSpeed = ballSpeed,
                    ballMass = ballMass,
                    ballScale = ballScale,
                    ballLifetime = ballLifetime
                };
        }
    }
}

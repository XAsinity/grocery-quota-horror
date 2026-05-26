using GroceryQuotaHorror.Data;
using UnityEngine;

namespace GroceryQuotaHorror.Physics
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(HingeJoint))]
    public sealed class SwingingImpactBlock : MonoBehaviour
    {
        [Tooltip("When enabled, hammer physics values come from GameBalanceProfile > Prototype Hazards.")]
        [SerializeField] private bool useBalanceProfile = true;
        [Tooltip("Fallback local hinge anchor used when not using balance profile values.")]
        [SerializeField] private Vector3 localHingeAnchor = new(0f, 0.72f, 0f);
        [Tooltip("Fallback: if enabled, hinge anchor is placed at the top of visible renderer bounds.")]
        [SerializeField] private bool autoAnchorToTop = true;
        [Tooltip("Fallback: if enabled, the hammer spins continuously instead of reversing at limits.")]
        [SerializeField] private bool continuousSpin = true;
        [Tooltip("Fallback swing angle used only when continuous spin is disabled.")]
        [SerializeField] private float swingLimitDegrees = 68f;
        [Tooltip("Fallback hinge motor speed.")]
        [SerializeField] private float motorSpeed = 175f;
        [Tooltip("Fallback hinge motor force.")]
        [SerializeField] private float motorForce = 12000f;
        [Tooltip("Fallback hammer rigidbody mass.")]
        [SerializeField] private float mass = 240f;
        [Tooltip("Fallback maximum angular velocity for the hammer rigidbody.")]
        [SerializeField] private float maxAngularVelocity = 50f;

        private HingeJoint hinge;
        private PrototypeHazardTuning hazards;
        private int swingDirection = 1;

        private void Awake()
        {
            hazards = GetHazards();
            var body = GetComponent<Rigidbody>();
            body.mass = hazards.hammerMass;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.maxAngularVelocity = hazards.hammerMaxAngularVelocity;
            if (GetComponent<PhysicsImpactAudio>() == null)
            {
                gameObject.AddComponent<PhysicsImpactAudio>();
            }

            var box = GetComponent<BoxCollider>();
            if (box.size == Vector3.zero)
            {
                box.size = Vector3.one;
            }

            hinge = GetComponent<HingeJoint>();
            hinge.axis = Vector3.right;
            var hingeAnchor = hazards.autoAnchorHammerToTop ? ResolveTopHingeAnchor() : hazards.hammerLocalHingeAnchor;
            hinge.anchor = hingeAnchor;
            hinge.autoConfigureConnectedAnchor = false;
            hinge.connectedAnchor = transform.TransformPoint(hingeAnchor);
            hinge.useLimits = !hazards.hammerContinuousSpin;
            hinge.limits = new JointLimits
            {
                min = -hazards.hammerSwingLimitDegrees,
                max = hazards.hammerSwingLimitDegrees,
                bounciness = 0.08f,
                bounceMinVelocity = 0.2f,
                contactDistance = 4f
            };
            hinge.useMotor = true;
            ApplyMotor();
        }

        private void FixedUpdate()
        {
            if (hinge == null)
            {
                return;
            }

            hazards = GetHazards();
            if (hazards.hammerContinuousSpin)
            {
                swingDirection = 1;
                ApplyMotor();
                return;
            }

            if (hinge.angle > hazards.hammerSwingLimitDegrees - 8f)
            {
                swingDirection = -1;
            }
            else if (hinge.angle < -hazards.hammerSwingLimitDegrees + 8f)
            {
                swingDirection = 1;
            }

            ApplyMotor();
        }

        private void ApplyMotor()
        {
            hinge.motor = new JointMotor
            {
                force = hazards.hammerMotorForce,
                targetVelocity = hazards.hammerMotorSpeed * swingDirection,
                freeSpin = hazards.hammerContinuousSpin
            };
        }

        private Vector3 ResolveTopHingeAnchor()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return hazards.hammerLocalHingeAnchor;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            var topWorld = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            return transform.InverseTransformPoint(topWorld);
        }

        private PrototypeHazardTuning GetHazards()
        {
            return useBalanceProfile && GameRuntime.Balance != null
                ? GameRuntime.Balance.hazards
                : new PrototypeHazardTuning
                {
                    hammerLocalHingeAnchor = localHingeAnchor,
                    autoAnchorHammerToTop = autoAnchorToTop,
                    hammerContinuousSpin = continuousSpin,
                    hammerSwingLimitDegrees = swingLimitDegrees,
                    hammerMotorSpeed = motorSpeed,
                    hammerMotorForce = motorForce,
                    hammerMass = mass,
                    hammerMaxAngularVelocity = maxAngularVelocity
                };
        }
    }
}

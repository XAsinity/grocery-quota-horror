using System;
using UnityEngine;

namespace GroceryQuotaHorror.Data
{
    [CreateAssetMenu(menuName = "Grocery Quota Horror/Game Balance Profile", fileName = "GameBalanceProfile")]
    public sealed class GameBalanceProfile : ScriptableObject
    {
        [Header("Global Physics")]
        [Tooltip("World-level physics values applied when this balance profile becomes active.")]
        public GlobalPhysicsTuning globalPhysics = new();

        [Header("Player Movement")]
        [Tooltip("Core player movement, jumping, gravity, and mouse look values.")]
        public PlayerMovementTuning playerMovement = new();

        [Header("Player Camera")]
        [Tooltip("First-person camera placement, follow smoothing, limp camera behavior, and recovery blending.")]
        public PlayerCameraTuning playerCamera = new();

        [Header("Player Body")]
        [Tooltip("Procedural supported-body pose, loose limb, leaning, look, and active ragdoll drive values.")]
        public PlayerBodyTuning playerBody = new();

        [Header("Ragdoll")]
        [Tooltip("Ragdoll spawning, grabbing, recovery, and velocity-impact response values.")]
        public RagdollTuning ragdoll = new();

        [Header("Prototype Hazards")]
        [Tooltip("Central command center for the prototype hammer, ball shooter, and impact-source test objects.")]
        public PrototypeHazardTuning hazards = new();

        [Header("Interaction")]
        [Tooltip("Pickup, grab, interact, and throw distances/forces.")]
        public InteractionTuning interaction = new();

        [Header("Projectile")]
        [Tooltip("Manual debug physics ball values used by the player B-key ball throw.")]
        public ProjectileTuning projectile = new();

        [Header("Monster")]
        [Tooltip("Basic monster movement and attack tuning.")]
        public MonsterTuning monster = new();

        [Header("Objectives")]
        [Tooltip("Night length and shopping quota generation settings.")]
        public ObjectiveTuning objectives = new();

        [Header("Spawn")]
        [Tooltip("Run seed and spawn budget settings.")]
        public SpawnTuning spawn = new();

        [Header("Layout")]
        [Tooltip("Generated store layout size and spacing settings.")]
        public LayoutTuning layout = new();

        [Header("Debug")]
        [Tooltip("Runtime debug panel, keybind, and profile switching options.")]
        public DebugTuning debug = new();
    }

    [Serializable]
    public sealed class GlobalPhysicsTuning
    {
        [Tooltip("Unity Physics.gravity applied at runtime. More negative Y makes all physics fall harder.")]
        public Vector3 worldGravity = new(0f, -9.81f, 0f);
    }

    [Serializable]
    public sealed class PlayerMovementTuning
    {
        [Tooltip("Base walking speed before sprint multiplier.")]
        public float moveSpeed = 4f;
        [Tooltip("Multiplier applied to move speed while sprinting.")]
        public float sprintMultiplier = 1.6f;
        [Tooltip("Downward acceleration applied to the player rigidbody. More negative means heavier/faster falling.")]
        public float playerGravity = -20f;
        [Tooltip("Mouse input sensitivity before turn-rate scaling.")]
        public float lookSensitivity = 2.2f;
        [Tooltip("Maximum configured pitch angle. Runtime camera safety clamps may reduce this further.")]
        public float maxLookPitch = 80f;
        [Tooltip("Degrees per second used to turn the body/camera from mouse input.")]
        public float mouseTurnRate = 120f;
        [Tooltip("Fallback camera height from the player root when no head socket is available.")]
        public float cameraHeight = 1.55f;
        [Tooltip("How quickly movement input smooths toward the requested direction. Higher is snappier.")]
        public float movementSmoothing = 10f;
        [Tooltip("Movement control multiplier while airborne.")]
        public float airControlMultiplier = 0.45f;
        [Tooltip("Extra force pushing the player into the ground when grounded to reduce floatiness.")]
        public float groundStickForce = 14f;
        [Tooltip("Fastest downward velocity allowed while grounded.")]
        public float groundedMaxFallSpeed = -6f;
        [Tooltip("How strongly horizontal rigidbody velocity follows desired movement.")]
        public float groundVelocityFollow = 0.82f;
        [Tooltip("Initial upward velocity applied when jumping.")]
        public float jumpVelocity = 6.4f;
        [Tooltip("Minimum time between jumps.")]
        public float jumpCooldown = 0.18f;
        [Tooltip("How long a jump input is buffered before the player becomes grounded.")]
        public float jumpBufferSeconds = 0.12f;
        [Tooltip("How much the visible body compresses on landing.")]
        public float landingCompression = 1f;
        [Tooltip("How quickly landing compression relaxes back to normal.")]
        public float landingRecoverySpeed = 7f;
    }

    [Serializable]
    public sealed class PlayerCameraTuning
    {
        [Tooltip("Fallback supported camera local offset from the player root.")]
        public Vector3 supportedLocalOffset = new(0f, 1.55f, 0f);
        [Tooltip("First-person camera socket offset from the humanoid head. Increase Z to move toward the eyes/front of face.")]
        public Vector3 supportedHeadLocalOffset = new(0f, 0.05f, 0.18f);
        [Tooltip("How much the supported camera follows the head position when active ragdoll camera targeting is used.")]
        public float supportedHeadPositionWeight = 0.72f;
        [Tooltip("How much supported camera rotation follows the head when active ragdoll camera targeting is used.")]
        public float supportedHeadRotationWeight = 0.42f;
        [Tooltip("How sharply the camera follows supported target position.")]
        public float supportedFollowPositionSharpness = 18f;
        [Tooltip("How sharply the camera follows supported target rotation.")]
        public float supportedFollowRotationSharpness = 16f;
        [Tooltip("Distance where supported camera snaps instead of smoothing.")]
        public float supportedSnapDistance = 0.35f;
        [Tooltip("How sharply the camera follows position while the player is limp.")]
        public float limpFollowPositionSharpness = 10f;
        [Tooltip("How sharply the camera follows rotation while the player is limp.")]
        public float limpFollowRotationSharpness = 8f;
        [Tooltip("Distance where limp camera snaps instead of smoothing.")]
        public float limpSnapDistance = 0.65f;
        [Tooltip("Seconds used to blend from ragdoll camera back into normal first-person view during recovery.")]
        public float recoveryBlendSeconds = 0.22f;
        [Tooltip("Maximum upward look pitch allowed by the first-person camera safety clamp.")]
        public float firstPersonLookUpLimit = 72f;
        [Tooltip("Maximum downward look pitch allowed by the first-person camera safety clamp.")]
        public float firstPersonLookDownLimit = 58f;
        [Tooltip("Field of view forced onto the supported first-person camera.")]
        public float supportedCameraFieldOfView = 64f;
        [Tooltip("Near clip plane forced onto the supported first-person camera.")]
        public float supportedCameraNearClip = 0.08f;
    }

    [Serializable]
    public sealed class PlayerBodyTuning
    {
        [Header("Active Ragdoll Drive")]
        [Tooltip("Force that pulls the pelvis/body toward the supported player target.")]
        public float pelvisFollowForce = 120f;
        [Tooltip("Linear damping on pelvis follow movement.")]
        public float pelvisDamping = 12f;
        [Tooltip("Torque used to rotate the pelvis toward its supported pose.")]
        public float pelvisTorque = 14f;
        [Tooltip("Base torque used for driven ragdoll bones.")]
        public float boneTorque = 8f;
        [Tooltip("Angular damping applied to driven bones.")]
        public float boneAngularDamping = 0.75f;
        [Tooltip("Linear damping while the active ragdoll is supported.")]
        public float supportedLinearDamping = 0.6f;
        [Tooltip("Angular damping while the active ragdoll is supported.")]
        public float supportedAngularDamping = 0.12f;
        [Tooltip("Linear damping while downed/limp.")]
        public float downedLinearDamping = 0.18f;
        [Tooltip("Angular damping while downed/limp.")]
        public float downedAngularDamping = 0.06f;
        [Tooltip("Drive multiplier used while downed.")]
        public float downedDriveMultiplier = 0.38f;
        [Tooltip("Drive multiplier used while supported.")]
        public float supportedDriveMultiplier = 1f;

        [Header("Supported Body Motion")]
        [Tooltip("Vertical bob amount applied to the supported visual body.")]
        public float supportedBobAmplitude = 0.05f;
        [Tooltip("Frequency of supported movement bobbing.")]
        public float supportedBobFrequency = 5.5f;
        [Tooltip("How far the body leans with movement input.")]
        public float supportedMoveLean = 10f;
        [Tooltip("Extra forward lean while sprinting.")]
        public float sprintLeanBonus = 6f;
        [Tooltip("Maximum ALT/free-look yaw range in degrees.")]
        public float supportedLookYawDegrees = 98f;
        [Tooltip("How quickly ALT/free-look yaw returns to center when released.")]
        public float supportedLookYawReturnSharpness = 7f;
        [Tooltip("How quickly spine pose follows supported procedural pose.")]
        public float spinePoseSharpness = 10f;
        [Tooltip("How quickly head pose follows supported procedural pose.")]
        public float headPoseSharpness = 14f;
        [Tooltip("How quickly arm pose follows supported procedural pose.")]
        public float armPoseSharpness = 4f;
        [Tooltip("How quickly leg pose follows supported procedural pose.")]
        public float legPoseSharpness = 11f;

        [Header("Loose Arms")]
        [Tooltip("Spring strength pulling upper arms toward their procedural loose target.")]
        public float supportedUpperArmSpring = 18f;
        [Tooltip("Damping applied to upper-arm loose motion.")]
        public float supportedUpperArmDamping = 3.6f;
        [Tooltip("Spring strength pulling lower arms toward their procedural loose target.")]
        public float supportedLowerArmSpring = 8f;
        [Tooltip("Damping applied to lower-arm loose motion.")]
        public float supportedLowerArmDamping = 2f;

        [Header("Pose Weights")]
        [Tooltip("How much camera pitch influences the head pose.")]
        public float headPitchWeight = 0.5f;
        [Tooltip("How much ALT/free-look yaw influences the head pose.")]
        public float headYawWeight = 0.85f;
        [Tooltip("How much camera pitch influences chest pose.")]
        public float chestPitchWeight = 0.18f;
        [Tooltip("How much ALT/free-look yaw influences chest pose.")]
        public float chestYawWeight = 0.35f;
        [Tooltip("How much shoulders follow yaw/freelook.")]
        public float shoulderYawWeight = 0.18f;
        [Tooltip("Resting shoulder roll angle.")]
        public float shoulderRollDegrees = 12f;
        [Tooltip("How far arms hang downward in supported loose mode.")]
        public float armHangDegrees = 18f;
        [Tooltip("Default lower-arm bend angle.")]
        public float armLowerBendDegrees = 16f;
        [Tooltip("Default relaxed hand angle.")]
        public float handRelaxDegrees = 8f;
        [Tooltip("Arm swing amount while moving.")]
        public float armSwingDegrees = 16f;
        [Tooltip("Leg stride amount while moving.")]
        public float legStrideDegrees = 18f;
        [Tooltip("How much arms lag behind player velocity changes.")]
        public float armDragDegrees = 6f;
        [Tooltip("How much limbs tuck while airborne.")]
        public float airborneTuckDegrees = 10f;
        [Tooltip("How much the body visually stumbles from recent impacts/motion.")]
        public float stumbleResponse = 0.04f;
    }

    [Serializable]
    public sealed class RagdollTuning
    {
        [Header("Manual Ragdoll Spawn")]
        [Tooltip("Distance in front of the player where P-key spawned ragdolls appear.")]
        public float spawnDistance = 3f;
        [Tooltip("Vertical offset for P-key spawned ragdolls.")]
        public float spawnVerticalOffset = 0.2f;
        [Tooltip("Swing angle limit used when runtime ragdoll joints are built.")]
        public float jointSwingLimit = 25f;
        [Tooltip("Twist angle limit used when runtime ragdoll joints are built.")]
        public float jointTwistLimit = 20f;
        [Tooltip("Torque applied to the head when looking around while limp.")]
        public float headLookTorque = 2.4f;
        [Tooltip("Upward assist force applied to the head while looking while limp.")]
        public float headLiftForce = 0.65f;
        [Tooltip("Assist force used by active ragdoll recovery behavior.")]
        public float recoveryAssistForce = 24f;

        [Header("Ragdoll Grab")]
        [Tooltip("Force used to pull grabbed ragdoll bodies toward the hold target.")]
        public float ragdollHoldForce = 180f;
        [Tooltip("Damping used when pulling grabbed ragdoll bodies.")]
        public float ragdollHoldDamping = 4.5f;
        [Tooltip("Linear damping applied to a ragdoll while it is grabbed.")]
        public float ragdollHeldLinearDamping = 0.25f;
        [Tooltip("Angular damping applied to a ragdoll while it is grabbed.")]
        public float ragdollHeldAngularDamping = 0.35f;
        [Tooltip("How strongly held ragdolls are assisted vertically toward the hold point.")]
        public float ragdollHeldVerticalAssist = 0.9f;
        [Tooltip("Maximum force allowed while pulling a grabbed ragdoll.")]
        public float ragdollHeldMaxForce = 360f;

        [Header("Velocity Impact Ragdoll")]
        [Tooltip("If enabled, the player listens for any external rigidbody collision and evaluates it as a possible ragdoll impact. This makes thrown cans, deflected balls, hammers, and future physics props use the same rule.")]
        public bool impactWorldCollisionEnabled = true;
        [Tooltip("Minimum rigidbody mass allowed to trigger player impact checks. Raise this if tiny debris is knocking the player over too often.")]
        public float impactMinimumSourceMass = 0.05f;
        [Tooltip("Minimum relative collision speed required to ragdoll the player.")]
        public float impactVelocityThreshold = 7.5f;
        [Tooltip("How much of impact speed becomes initial whole-body ragdoll velocity before sharing/caps.")]
        public float impactVelocityTransfer = 0.18f;
        [Tooltip("Maximum transferred velocity before whole-body share is applied.")]
        public float impactMaxTransferredSpeed = 12f;
        [Tooltip("Multiplier converting impact speed and source mass into targeted local impulse.")]
        public float impactImpulseMultiplier = 0.42f;
        [Tooltip("Maximum targeted impact impulse applied to the closest hit body part.")]
        public float impactMaxImpulse = 13f;
        [Tooltip("Mass treated as a normal human-sized impact source. Heavier sources scale impact up; lighter sources scale down.")]
        public float impactMassReference = 70f;
        [Tooltip("Share of transferred velocity applied to the whole body for normal upper-body hits.")]
        public float impactWholeBodyVelocityShare = 0.3f;
        [Tooltip("How close a contact must be to the visible body bounds to count as a real hit.")]
        public float impactContactPadding = 0.12f;
        [Tooltip("Extra targeted impulse multiplier for low leg/foot hits so they sweep the player.")]
        public float impactLowHitImpulseMultiplier = 1.8f;
        [Tooltip("Whole-body velocity share for low leg/foot hits. Lower means more sweep/torque and less center-mass launch.")]
        public float impactLowHitVelocityShare = 0.08f;
        [Tooltip("Maximum offset from a target body part center where targeted impact force can be applied.")]
        public float impactForcePointRadius = 0.55f;
        [Tooltip("Fraction of targeted impact impulse shared into nearby ragdoll body parts.")]
        public float impactNearbyImpulseShare = 0.18f;
        [Tooltip("Radius around the hit point where nearby ragdoll body parts receive shared impulse.")]
        public float impactNearbyImpulseRadius = 1.15f;
        [Tooltip("Seconds before impact ragdoll automatically starts recovery.")]
        public float impactAutoRecoveryDelay = 2f;
        [Tooltip("Cooldown between impact ragdoll triggers so repeated contacts do not spam-reset the state.")]
        public float impactCooldown = 0.6f;
        [Tooltip("If enabled, hard landings against static ground/surfaces can trigger the same impact ragdoll system as moving props.")]
        public bool impactFallRagdollEnabled = true;
        [Tooltip("Minimum landing collision speed needed to ragdoll from a fall.")]
        public float impactFallVelocityThreshold = 10f;
        [Tooltip("Effective source mass used for fall impacts because the ground has no rigidbody mass. Usually this should be near player/body mass.")]
        public float impactFallEffectiveMass = 70f;
        [Tooltip("Multiplier applied to landing speed before feeding it into impact severity and recovery.")]
        public float impactFallVelocityMultiplier = 1f;
        [Tooltip("Shortest possible recovery delay for a light physics impact.")]
        public float impactRecoveryMinDelay = 1.25f;
        [Tooltip("Longest possible recovery delay for a severe physics impact.")]
        public float impactRecoveryMaxDelay = 5.5f;
        [Tooltip("Impact speed treated as a severe recovery event before mass/impulse are considered.")]
        public float impactRecoverySeverityReferenceSpeed = 18f;
        [Tooltip("Source mass treated as a full-strength recovery severity contributor.")]
        public float impactRecoverySeverityMassReference = 70f;
        [Tooltip("Seconds the ragdoll must remain under stability thresholds before recovery starts.")]
        public float impactRecoveryStableHoldSeconds = 0.45f;
        [Tooltip("Maximum average ragdoll body speed allowed before recovery can start.")]
        public float impactRecoveryMaxAverageSpeed = 0.45f;
        [Tooltip("Maximum fastest single ragdoll body speed allowed before recovery can start.")]
        public float impactRecoveryMaxBodySpeed = 1.2f;
        [Tooltip("Maximum average ragdoll angular speed allowed before recovery can start.")]
        public float impactRecoveryMaxAngularSpeed = 1.2f;
        [Tooltip("Maximum center-of-mass drift speed allowed before recovery can start.")]
        public float impactRecoveryMaxComDriftSpeed = 0.35f;
        [Tooltip("Maximum distance below the ragdoll center of mass that still counts as stable ground support.")]
        public float impactRecoveryGroundProbeDistance = 2.5f;
        [Tooltip("Impact severity needed to trigger full knockout blackout instead of only a dark vignette.")]
        public float impactKnockoutSeverityThreshold = 0.82f;
        [Tooltip("If enabled, repeated impacts while ragdolled add together into one knockdown/knockout severity meter instead of only using the strongest single hit.")]
        public bool impactKnockoutAccumulationEnabled = true;
        [Tooltip("How much each new impact adds to the accumulated knockdown meter. 1 means a 0.2 severity hit adds 20 percent toward full knockout.")]
        public float impactKnockoutAccumulationGain = 1f;
        [Tooltip("Maximum normalized accumulated knockdown value. Keep at 1 for the normal 0-to-100 percent knockout scale.")]
        public float impactKnockoutAccumulationMax = 1f;
        [Tooltip("Minimum vignette opacity for light physics impacts.")]
        public float impactVignetteMinAlpha = 0.15f;
        [Tooltip("Maximum vignette opacity for hard non-knockout impacts.")]
        public float impactVignetteMaxAlpha = 0.72f;
        [Tooltip("Full-screen blackout opacity for knockout impacts.")]
        public float impactBlackoutAlpha = 1f;
        [Tooltip("Shortest full blackout duration after a knockout impact.")]
        public float impactBlackoutMinSeconds = 0.75f;
        [Tooltip("Longest full blackout duration after a maximum severity knockout impact.")]
        public float impactBlackoutMaxSeconds = 2.25f;
        [Tooltip("Seconds used to fade impact darkness away after recovery completes.")]
        public float impactOverlayFadeOutSeconds = 1.25f;
    }

    [Serializable]
    public sealed class PrototypeHazardTuning
    {
        [Header("Impact Source")]
        [Tooltip("Fallback impact speed threshold used by hazards when they are not using the ragdoll profile threshold.")]
        public float overrideImpactVelocityThreshold = 7.5f;
        [Tooltip("Per-hazard cooldown after a successful impact trigger.")]
        public float impactSourceCooldown = 0.25f;
        [Tooltip("Multiplier applied to collision relative velocity before impact calculations.")]
        public float impactVelocityScale = 1f;

        [Header("Swinging Hammer")]
        [Tooltip("Local hinge anchor used when autoAnchorHammerToTop is disabled.")]
        public Vector3 hammerLocalHingeAnchor = new(0f, 0.72f, 0f);
        [Tooltip("If enabled, the hammer hinge is placed at the top of its visible renderer bounds.")]
        public bool autoAnchorHammerToTop = true;
        [Tooltip("If enabled, the hammer spins continuously instead of reversing between swing limits.")]
        public bool hammerContinuousSpin = true;
        [Tooltip("Swing limit used only when hammerContinuousSpin is disabled.")]
        public float hammerSwingLimitDegrees = 68f;
        [Tooltip("Target motor speed for the hammer hinge.")]
        public float hammerMotorSpeed = 175f;
        [Tooltip("Hinge motor force. Higher values make the hammer harder for the player to slow or stop.")]
        public float hammerMotorForce = 12000f;
        [Tooltip("Hammer rigidbody mass. Higher mass makes impacts scale heavier.")]
        public float hammerMass = 240f;
        [Tooltip("Maximum angular velocity allowed on the hammer rigidbody.")]
        public float hammerMaxAngularVelocity = 50f;
        [Tooltip("Fallback cube scale used only if no hammer prefab is assigned.")]
        public Vector3 fallbackHammerScale = new(1.4f, 1.4f, 1.4f);

        [Header("Ball Shooter")]
        [Tooltip("Seconds after scene start before the shooter fires its first ball.")]
        public float ballInitialDelay = 0.75f;
        [Tooltip("Seconds between fired balls.")]
        public float ballFireInterval = 2.2f;
        [Tooltip("Initial ball speed.")]
        public float ballSpeed = 16f;
        [Tooltip("Mass of fired balls.")]
        public float ballMass = 2f;
        [Tooltip("Visual and collider scale of fired balls.")]
        public float ballScale = 0.42f;
        [Tooltip("Seconds before fired balls are destroyed.")]
        public float ballLifetime = 6f;

        [Header("Prototype Test Rig")]
        [Tooltip("World position where the hammer hazard is spawned in TestPrototype.")]
        public Vector3 testHammerPosition = new(-3.8f, 2.4f, 205f);
        [Tooltip("World position where the ball shooter is spawned in TestPrototype.")]
        public Vector3 testBallShooterPosition = new(5.5f, 1.15f, 201.5f);
        [Tooltip("World point the ball shooter aims at when spawned.")]
        public Vector3 testBallShooterTarget = new(0f, 1.15f, 205f);
    }

    [Serializable]
    public sealed class InteractionTuning
    {
        [Tooltip("Maximum distance for interact and ragdoll grab ray/sphere casts.")]
        public float interactRange = 4.5f;
        [Tooltip("Offset used when placing the player in hide spots.")]
        public float hideOffset = 0.5f;
        [Tooltip("Forward offset for held item placement.")]
        public float heldItemForwardOffset = 1.1f;
        [Tooltip("Upward offset for held item placement.")]
        public float heldItemUpOffset = 1.1f;
        [Tooltip("Distance in front of the player where dropped items are placed.")]
        public float dropDistance = 1.4f;
        [Tooltip("Camera-forward distance used by ragdoll grabbing.")]
        public float grabbedHoldDistance = 2.6f;
        [Tooltip("Forward velocity added when throwing a grabbed ragdoll/body.")]
        public float throwImpulse = 7.5f;
    }

    [Serializable]
    public sealed class ProjectileTuning
    {
        [Tooltip("Impulse speed for the manual B-key physics ball.")]
        public float speed = 30f;
        [Tooltip("Seconds before manual physics balls are destroyed.")]
        public float lifetime = 5f;
        [Tooltip("Scale of manual physics balls.")]
        public float scale = 0.25f;
        [Tooltip("Mass of manual physics balls.")]
        public float mass = 0.6f;
        [Tooltip("Spawn distance from the player camera for manual physics balls.")]
        public float spawnDistance = 0.7f;
    }

    [Serializable]
    public sealed class MonsterTuning
    {
        [Tooltip("Multiplier applied to monster patrol movement speed.")]
        public float patrolSpeedMultiplier = 0.55f;
        [Tooltip("Smoothing time for monster turning.")]
        public float turnSmoothing = 0.12f;
        [Tooltip("Distance where a monster considers a patrol point reached.")]
        public float patrolArrivalDistance = 1.2f;
        [Tooltip("Prototype throw velocity applied when a monster grabs/chucks the player. High values are intentionally goofy for testing.")]
        public float prototypeThrowSpeed = 42f;
        [Tooltip("Extra upward velocity applied during the prototype monster throw.")]
        public float prototypeThrowUpwardVelocity = 14f;
        [Tooltip("Point impulse applied at the player's body during the prototype monster throw.")]
        public float prototypeThrowImpulse = 85f;
        [Tooltip("Recovery/knockout severity used for the prototype monster throw.")]
        public float prototypeThrowSeverity = 1f;
    }

    [Serializable]
    public sealed class ObjectiveTuning
    {
        [Tooltip("Minimum quota value generated for a run.")]
        public int minQuotaValue = 12;
        [Tooltip("Maximum quota value generated for a run.")]
        public int maxQuotaValue = 20;
        [Tooltip("Night duration in seconds.")]
        public float nightLengthSeconds = 420f;
        [Tooltip("Salt added to objective generation so objective RNG differs from layout/item RNG.")]
        public int objectiveSeedSalt = 1777;
        [Tooltip("Minimum number of distinct objective entries.")]
        public int objectiveMinCount = 3;
        [Tooltip("Maximum number of distinct objective entries.")]
        public int objectiveMaxCount = 5;
        [Tooltip("Divisor used when converting quota value into objective item counts.")]
        public int objectiveQuotaDivisor = 4;
    }

    [Serializable]
    public sealed class SpawnTuning
    {
        [Tooltip("Minimum random seed used when no seed is specified.")]
        public int defaultSeedMin = 1000;
        [Tooltip("Maximum random seed used when no seed is specified.")]
        public int defaultSeedMax = 999999;
        [Tooltip("Step added while generating item spawn randomness.")]
        public int itemSeedStep = 17;
        [Tooltip("Maximum monster budget for the run.")]
        public int monsterBudget = 3;
    }

    [Serializable]
    public sealed class LayoutTuning
    {
        [Tooltip("Number of middle chunks generated between entrance and exit sections.")]
        public int middleChunkCount = 6;
        [Tooltip("World-space distance between generated chunks.")]
        public float chunkSpacing = 26f;
    }

    [Serializable]
    public sealed class DebugTuning
    {
        [Tooltip("If enabled, the runtime debug/tuning panel can be shown in play mode.")]
        public bool showRuntimeDebugPanel = true;
        [Tooltip("Key that toggles the runtime debug/tuning panel.")]
        public KeyCode toggleDebugPanelKey = KeyCode.F3;
        [Tooltip("If enabled, offline testing can cycle between balance profiles from the debug panel.")]
        public bool allowProfileSwitchingOffline = true;
    }
}

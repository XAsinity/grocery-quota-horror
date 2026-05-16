using System;
using UnityEngine;

namespace GroceryQuotaHorror.Data
{
    [CreateAssetMenu(menuName = "Grocery Quota Horror/Game Balance Profile", fileName = "GameBalanceProfile")]
    public sealed class GameBalanceProfile : ScriptableObject
    {
        [Header("Global Physics")]
        public GlobalPhysicsTuning globalPhysics = new();

        [Header("Player Movement")]
        public PlayerMovementTuning playerMovement = new();

        [Header("Player Camera")]
        public PlayerCameraTuning playerCamera = new();

        [Header("Player Body")]
        public PlayerBodyTuning playerBody = new();

        [Header("Ragdoll")]
        public RagdollTuning ragdoll = new();

        [Header("Interaction")]
        public InteractionTuning interaction = new();

        [Header("Projectile")]
        public ProjectileTuning projectile = new();

        [Header("Monster")]
        public MonsterTuning monster = new();

        [Header("Objectives")]
        public ObjectiveTuning objectives = new();

        [Header("Spawn")]
        public SpawnTuning spawn = new();

        [Header("Layout")]
        public LayoutTuning layout = new();

        [Header("Debug")]
        public DebugTuning debug = new();
    }

    [Serializable]
    public sealed class GlobalPhysicsTuning
    {
        public Vector3 worldGravity = new(0f, -9.81f, 0f);
    }

    [Serializable]
    public sealed class PlayerMovementTuning
    {
        public float moveSpeed = 4f;
        public float sprintMultiplier = 1.6f;
        public float playerGravity = -20f;
        public float lookSensitivity = 2.2f;
        public float maxLookPitch = 80f;
        public float mouseTurnRate = 120f;
        public float cameraHeight = 1.55f;
        public float movementSmoothing = 10f;
    }

    [Serializable]
    public sealed class PlayerCameraTuning
    {
        public Vector3 supportedLocalOffset = new(0f, 1.55f, 0f);
        public float limpFollowPositionSharpness = 10f;
        public float limpFollowRotationSharpness = 8f;
        public float recoveryBlendSeconds = 0.22f;
    }

    [Serializable]
    public sealed class PlayerBodyTuning
    {
        public float pelvisFollowForce = 120f;
        public float pelvisDamping = 12f;
        public float pelvisTorque = 14f;
        public float boneTorque = 8f;
        public float boneAngularDamping = 0.75f;
        public float supportedLinearDamping = 0.6f;
        public float supportedAngularDamping = 0.12f;
        public float downedLinearDamping = 0.18f;
        public float downedAngularDamping = 0.06f;
        public float downedDriveMultiplier = 0.38f;
        public float supportedDriveMultiplier = 1f;
        public float supportedBobAmplitude = 0.05f;
        public float supportedBobFrequency = 5.5f;
        public float supportedMoveLean = 10f;
        public float sprintLeanBonus = 6f;
        public float headPitchWeight = 0.5f;
        public float chestPitchWeight = 0.18f;
        public float armSwingDegrees = 16f;
        public float legStrideDegrees = 18f;
        public float armDragDegrees = 6f;
        public float airborneTuckDegrees = 10f;
        public float stumbleResponse = 0.04f;
    }

    [Serializable]
    public sealed class RagdollTuning
    {
        public float spawnDistance = 3f;
        public float spawnVerticalOffset = 0.2f;
        public float jointSwingLimit = 25f;
        public float jointTwistLimit = 20f;
        public float headLookTorque = 2.4f;
        public float headLiftForce = 0.65f;
        public float restoreHeightOffset = 0.15f;
        public float recoveryAssistForce = 24f;
        public float ragdollHoldForce = 90f;
        public float ragdollHoldDamping = 2.5f;
        public float ragdollHeldLinearDamping = 0.4f;
        public float ragdollHeldAngularDamping = 0.45f;
        public float ragdollHeldVerticalAssist = 0.7f;
        public float ragdollHeldMaxForce = 140f;
    }

    [Serializable]
    public sealed class InteractionTuning
    {
        public float interactRange = 3.2f;
        public float hideOffset = 0.5f;
        public float heldItemForwardOffset = 1.1f;
        public float heldItemUpOffset = 1.1f;
        public float dropDistance = 1.4f;
        public float grabbedHoldDistance = 2.6f;
        public float throwImpulse = 7.5f;
    }

    [Serializable]
    public sealed class ProjectileTuning
    {
        public float speed = 30f;
        public float lifetime = 5f;
        public float scale = 0.25f;
        public float mass = 0.6f;
        public float spawnDistance = 0.7f;
    }

    [Serializable]
    public sealed class MonsterTuning
    {
        public float patrolSpeedMultiplier = 0.55f;
        public float turnSmoothing = 0.12f;
        public float patrolArrivalDistance = 1.2f;
    }

    [Serializable]
    public sealed class ObjectiveTuning
    {
        public int minQuotaValue = 12;
        public int maxQuotaValue = 20;
        public float nightLengthSeconds = 420f;
        public int objectiveSeedSalt = 1777;
        public int objectiveMinCount = 3;
        public int objectiveMaxCount = 5;
        public int objectiveQuotaDivisor = 4;
    }

    [Serializable]
    public sealed class SpawnTuning
    {
        public int defaultSeedMin = 1000;
        public int defaultSeedMax = 999999;
        public int itemSeedStep = 17;
        public int monsterBudget = 3;
    }

    [Serializable]
    public sealed class LayoutTuning
    {
        public int middleChunkCount = 6;
        public float chunkSpacing = 26f;
    }

    [Serializable]
    public sealed class DebugTuning
    {
        public bool showRuntimeDebugPanel = true;
        public KeyCode toggleDebugPanelKey = KeyCode.F3;
        public bool allowProfileSwitchingOffline = true;
    }
}

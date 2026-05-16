using GroceryQuotaHorror.Physics;
using GroceryQuotaHorror.Player;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace GroceryQuotaHorror.Editor
{
    [InitializeOnLoad]
    public static class MainCharacterPlayerBootstrap
    {
        public const string MainCharacterPrefabPath = "Assets/CharacterREF/MainCharacter.prefab";
        public const string PlayerPrefabPath = "Assets/Prefabs/Gameplay/Player.prefab";
        private const string RagdollPrefabPath = "Assets/Prefabs/Gameplay/NewGameCharacterRagdoll.prefab";
        private const string CharacterModelPath = "Assets/CharacterREF/newgamecharacter.fbx";

        static MainCharacterPlayerBootstrap()
        {
            EditorApplication.delayCall += EnsurePlayerPrefab;
        }

        [MenuItem("Tools/Grocery Quota Horror/Rebuild Player From MainCharacter")]
        public static void EnsurePlayerPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
            if (modelAsset == null)
            {
                return;
            }

            var sourceRoot = PrefabUtility.LoadPrefabContents(MainCharacterPrefabPath);
            if (sourceRoot == null)
            {
                return;
            }

            var root = new GameObject("Player");
            root.name = "Player";
            var controller = EnsureComponent<CharacterController>(root);
            var networkObject = EnsureComponent<NetworkObject>(root);
            var networkTransform = EnsureComponent<Unity.Netcode.Components.NetworkTransform>(root);
            var player = EnsureComponent<PlayerController>(root);
            var activeRagdoll = EnsureComponent<ActiveRagdollController>(root);
            _ = networkObject;
            _ = networkTransform;

            controller = root.GetComponent<CharacterController>();
            player = root.GetComponent<PlayerController>();
            activeRagdoll = root.GetComponent<ActiveRagdollController>();
            if (controller == null || player == null || activeRagdoll == null)
            {
                Debug.LogError("Failed to build Player prefab because required components were not added to the root object.");
                Object.DestroyImmediate(root);
                PrefabUtility.UnloadPrefabContents(sourceRoot);
                return;
            }

            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);

            var modelInstance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
            if (modelInstance != null)
            {
                modelInstance.name = modelAsset.name;
                modelInstance.transform.SetParent(visualRoot.transform, false);
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;

                var animator = modelInstance.GetComponentInChildren<Animator>(true);
                if (animator != null)
                {
                    animator.applyRootMotion = false;
                    animator.runtimeAnimatorController = null;
                }
            }

            ConfigureCamera(sourceRoot, root);
            ConfigureCharacterController(modelInstance != null ? modelInstance.transform : visualRoot.transform, controller);
            ConfigurePlayerFields(modelInstance != null ? modelInstance.transform : visualRoot.transform, player, activeRagdoll);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            PrefabUtility.UnloadPrefabContents(sourceRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ConfigureCamera(GameObject sourceRoot, GameObject targetRoot)
        {
            var sourceCamera = sourceRoot.GetComponentInChildren<Camera>(true);
            if (sourceCamera == null)
            {
                return;
            }

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(targetRoot.transform, false);
            cameraObject.transform.localPosition = sourceCamera.transform.localPosition;
            cameraObject.transform.localRotation = sourceCamera.transform.localRotation;

            var camera = cameraObject.AddComponent<Camera>();
            EditorUtility.CopySerialized(sourceCamera, camera);
            camera.tag = "MainCamera";
            camera.enabled = true;

            var sourceListener = sourceCamera.GetComponent<AudioListener>();
            if (sourceListener != null)
            {
                var listener = cameraObject.AddComponent<AudioListener>();
                EditorUtility.CopySerialized(sourceListener, listener);
                listener.enabled = true;
            }
        }

        private static void ConfigureCharacterController(Transform root, CharacterController controller)
        {
            if (!TryGetRendererBounds(root, out var bounds))
            {
                controller.height = 1.8f;
                controller.radius = 0.35f;
                controller.center = new Vector3(0f, 0.9f, 0f);
                return;
            }

            controller.height = Mathf.Max(1.5f, bounds.size.y * 0.92f);
            controller.radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.38f, 0.22f, 0.45f);
            var localCenter = root.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y + controller.height * 0.5f, bounds.center.z));
            controller.center = new Vector3(localCenter.x, localCenter.y, localCenter.z);
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.08f;
            controller.minMoveDistance = 0.001f;
        }

        private static void ConfigurePlayerFields(Transform modelRoot, PlayerController player, ActiveRagdollController activeRagdoll)
        {
            var serializedPlayer = new SerializedObject(player);
            serializedPlayer.FindProperty("flashlight").objectReferenceValue = null;
            serializedPlayer.FindProperty("activeRagdoll").objectReferenceValue = activeRagdoll;

            var renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            var rendererProperty = serializedPlayer.FindProperty("bodyRenderers");
            rendererProperty.arraySize = renderers.Length;
            for (var i = 0; i < renderers.Length; i++)
            {
                rendererProperty.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            }

            var ragdollPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RagdollPrefabPath);
            if (ragdollPrefab != null)
            {
                serializedPlayer.FindProperty("spawnableRagdollPrefab").objectReferenceValue = ragdollPrefab.GetComponent<SpawnableRagdoll>();
            }

            serializedPlayer.ApplyModifiedPropertiesWithoutUndo();

            var serializedRagdoll = new SerializedObject(activeRagdoll);
            serializedRagdoll.FindProperty("modelRoot").objectReferenceValue = modelRoot;
            serializedRagdoll.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
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

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component == null)
            {
                component = target.AddComponent<T>();
            }

            return component;
        }
    }
}

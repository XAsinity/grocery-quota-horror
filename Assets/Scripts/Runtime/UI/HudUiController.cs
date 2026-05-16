using GroceryQuotaHorror.Bootstrap;
using GroceryQuotaHorror.Core;
using GroceryQuotaHorror.Data;
using UnityEngine;

namespace GroceryQuotaHorror.UI
{
    public sealed class HudUiController : MonoBehaviour
    {
        private bool showDebugPanel;
        private int profileIndex;

        private void OnGUI()
        {
            if (NightGameManager.Instance == null)
            {
                DrawDebugPanel();
                return;
            }

            GUILayout.BeginArea(new Rect(16f, 16f, 360f, 240f), GUI.skin.window);
            GUILayout.Label($"Time: {NightGameManager.Instance.TimeRemaining:0}");
            GUILayout.Label($"Run: {NightGameManager.Instance.CurrentResult}");
            GUILayout.Label(NightGameManager.Instance.QuotaMet ? "Extraction unlocked" : "Quota not met");
            GUILayout.Space(6f);
            GUILayout.Label("Shopping list");

            var entries = NightGameManager.Instance.ObjectiveEntries;
            for (var i = 0; i < entries.Count; i++)
            {
                GUILayout.Label($"- {entries[i].displayName}: {entries[i].depositedCount}/{entries[i].requiredCount}");
            }

            GUILayout.Space(6f);
            GUILayout.Label("Controls: WASD move, Shift sprint, E interact, F flashlight, G drop, R limp, B physics ball");
            GUILayout.EndArea();
            DrawDebugPanel();
        }

        private void Update()
        {
            if (GameRuntime.Balance == null)
            {
                return;
            }

            if (Input.GetKeyDown(GameRuntime.Balance.debug.toggleDebugPanelKey))
            {
                showDebugPanel = !showDebugPanel;
            }
        }

        private void DrawDebugPanel()
        {
            if (!showDebugPanel || GameRuntime.Balance == null || !GameRuntime.Balance.debug.showRuntimeDebugPanel)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(390f, 16f, 360f, 320f), GUI.skin.window);
            GUILayout.Label($"Profile: {GameRuntime.Balance.name}");
            GUILayout.Label($"Move Speed: {GameRuntime.Balance.playerMovement.moveSpeed:0.00}");
            GUILayout.Label($"Sprint Mult: {GameRuntime.Balance.playerMovement.sprintMultiplier:0.00}");
            GUILayout.Label($"Pelvis Force: {GameRuntime.Balance.playerBody.pelvisFollowForce:0.0}");
            GUILayout.Label($"Bone Torque: {GameRuntime.Balance.playerBody.boneTorque:0.0}");
            GUILayout.Label($"Hold Force: {GameRuntime.Balance.ragdoll.ragdollHoldForce:0.0}");
            GUILayout.Label($"Gravity: {GameRuntime.Balance.globalPhysics.worldGravity.y:0.00}");

            GameRuntime.Balance.playerMovement.moveSpeed = GUILayout.HorizontalSlider(GameRuntime.Balance.playerMovement.moveSpeed, 1f, 12f);
            GameRuntime.Balance.playerBody.pelvisFollowForce = GUILayout.HorizontalSlider(GameRuntime.Balance.playerBody.pelvisFollowForce, 30f, 220f);
            GameRuntime.Balance.playerBody.boneTorque = GUILayout.HorizontalSlider(GameRuntime.Balance.playerBody.boneTorque, 1f, 24f);
            GameRuntime.Balance.ragdoll.ragdollHoldForce = GUILayout.HorizontalSlider(GameRuntime.Balance.ragdoll.ragdollHoldForce, 10f, 180f);
            GameRuntime.Balance.globalPhysics.worldGravity.y = GUILayout.HorizontalSlider(GameRuntime.Balance.globalPhysics.worldGravity.y, -30f, -1f);

            if (GUILayout.Button("Apply Runtime Values"))
            {
                GameRuntime.NotifySettingsChanged();
            }

            var profiles = GameRuntime.AllProfiles;
            if (profiles.Length > 0 && GameRuntime.Balance.debug.allowProfileSwitchingOffline && NetworkBootstrap.LocalOfflineMode)
            {
                if (GUILayout.Button("Next Profile"))
                {
                    profileIndex = (profileIndex + 1) % profiles.Length;
                    GameRuntime.TrySetBalanceProfileByName(profiles[profileIndex].name);
                }
            }

            GUILayout.EndArea();
        }
    }
}

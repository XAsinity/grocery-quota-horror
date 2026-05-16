using GroceryQuotaHorror.Core;
using UnityEngine;

namespace GroceryQuotaHorror.UI
{
    public sealed class HudUiController : MonoBehaviour
    {
        private void OnGUI()
        {
            if (NightGameManager.Instance == null)
            {
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
            GUILayout.Label("Controls: WASD move, Shift sprint, E interact, F flashlight, G drop");
            GUILayout.EndArea();
        }
    }
}


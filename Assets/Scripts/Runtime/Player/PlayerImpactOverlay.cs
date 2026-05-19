using GroceryQuotaHorror.Data;
using UnityEngine;

namespace GroceryQuotaHorror.Player
{
    public sealed class PlayerImpactOverlay : MonoBehaviour
    {
        private Texture2D vignetteTexture;
        private Texture2D blackTexture;
        private float vignetteAlpha;
        private float targetVignetteAlpha;
        private float blackoutAlpha;
        private float blackoutUntil;
        private float fadeOutSeconds = 1.25f;
        private bool clearing;

        public void ShowImpact(float severity01, RagdollTuning tuning)
        {
            if (tuning == null)
            {
                return;
            }

            EnsureTextures();
            severity01 = Mathf.Clamp01(severity01);
            fadeOutSeconds = Mathf.Max(0.05f, tuning.impactOverlayFadeOutSeconds);
            targetVignetteAlpha = Mathf.Max(targetVignetteAlpha, Mathf.Lerp(tuning.impactVignetteMinAlpha, tuning.impactVignetteMaxAlpha, severity01));
            vignetteAlpha = Mathf.Max(vignetteAlpha, targetVignetteAlpha);
            clearing = false;

            if (severity01 < tuning.impactKnockoutSeverityThreshold)
            {
                return;
            }

            var knockout01 = Mathf.InverseLerp(tuning.impactKnockoutSeverityThreshold, 1f, severity01);
            blackoutAlpha = Mathf.Max(blackoutAlpha, tuning.impactBlackoutAlpha);
            blackoutUntil = Mathf.Max(
                blackoutUntil,
                Time.time + Mathf.Lerp(tuning.impactBlackoutMinSeconds, tuning.impactBlackoutMaxSeconds, knockout01));
        }

        public void Clear(float fadeSeconds)
        {
            fadeOutSeconds = Mathf.Max(0.05f, fadeSeconds);
            targetVignetteAlpha = 0f;
            clearing = true;
        }

        private void Update()
        {
            if (blackoutAlpha > 0f && Time.time >= blackoutUntil)
            {
                blackoutAlpha = Mathf.MoveTowards(blackoutAlpha, 0f, Time.deltaTime / fadeOutSeconds);
            }

            var fadeRate = Time.deltaTime / fadeOutSeconds;
            vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, targetVignetteAlpha, fadeRate);
            if (clearing && vignetteAlpha <= 0.001f && blackoutAlpha <= 0.001f)
            {
                clearing = false;
                vignetteAlpha = 0f;
                blackoutAlpha = 0f;
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || vignetteAlpha <= 0f && blackoutAlpha <= 0f)
            {
                return;
            }

            EnsureTextures();
            var screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
            if (vignetteAlpha > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, vignetteAlpha);
                GUI.DrawTexture(screenRect, vignetteTexture, ScaleMode.StretchToFill, true);
            }

            if (blackoutAlpha > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, blackoutAlpha);
                GUI.DrawTexture(screenRect, blackTexture);
            }

            GUI.color = Color.white;
        }

        private void EnsureTextures()
        {
            if (blackTexture == null)
            {
                blackTexture = Texture2D.whiteTexture;
            }

            if (vignetteTexture != null)
            {
                return;
            }

            const int size = 128;
            vignetteTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            var maxDistance = center.magnitude;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance01 = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                    var alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.34f, 1f, distance01));
                    vignetteTexture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
                }
            }

            vignetteTexture.Apply(false, true);
        }
    }
}

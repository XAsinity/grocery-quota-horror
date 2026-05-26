using System;
using GroceryQuotaHorror.Data;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GroceryQuotaHorror.Physics
{
    public sealed class PhysicsImpactAudio : MonoBehaviour
    {
        private const int ClipCount = 5;
        private const int SampleRate = 44100;
        private const float SharedPointGridSize = 1.5f;

        private static AudioClip[] impactClips;
        private static readonly float[] SharedPointTimes = new float[64];
        private static readonly int[] SharedPointKeys = new int[64];
        private static int sharedPointCursor;
        private static int activeVoices;

        private AudioSource audioSource;
        private Rigidbody body;
        private float nextImpactTime;
        private int playingVoiceCount;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.priority = 96;
            EnsureClips();
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryPlayImpact(collision);
        }

        private void TryPlayImpact(Collision collision)
        {
            var tuning = GameRuntime.Balance != null ? GameRuntime.Balance.impactAudio : null;
            if (tuning == null || !tuning.enabled || Time.time < nextImpactTime)
            {
                return;
            }

            var speed = collision.relativeVelocity.magnitude;
            if (speed < tuning.minVelocity)
            {
                return;
            }

            if (activeVoices >= Mathf.Max(1, tuning.maxSimultaneousVoices))
            {
                return;
            }

            var contact = collision.contactCount > 0 ? collision.GetContact(0) : default;
            var normalVelocity = collision.contactCount > 0
                ? Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal))
                : speed;
            var normalRatio = normalVelocity / Mathf.Max(0.01f, speed);
            if (normalVelocity < tuning.minNormalVelocity || normalRatio < tuning.minNormalVelocityRatio)
            {
                return;
            }

            if (IsSharedPointCoolingDown(contact, tuning.sharedPointCooldown))
            {
                return;
            }

            nextImpactTime = Time.time + Mathf.Max(0.01f, tuning.perObjectCooldown);
            var impact01 = Mathf.InverseLerp(tuning.minVelocity, Mathf.Max(tuning.minVelocity + 0.01f, tuning.hardVelocity), normalVelocity);
            var mass = body != null ? body.mass : 1f;
            var massScale = 1f + Mathf.Log10(Mathf.Max(1f, mass)) * Mathf.Max(0f, tuning.massVolumeScale);
            var volume = Mathf.Lerp(tuning.minVolume, tuning.maxVolume, impact01) * massScale;
            var pitch = Mathf.Lerp(1.18f, 0.74f, impact01) + Random.Range(-tuning.pitchRandomness, tuning.pitchRandomness);
            var clipIndex = Mathf.Clamp(Mathf.FloorToInt(impact01 * ClipCount), 0, ClipCount - 1);
            audioSource.maxDistance = Mathf.Max(1f, tuning.maxDistance);
            audioSource.pitch = Mathf.Clamp(pitch, 0.45f, 1.55f);
            audioSource.PlayOneShot(impactClips[clipIndex], Mathf.Clamp01(volume));
            activeVoices++;
            playingVoiceCount++;
            StartCoroutine(ReleaseVoiceAfter(impactClips[clipIndex].length / Mathf.Max(0.1f, audioSource.pitch)));
        }

        private void OnDestroy()
        {
            if (playingVoiceCount <= 0)
            {
                return;
            }

            activeVoices = Mathf.Max(0, activeVoices - playingVoiceCount);
            playingVoiceCount = 0;
        }

        private static bool IsSharedPointCoolingDown(ContactPoint contact, float cooldown)
        {
            return IsSharedPointCoolingDown(contact.point, cooldown);
        }

        private System.Collections.IEnumerator ReleaseVoiceAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            playingVoiceCount = Mathf.Max(0, playingVoiceCount - 1);
            activeVoices = Mathf.Max(0, activeVoices - 1);
        }

        private static bool IsSharedPointCoolingDown(Vector3 point, float cooldown)
        {
            var key = HashPoint(point);
            for (var i = 0; i < SharedPointKeys.Length; i++)
            {
                if (SharedPointKeys[i] == key && Time.time - SharedPointTimes[i] < cooldown)
                {
                    return true;
                }
            }

            SharedPointKeys[sharedPointCursor] = key;
            SharedPointTimes[sharedPointCursor] = Time.time;
            sharedPointCursor = (sharedPointCursor + 1) % SharedPointKeys.Length;
            return false;
        }

        private static int HashPoint(Vector3 point)
        {
            var x = Mathf.RoundToInt(point.x / SharedPointGridSize);
            var y = Mathf.RoundToInt(point.y / SharedPointGridSize);
            var z = Mathf.RoundToInt(point.z / SharedPointGridSize);
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + x;
                hash = hash * 31 + y;
                hash = hash * 31 + z;
                return hash;
            }
        }

        private static void EnsureClips()
        {
            if (impactClips != null)
            {
                return;
            }

            impactClips = new AudioClip[ClipCount];
            for (var i = 0; i < impactClips.Length; i++)
            {
                impactClips[i] = CreateImpactClip(i);
            }
        }

        private static AudioClip CreateImpactClip(int index)
        {
            var duration = Mathf.Lerp(0.11f, 0.24f, index / (float)(ClipCount - 1));
            var sampleCount = Mathf.CeilToInt(duration * SampleRate);
            var samples = new float[sampleCount];
            var lowFrequency = Mathf.Lerp(120f, 54f, index / (float)(ClipCount - 1));
            var clickFrequency = Mathf.Lerp(1150f, 520f, index / (float)(ClipCount - 1));
            var noiseState = 0.113f + index * 0.217f;

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)SampleRate;
                var age01 = i / (float)Math.Max(1, sampleCount - 1);
                var envelope = Mathf.Exp(-age01 * Mathf.Lerp(12f, 7f, index / (float)(ClipCount - 1)));
                var body = Mathf.Sin(2f * Mathf.PI * lowFrequency * t) * Mathf.Lerp(0.42f, 0.72f, index / (float)(ClipCount - 1));
                noiseState = Mathf.Repeat(noiseState * 3.781f + 0.137f, 1f);
                var noise = (noiseState * 2f - 1f) * Mathf.Exp(-age01 * 22f);
                var click = Mathf.Sin(2f * Mathf.PI * clickFrequency * t) * Mathf.Exp(-age01 * 34f);
                samples[i] = Mathf.Clamp((body + noise * 0.28f + click * 0.16f) * envelope, -1f, 1f);
            }

            var clip = AudioClip.Create($"ProceduralImpactThud{index}", sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}

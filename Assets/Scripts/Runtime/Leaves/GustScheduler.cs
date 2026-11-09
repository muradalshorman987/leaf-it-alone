using System;
using UnityEngine;

namespace FallenYard.Leaves
{
    /// <summary>Tuning for the yard's breeze and gusts.</summary>
    [Serializable]
    public class GustSettings
    {
        [Header("Breeze")]
        [Tooltip("Prevailing wind direction in degrees (0 = towards +Z, 90 = towards +X).")]
        public float prevailingDirection = 30f;
        [Tooltip("Everyday wind (m/s). Below most leaf thresholds: only maple leaves drift.")]
        [Min(0f)] public float breezeStrength = 0.7f;
        [Tooltip("How much the breeze strength wobbles, as a fraction of it.")]
        [Range(0f, 1f)] public float breezeVariation = 0.35f;
        [Tooltip("How far (degrees) the breeze direction wanders around the prevailing direction.")]
        [Range(0f, 90f)] public float directionWander = 20f;

        [Header("Gusts")]
        public bool gustsEnabled = true;
        [Tooltip("Seconds before the first gust of a yard.")]
        [Min(0f)] public float firstGustDelay = 25f;
        [Tooltip("Seconds between the end of one gust and the start of the next (min, max).")]
        public Vector2 gustInterval = new Vector2(30f, 60f);
        [Tooltip("How long a gust lasts in seconds (min, max).")]
        public Vector2 gustDuration = new Vector2(2.5f, 4.5f);
        [Tooltip("Peak gust strength in m/s (min, max). Above ~3 the top of piles scatters.")]
        public Vector2 gustStrength = new Vector2(3f, 5.5f);
        [Tooltip("Max angle (degrees) a gust's direction differs from the prevailing direction.")]
        [Range(0f, 180f)] public float gustSpread = 40f;
    }

    /// <summary>Wind at one moment.</summary>
    public struct WindSample
    {
        /// <summary>Unit direction on the ground plane (x = world X, y = world Z).</summary>
        public Vector2 direction;
        /// <summary>m/s.</summary>
        public float strength;
        public bool gustActive;

        public Vector3 Direction3 => new Vector3(direction.x, 0f, direction.y);
    }

    /// <summary>
    /// Deterministic breeze + gust timeline. Plain C# so it can be tested without a scene;
    /// <see cref="WindSystem"/> drives it each frame.
    /// </summary>
    public class GustScheduler
    {
        readonly GustSettings _settings;
        readonly System.Random _rng;

        public float Time { get; private set; }
        public bool GustActive { get; private set; }
        /// <summary>0..1 through the current gust.</summary>
        public float GustProgress => GustActive ? Mathf.Clamp01((Time - _gustStart) / _gustDuration) : 0f;
        public Vector2 GustDirection { get; private set; }
        public float GustPeakStrength { get; private set; }
        /// <summary>Scheduler time the next gust starts (meaningless while one is active).</summary>
        public float NextGustTime { get; private set; }
        public int GustsStarted { get; private set; }

        float _gustStart, _gustDuration;
        readonly float _phase;

        public GustScheduler(GustSettings settings, int seed)
        {
            _settings = settings ?? new GustSettings();
            _rng = new System.Random(seed);
            _phase = (float)_rng.NextDouble() * 100f;
            NextGustTime = _settings.firstGustDelay;
        }

        /// <summary>Advances time. Returns true when a gust starts during this tick.</summary>
        public bool Tick(float dt)
        {
            Time += Mathf.Max(0f, dt);
            if (GustActive && Time >= _gustStart + _gustDuration)
            {
                GustActive = false;
                NextGustTime = Time + Range(_settings.gustInterval);
            }
            if (!GustActive && _settings.gustsEnabled && Time >= NextGustTime)
            {
                StartGust(null, null);
                return true;
            }
            return false;
        }

        /// <summary>Starts a gust now. Null direction/strength pick random ones from the settings.</summary>
        public void StartGust(Vector2? direction, float? strength)
        {
            float spread = ((float)_rng.NextDouble() * 2f - 1f) * _settings.gustSpread;
            GustDirection = direction.HasValue && direction.Value.sqrMagnitude > 1e-8f
                ? direction.Value.normalized
                : DirectionFromDegrees(_settings.prevailingDirection + spread);
            GustPeakStrength = strength ?? Range(_settings.gustStrength);
            _gustDuration = Mathf.Max(0.1f, Range(_settings.gustDuration));
            _gustStart = Time;
            GustActive = true;
            GustsStarted++;
        }

        public WindSample Sample()
        {
            float t = Time + _phase;
            float wobble = Mathf.Sin(t * 0.37f) * 0.6f + Mathf.Sin(t * 1.13f) * 0.4f;
            float breeze = Mathf.Max(0f, _settings.breezeStrength * (1f + _settings.breezeVariation * wobble));
            float wander = Mathf.Sin(t * 0.05f) * 0.7f + Mathf.Sin(t * 0.13f) * 0.3f;
            Vector2 breezeDir = DirectionFromDegrees(_settings.prevailingDirection + wander * _settings.directionWander);

            if (!GustActive)
                return new WindSample { direction = breezeDir, strength = breeze };

            float env = Envelope(GustProgress);
            Vector2 dir = Vector2.Lerp(breezeDir, GustDirection, env);
            if (dir.sqrMagnitude < 1e-6f) dir = GustDirection;
            return new WindSample
            {
                direction = dir.normalized,
                strength = Mathf.Lerp(breeze, Mathf.Max(breeze, GustPeakStrength), env),
                gustActive = true,
            };
        }

        /// <summary>Gust shape over its life: quick rise (first 20%), hold, gentle fall (last 40%).</summary>
        public static float Envelope(float t01)
        {
            t01 = Mathf.Clamp01(t01);
            if (t01 < 0.2f) return Mathf.SmoothStep(0f, 1f, t01 / 0.2f);
            if (t01 > 0.6f) return 1f - Mathf.SmoothStep(0f, 1f, (t01 - 0.6f) / 0.4f);
            return 1f;
        }

        public static Vector2 DirectionFromDegrees(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(r), Mathf.Cos(r));
        }

        float Range(Vector2 minMax)
        {
            float lo = Mathf.Min(minMax.x, minMax.y), hi = Mathf.Max(minMax.x, minMax.y);
            return lo + (float)_rng.NextDouble() * (hi - lo);
        }
    }
}

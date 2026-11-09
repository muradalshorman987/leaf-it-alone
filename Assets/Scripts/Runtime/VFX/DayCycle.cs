using System;
using UnityEngine;

namespace FallenYard.VFX
{
    /// <summary>Tuning for the time of day.</summary>
    [Serializable]
    public class DayCycleSettings
    {
        [Tooltip("Hour the yard starts at (0..24). Mid-afternoon reads best for autumn colours.")]
        [Range(0f, 24f)] public float startHour = 14.5f;
        [Tooltip("Real minutes for a full 24 game hours. 0 freezes the clock.")]
        [Min(0f)] public float dayLengthMinutes = 30f;
        [Range(0f, 12f)] public float sunriseHour = 6.5f;
        [Range(12f, 24f)] public float sunsetHour = 18.5f;
        [Tooltip("Sun height at midday, degrees above the horizon. Low autumn sun = long shadows.")]
        [Range(5f, 89f)] public float maxSunElevation = 48f;
        [Tooltip("Moon height at midnight, degrees. The same light is used for the moon.")]
        [Range(5f, 89f)] public float maxMoonElevation = 35f;
        [Tooltip("Rotates the whole sun path around the yard (degrees).")]
        public float azimuthOffset = -35f;
        [Tooltip("Lowest the light goes, so shadows never stretch to infinity at dawn and dusk.")]
        [Range(1f, 20f)] public float minElevation = 6f;
    }

    /// <summary>
    /// The game clock. Plain C# so it can be tested without a scene;
    /// <see cref="LightingRig"/> ticks it and turns the hour into a light.
    /// </summary>
    public class DayCycle
    {
        readonly DayCycleSettings _settings;

        public DayCycleSettings Settings => _settings;
        /// <summary>0..24.</summary>
        public float Hour { get; private set; }
        public bool Paused { get; set; }
        /// <summary>Whole days passed since the cycle started.</summary>
        public int DaysElapsed { get; private set; }

        public DayCycle(DayCycleSettings settings)
        {
            _settings = settings ?? new DayCycleSettings();
            Hour = Wrap(_settings.startHour);
        }

        /// <summary>Game hours per real second.</summary>
        public float HoursPerSecond => _settings.dayLengthMinutes <= 0f ? 0f : 24f / (_settings.dayLengthMinutes * 60f);

        public void Tick(float dt)
        {
            if (Paused || dt <= 0f) return;
            float h = Hour + dt * HoursPerSecond;
            while (h >= 24f) { h -= 24f; DaysElapsed++; }
            Hour = h;
        }

        public void SetHour(float hour) => Hour = Wrap(hour);

        public bool IsDay => SunHeight(Hour, _settings.sunriseHour, _settings.sunsetHour) > 0f;

        /// <summary>0 at night, 1 at midday, smooth through dawn and dusk.</summary>
        public float Daylight01 => Daylight(Hour, _settings.sunriseHour, _settings.sunsetHour);

        /// <summary>Rotation for the directional light (sun by day, moon by night).</summary>
        public Quaternion LightRotation => LightRotationAt(Hour, _settings);

        public static float Wrap(float hour)
        {
            if (float.IsNaN(hour) || float.IsInfinity(hour)) return 0f;
            float h = hour % 24f;
            return h < 0f ? h + 24f : h;
        }

        /// <summary>
        /// +1 at solar noon, 0 at sunrise/sunset, -1 at the middle of the night.
        /// Day and night are each a half sine, stretched to their own length.
        /// </summary>
        public static float SunHeight(float hour, float sunrise, float sunset)
        {
            hour = Wrap(hour);
            float dayLength = Mathf.Max(0.01f, sunset - sunrise);
            if (hour >= sunrise && hour <= sunset)
                return Mathf.Sin(Mathf.PI * (hour - sunrise) / dayLength);
            float nightLength = Mathf.Max(0.01f, 24f - dayLength);
            float intoNight = hour > sunset ? hour - sunset : hour + 24f - sunset;
            return -Mathf.Sin(Mathf.PI * intoNight / nightLength);
        }

        /// <summary>0..1 brightness factor: 0 an hour past sunset, 1 once the sun is well up.</summary>
        public static float Daylight(float hour, float sunrise, float sunset)
        {
            float h = SunHeight(hour, sunrise, sunset);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.15f, 0.35f, h));
        }

        /// <summary>
        /// Light rotation: the sun travels east to west by day; at night the same light
        /// becomes the moon on the opposite side, so there is always one shadow caster.
        /// </summary>
        public static Quaternion LightRotationAt(float hour, DayCycleSettings s)
        {
            hour = Wrap(hour);
            float height = SunHeight(hour, s.sunriseHour, s.sunsetHour);
            float dayLength = Mathf.Max(0.01f, s.sunsetHour - s.sunriseHour);
            float progress; // 0..1 across the current day or night
            float elevation;
            float azimuth;
            if (height >= 0f)
            {
                progress = Mathf.Clamp01((hour - s.sunriseHour) / dayLength);
                elevation = Mathf.Max(s.minElevation, height * s.maxSunElevation);
                azimuth = 90f + 180f * progress; // where the sun is: east -> south -> west
            }
            else
            {
                float nightLength = Mathf.Max(0.01f, 24f - dayLength);
                float intoNight = hour > s.sunsetHour ? hour - s.sunsetHour : hour + 24f - s.sunsetHour;
                progress = Mathf.Clamp01(intoNight / nightLength);
                elevation = Mathf.Max(s.minElevation, -height * s.maxMoonElevation);
                azimuth = 90f + 180f * progress; // the moon rises in the east too
            }
            // Light shines along its forward: pitch down by elevation, yaw so it travels from the sun's side.
            return Quaternion.Euler(elevation, azimuth + 180f + s.azimuthOffset, 0f);
        }
    }
}

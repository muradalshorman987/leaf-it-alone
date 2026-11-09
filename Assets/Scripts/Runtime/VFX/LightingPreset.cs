using UnityEngine;

namespace FallenYard.VFX
{
    /// <summary>Everything the lighting rig sets for one moment.</summary>
    public struct LightingState
    {
        public Color sunColor;
        public float sunIntensity;
        /// <summary>0..1 shadow darkness.</summary>
        public float shadowStrength;
        public Color skyAmbient;
        public Color equatorAmbient;
        public Color groundAmbient;
        public Color fogColor;
        public float fogDensity;
    }

    /// <summary>
    /// Light rig colours over a 24 hour day plus how weather changes them.
    /// One asset per mood (e.g. one per yard); <see cref="CreateDefault"/> builds the
    /// cozy autumn look used when none is assigned.
    /// </summary>
    [CreateAssetMenu(menuName = "Fallen Yard/Lighting Preset", fileName = "LightingPreset")]
    public class LightingPreset : ScriptableObject
    {
        [Header("Sun / moon (gradients run midnight -> midnight)")]
        public Gradient sunColor = new Gradient();
        [Tooltip("Light intensity by hour (0..24).")]
        public AnimationCurve sunIntensity = new AnimationCurve();
        [Range(0f, 1f)] public float shadowStrength = 0.85f;

        [Header("Ambient (trilight)")]
        public Gradient skyAmbient = new Gradient();
        public Gradient equatorAmbient = new Gradient();
        public Gradient groundAmbient = new Gradient();

        [Header("Fog")]
        public Gradient fogColor = new Gradient();
        [Tooltip("Exponential-squared fog density by hour (0..24). Morning mist reads well.")]
        public AnimationCurve fogDensity = new AnimationCurve();

        [Header("Weather")]
        [Tooltip("How much full cloud dims the sun.")]
        [Range(0f, 1f)] public float cloudSunDim = 0.6f;
        [Tooltip("How much full cloud greys the sun and ambient.")]
        [Range(0f, 1f)] public float cloudDesaturate = 0.55f;
        [Tooltip("Ambient boost under cloud (light scattered by the clouds).")]
        [Range(0f, 1f)] public float cloudAmbientLift = 0.15f;
        [Tooltip("How much a gust flash dims the sun (a cloud racing past).")]
        [Range(0f, 1f)] public float gustDim = 0.2f;

        public bool IsConfigured => sunIntensity != null && sunIntensity.length > 0 &&
                                    sunColor != null && sunColor.colorKeys.Length > 1;

        public static LightingPreset CreateDefault()
        {
            var p = CreateInstance<LightingPreset>();
            p.name = "Autumn (default)";
            p.ApplyAutumnDefaults();
            return p;
        }

        void Reset() => ApplyAutumnDefaults();

        /// <summary>Warm, low autumn sun; pink dawn, orange dusk, blue moonlit night.</summary>
        public void ApplyAutumnDefaults()
        {
            Color night = new Color(0.55f, 0.63f, 0.88f);
            sunColor = Grad(
                (0f, night), (6f, new Color(1f, 0.62f, 0.45f)), (8f, new Color(1f, 0.86f, 0.70f)),
                (12f, new Color(1f, 0.94f, 0.84f)), (15.5f, new Color(1f, 0.83f, 0.60f)),
                (18.3f, new Color(1f, 0.52f, 0.30f)), (20f, night), (24f, night));
            sunIntensity = new AnimationCurve(
                new Keyframe(0f, 0.18f), new Keyframe(5.5f, 0.15f), new Keyframe(6.6f, 0.35f),
                new Keyframe(9f, 1.0f), new Keyframe(13f, 1.25f), new Keyframe(16.5f, 1.05f),
                new Keyframe(18.4f, 0.35f), new Keyframe(19.5f, 0.15f), new Keyframe(24f, 0.18f));

            Color skyNight = new Color(0.16f, 0.20f, 0.33f);
            skyAmbient = Grad((0f, skyNight), (6.5f, new Color(0.62f, 0.52f, 0.55f)), (9f, new Color(0.75f, 0.78f, 0.85f)),
                (16f, new Color(0.78f, 0.74f, 0.72f)), (18.5f, new Color(0.62f, 0.48f, 0.45f)), (20f, skyNight), (24f, skyNight));
            Color eqNight = new Color(0.12f, 0.13f, 0.20f);
            equatorAmbient = Grad((0f, eqNight), (6.5f, new Color(0.52f, 0.42f, 0.40f)), (9f, new Color(0.62f, 0.55f, 0.45f)),
                (18.5f, new Color(0.58f, 0.40f, 0.30f)), (20f, eqNight), (24f, eqNight));
            Color groundNight = new Color(0.05f, 0.05f, 0.07f);
            groundAmbient = Grad((0f, groundNight), (7f, new Color(0.25f, 0.20f, 0.17f)), (12f, new Color(0.30f, 0.25f, 0.20f)),
                (18.5f, new Color(0.24f, 0.17f, 0.12f)), (20f, groundNight), (24f, groundNight));

            Color fogNight = new Color(0.11f, 0.13f, 0.21f);
            fogColor = Grad((0f, fogNight), (6.5f, new Color(0.85f, 0.70f, 0.66f)), (10f, new Color(0.84f, 0.78f, 0.68f)),
                (16f, new Color(0.86f, 0.76f, 0.62f)), (18.5f, new Color(0.90f, 0.60f, 0.45f)), (20f, fogNight), (24f, fogNight));
            fogDensity = new AnimationCurve(
                new Keyframe(0f, 0.016f), new Keyframe(5.5f, 0.022f), new Keyframe(7.5f, 0.030f),
                new Keyframe(11f, 0.008f), new Keyframe(17f, 0.009f), new Keyframe(19.5f, 0.014f), new Keyframe(24f, 0.016f));
        }

        /// <summary>The look at an hour (0..24) under some weather.</summary>
        public LightingState Evaluate(float hour, WeatherSample weather)
        {
            if (!IsConfigured) ApplyAutumnDefaults();
            hour = DayCycle.Wrap(hour);
            float t = hour / 24f;
            float cloud = Mathf.Clamp01(weather.cloudCover);
            float flash = Mathf.Clamp01(weather.gustFlash);

            var s = new LightingState
            {
                sunColor = sunColor.Evaluate(t),
                sunIntensity = Mathf.Max(0f, sunIntensity.Evaluate(hour)),
                shadowStrength = shadowStrength,
                skyAmbient = skyAmbient.Evaluate(t),
                equatorAmbient = equatorAmbient.Evaluate(t),
                groundAmbient = groundAmbient.Evaluate(t),
                fogColor = fogColor.Evaluate(t),
                fogDensity = Mathf.Max(0f, fogDensity.Evaluate(hour)),
            };

            float desat = cloud * cloudDesaturate;
            s.sunIntensity *= Mathf.Clamp01(1f - cloud * cloudSunDim - flash * gustDim);
            s.sunColor = Desaturate(s.sunColor, desat);
            s.shadowStrength *= Mathf.Lerp(1f, 0.45f, cloud);
            float lift = 1f + cloud * cloudAmbientLift;
            s.skyAmbient = Desaturate(s.skyAmbient, desat) * lift;
            s.equatorAmbient = Desaturate(s.equatorAmbient, desat) * lift;
            s.groundAmbient = Desaturate(s.groundAmbient, desat * 0.5f);
            s.fogColor = Desaturate(s.fogColor, desat);
            s.fogDensity *= Mathf.Max(0f, weather.fogMultiplier);
            return s;
        }

        public static Color Desaturate(Color c, float amount)
        {
            float l = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
            var grey = new Color(l, l, l, c.a);
            return Color.Lerp(c, grey, Mathf.Clamp01(amount));
        }

        /// <summary>Gradient from (hour, colour) keys. Unity gradients hold at most 8 colour keys.</summary>
        static Gradient Grad(params (float hour, Color color)[] keys)
        {
            int n = Mathf.Min(8, keys.Length);
            var colors = new GradientColorKey[n];
            for (int i = 0; i < n; i++) colors[i] = new GradientColorKey(keys[i].color, Mathf.Clamp01(keys[i].hour / 24f));
            var g = new Gradient();
            g.SetKeys(colors, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }
    }
}

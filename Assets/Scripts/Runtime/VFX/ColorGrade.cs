using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallenYard.VFX
{
    /// <summary>
    /// Post-processing values the game drives (URP Color Adjustments, White Balance,
    /// Vignette and Bloom). Plain data so grading logic is testable without URP.
    /// </summary>
    [Serializable]
    public struct ColorGrade
    {
        [Tooltip("EV stops.")] public float postExposure;
        [Range(-100f, 100f)] public float contrast;
        [Range(-100f, 100f)] public float saturation;
        [Tooltip("White balance: negative = cooler, positive = warmer.")]
        [Range(-100f, 100f)] public float temperature;
        [Tooltip("White balance: negative = greener, positive = magenta.")]
        [Range(-100f, 100f)] public float tint;
        public Color colorFilter;
        [Range(0f, 1f)] public float vignette;
        [Min(0f)] public float bloom;

        public static ColorGrade Neutral => new ColorGrade { colorFilter = Color.white };

        /// <summary>A yard full of leaves: a little muted and hazy, heavier vignette.</summary>
        public static ColorGrade DefaultMessy => new ColorGrade
        {
            postExposure = 0f, contrast = 6f, saturation = -10f, temperature = 6f, tint = 0f,
            colorFilter = new Color(1f, 0.97f, 0.92f), vignette = 0.28f, bloom = 0.25f,
        };

        /// <summary>A restored yard: warmer, richer, a touch of glow.</summary>
        public static ColorGrade DefaultRestored => new ColorGrade
        {
            postExposure = 0.1f, contrast = 12f, saturation = 14f, temperature = 10f, tint = -3f,
            colorFilter = Color.white, vignette = 0.18f, bloom = 0.45f,
        };

        public static ColorGrade Lerp(ColorGrade a, ColorGrade b, float t)
        {
            t = Mathf.Clamp01(t);
            return new ColorGrade
            {
                postExposure = Mathf.Lerp(a.postExposure, b.postExposure, t),
                contrast = Mathf.Lerp(a.contrast, b.contrast, t),
                saturation = Mathf.Lerp(a.saturation, b.saturation, t),
                temperature = Mathf.Lerp(a.temperature, b.temperature, t),
                tint = Mathf.Lerp(a.tint, b.tint, t),
                colorFilter = Color.Lerp(a.colorFilter, b.colorFilter, t),
                vignette = Mathf.Lerp(a.vignette, b.vignette, t),
                bloom = Mathf.Lerp(a.bloom, b.bloom, t),
            };
        }

        /// <summary>
        /// Layers the moment on top of a yard's base grade: clouds mute and cool it,
        /// night cools and darkens it, a gust flash dips exposure, and the
        /// yard-complete celebration adds a warm glow.
        /// </summary>
        public static ColorGrade Compose(ColorGrade baseGrade, WeatherSample weather, float daylight01, float celebrate01)
        {
            float cloud = Mathf.Clamp01(weather.cloudCover);
            float night = 1f - Mathf.Clamp01(daylight01);
            float flash = Mathf.Clamp01(weather.gustFlash);
            float party = Mathf.Clamp01(celebrate01);

            var g = baseGrade;
            g.saturation += -22f * cloud - 18f * night + 12f * party;
            g.contrast += -8f * cloud;
            g.temperature += -10f * cloud - 22f * night + 8f * party;
            g.postExposure += -0.15f * flash - 0.1f * night + 0.25f * party;
            g.vignette += 0.06f * cloud + 0.08f * night;
            g.bloom += 0.6f * party + 0.15f * night;
            return Clamp(g);
        }

        public static ColorGrade Clamp(ColorGrade g)
        {
            g.contrast = Mathf.Clamp(g.contrast, -100f, 100f);
            g.saturation = Mathf.Clamp(g.saturation, -100f, 100f);
            g.temperature = Mathf.Clamp(g.temperature, -100f, 100f);
            g.tint = Mathf.Clamp(g.tint, -100f, 100f);
            g.vignette = Mathf.Clamp01(g.vignette);
            g.bloom = Mathf.Max(0f, g.bloom);
            return g;
        }
    }

    /// <summary>Messy and restored grade for one yard.</summary>
    [Serializable]
    public class YardGrade
    {
        [Tooltip("Must match YardProgress.yardId.")]
        public string yardId = "yard_01";
        public ColorGrade messy = ColorGrade.DefaultMessy;
        public ColorGrade restored = ColorGrade.DefaultRestored;
    }

    /// <summary>Per-yard autumn colour grading. Yards not listed use the defaults.</summary>
    [CreateAssetMenu(menuName = "Fallen Yard/Yard Grade Table", fileName = "YardGradeTable")]
    public class YardGradeTable : ScriptableObject
    {
        public ColorGrade defaultMessy = ColorGrade.DefaultMessy;
        public ColorGrade defaultRestored = ColorGrade.DefaultRestored;
        public List<YardGrade> yards = new List<YardGrade>();

        /// <summary>The grade for a yard at a restoration level 0..1.</summary>
        public ColorGrade Evaluate(string yardId, float restored01)
        {
            Get(yardId, out var messy, out var restored);
            return ColorGrade.Lerp(messy, restored, restored01);
        }

        public void Get(string yardId, out ColorGrade messy, out ColorGrade restored)
        {
            if (!string.IsNullOrEmpty(yardId) && yards != null)
                foreach (var y in yards)
                    if (y != null && y.yardId == yardId)
                    {
                        messy = y.messy;
                        restored = y.restored;
                        return;
                    }
            messy = defaultMessy;
            restored = defaultRestored;
        }
    }

    /// <summary>
    /// The yard "restoration" transition: blends how clean the yard is (YardCleanChanged)
    /// with how restored it is (YardRestorationChanged) into one smoothly moving 0..1 value,
    /// plus a short celebration pulse when the yard is completed.
    /// </summary>
    public class RestorationTransition
    {
        /// <summary>Share of the look that comes from clearing leaves (the rest from fertilizer).</summary>
        public float cleanWeight = 0.4f;
        /// <summary>Seconds to cover ~63% of the way to the target.</summary>
        public float smoothingSeconds = 2.5f;
        public float celebrateSeconds = 4f;

        public float Clean { get; private set; }
        public float Restoration { get; private set; }
        public float Value { get; private set; }
        public float Celebrate { get; private set; }

        public float Target => Mathf.Clamp01(cleanWeight * Clean + (1f - cleanWeight) * Restoration);

        public void SetClean(float clean01) => Clean = Mathf.Clamp01(clean01);
        public void SetRestoration(float restoration01) => Restoration = Mathf.Clamp01(restoration01);
        public void StartCelebration() => Celebrate = 1f;

        /// <summary>Jumps straight to the target (loading a yard).</summary>
        public void Snap() => Value = Target;

        public void Tick(float dt)
        {
            dt = Mathf.Max(0f, dt);
            float k = smoothingSeconds <= 0f ? 1f : 1f - Mathf.Exp(-dt / smoothingSeconds);
            Value = Mathf.Lerp(Value, Target, k);
            Celebrate = celebrateSeconds <= 0f ? 0f : Mathf.Max(0f, Celebrate - dt / celebrateSeconds);
        }
    }
}

using System;
using UnityEngine;

namespace FallenYard.VFX
{
    public enum WeatherKind
    {
        Clear,
        Breezy,
        Overcast,
        Blustery,
    }

    /// <summary>What one kind of weather does to the look and to the wind.</summary>
    [Serializable]
    public struct WeatherProfile
    {
        public WeatherKind kind;
        [Tooltip("0 = blue sky, 1 = full cloud. Dims and greys the sun, softens shadows.")]
        [Range(0f, 1f)] public float cloudCover;
        [Tooltip("Multiplies the lighting preset's fog density.")]
        [Min(0f)] public float fogMultiplier;
        [Tooltip("Multiplies the breeze strength (WindSystem).")]
        [Min(0f)] public float windMultiplier;
        [Tooltip("Multiplies the time between gusts. Below 1 = more gusts.")]
        [Min(0.1f)] public float gustIntervalMultiplier;
        [Tooltip("Multiplies gust peak strength.")]
        [Min(0f)] public float gustStrengthMultiplier;
        [Tooltip("How many loose leaves drift through the air, 0..1.")]
        [Range(0f, 1f)] public float airborneLeaves;
        [Tooltip("Relative chance of being picked as the next weather.")]
        [Min(0f)] public float weight;

        public static WeatherProfile Default(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Breezy:
                    return new WeatherProfile { kind = kind, cloudCover = 0.2f, fogMultiplier = 0.8f, windMultiplier = 1.35f, gustIntervalMultiplier = 0.85f, gustStrengthMultiplier = 1.05f, airborneLeaves = 0.5f, weight = 2f };
                case WeatherKind.Overcast:
                    return new WeatherProfile { kind = kind, cloudCover = 0.75f, fogMultiplier = 1.8f, windMultiplier = 0.9f, gustIntervalMultiplier = 1.1f, gustStrengthMultiplier = 1f, airborneLeaves = 0.2f, weight = 2f };
                case WeatherKind.Blustery:
                    return new WeatherProfile { kind = kind, cloudCover = 0.5f, fogMultiplier = 1.3f, windMultiplier = 1.6f, gustIntervalMultiplier = 0.6f, gustStrengthMultiplier = 1.2f, airborneLeaves = 0.9f, weight = 1f };
                default:
                    return new WeatherProfile { kind = WeatherKind.Clear, cloudCover = 0.05f, fogMultiplier = 1f, windMultiplier = 1f, gustIntervalMultiplier = 1.15f, gustStrengthMultiplier = 0.95f, airborneLeaves = 0.15f, weight = 3f };
            }
        }
    }

    [Serializable]
    public class WeatherSettings
    {
        [Tooltip("Change weather over time. Off = stay on Start Weather.")]
        public bool cycleWeather = true;
        public WeatherKind startWeather = WeatherKind.Clear;
        [Tooltip("How long one spell of weather lasts, seconds (min, max).")]
        public Vector2 spellDuration = new Vector2(150f, 300f);
        [Tooltip("Seconds to blend from one weather to the next.")]
        [Min(0f)] public float transitionSeconds = 20f;
        [Tooltip("Seconds for the gust 'flash' (darker sky, more flying leaves) to fade.")]
        [Min(0.05f)] public float gustFlashSeconds = 2.5f;
        [Tooltip("Gust strength (m/s) that gives a full gust flash.")]
        [Min(0.1f)] public float gustFlashStrength = 6f;
        public WeatherProfile[] profiles =
        {
            WeatherProfile.Default(WeatherKind.Clear),
            WeatherProfile.Default(WeatherKind.Breezy),
            WeatherProfile.Default(WeatherKind.Overcast),
            WeatherProfile.Default(WeatherKind.Blustery),
        };

        public WeatherProfile Profile(WeatherKind kind)
        {
            if (profiles != null)
                foreach (var p in profiles)
                    if (p.kind == kind) return p;
            return WeatherProfile.Default(kind);
        }
    }

    /// <summary>Blended weather at one moment, what lighting, post and particles read.</summary>
    public struct WeatherSample
    {
        public float cloudCover;
        public float fogMultiplier;
        public float windMultiplier;
        public float gustIntervalMultiplier;
        public float gustStrengthMultiplier;
        public float airborneLeaves;
        /// <summary>0..1, spikes when a gust starts and fades over a couple of seconds.</summary>
        public float gustFlash;

        /// <summary>No weather at all: what lighting uses when there is no WeatherSystem.</summary>
        public static WeatherSample Calm => new WeatherSample
        {
            fogMultiplier = 1f, windMultiplier = 1f, gustIntervalMultiplier = 1f, gustStrengthMultiplier = 1f,
        };

        public static WeatherSample From(WeatherProfile p) => new WeatherSample
        {
            cloudCover = p.cloudCover,
            fogMultiplier = p.fogMultiplier,
            windMultiplier = p.windMultiplier,
            gustIntervalMultiplier = p.gustIntervalMultiplier,
            gustStrengthMultiplier = p.gustStrengthMultiplier,
            airborneLeaves = p.airborneLeaves,
        };

        public static WeatherSample Lerp(WeatherSample a, WeatherSample b, float t)
        {
            t = Mathf.Clamp01(t);
            return new WeatherSample
            {
                cloudCover = Mathf.Lerp(a.cloudCover, b.cloudCover, t),
                fogMultiplier = Mathf.Lerp(a.fogMultiplier, b.fogMultiplier, t),
                windMultiplier = Mathf.Lerp(a.windMultiplier, b.windMultiplier, t),
                gustIntervalMultiplier = Mathf.Lerp(a.gustIntervalMultiplier, b.gustIntervalMultiplier, t),
                gustStrengthMultiplier = Mathf.Lerp(a.gustStrengthMultiplier, b.gustStrengthMultiplier, t),
                airborneLeaves = Mathf.Lerp(a.airborneLeaves, b.airborneLeaves, t),
                gustFlash = Mathf.Lerp(a.gustFlash, b.gustFlash, t),
            };
        }
    }

    /// <summary>
    /// Deterministic weather timeline: spells of one weather kind that blend into the next,
    /// plus a short "flash" whenever a wind gust starts. Plain C# for tests;
    /// <see cref="WeatherSystem"/> drives it and applies it to the wind.
    /// </summary>
    public class WeatherModel
    {
        readonly WeatherSettings _settings;
        readonly System.Random _rng;

        public WeatherKind Current { get; private set; }
        public WeatherKind Previous { get; private set; }
        /// <summary>0..1 from Previous to Current.</summary>
        public float Blend { get; private set; } = 1f;
        public float TimeInSpell { get; private set; }
        public float SpellLength { get; private set; }
        public float GustFlash { get; private set; }

        public WeatherModel(WeatherSettings settings, int seed)
        {
            _settings = settings ?? new WeatherSettings();
            _rng = new System.Random(seed);
            Current = Previous = _settings.startWeather;
            SpellLength = NextSpellLength();
        }

        /// <summary>Advances time. Returns true when a new weather spell starts.</summary>
        public bool Tick(float dt)
        {
            dt = Mathf.Max(0f, dt);
            TimeInSpell += dt;
            Blend = _settings.transitionSeconds <= 0f ? 1f : Mathf.Clamp01(Blend + dt / _settings.transitionSeconds);
            GustFlash = Mathf.Max(0f, GustFlash - dt / _settings.gustFlashSeconds);

            if (_settings.cycleWeather && TimeInSpell >= SpellLength)
            {
                SetWeather(PickNext(), false);
                return true;
            }
            return false;
        }

        /// <summary>Switches weather now. Immediate skips the blend.</summary>
        public void SetWeather(WeatherKind kind, bool immediate)
        {
            // Start the new blend from wherever we are now, so interrupting a transition doesn't pop.
            Previous = Blend >= 0.5f ? Current : Previous;
            Current = kind;
            Blend = immediate ? 1f : 0f;
            TimeInSpell = 0f;
            SpellLength = NextSpellLength();
        }

        /// <summary>A gust started (from <c>GameEvents.WindGust</c>).</summary>
        public void OnGust(float strength)
        {
            float flash = Mathf.Clamp01(strength / _settings.gustFlashStrength);
            GustFlash = Mathf.Max(GustFlash, flash);
        }

        public WeatherSample Sample()
        {
            float t = Mathf.SmoothStep(0f, 1f, Blend);
            var s = WeatherSample.Lerp(
                WeatherSample.From(_settings.Profile(Previous)),
                WeatherSample.From(_settings.Profile(Current)), t);
            s.gustFlash = GustFlash;
            return s;
        }

        public static string Id(WeatherKind kind) => kind.ToString().ToLowerInvariant();

        WeatherKind PickNext()
        {
            float total = 0f;
            foreach (WeatherKind k in Enum.GetValues(typeof(WeatherKind)))
                if (k != Current) total += Mathf.Max(0f, _settings.Profile(k).weight);
            if (total <= 0f) return Current;

            float r = (float)_rng.NextDouble() * total;
            foreach (WeatherKind k in Enum.GetValues(typeof(WeatherKind)))
            {
                if (k == Current) continue;
                float w = Mathf.Max(0f, _settings.Profile(k).weight);
                if (w <= 0f) continue;
                if (r < w) return k;
                r -= w;
            }
            return Current == WeatherKind.Clear ? WeatherKind.Breezy : WeatherKind.Clear;
        }

        float NextSpellLength()
        {
            float lo = Mathf.Min(_settings.spellDuration.x, _settings.spellDuration.y);
            float hi = Mathf.Max(_settings.spellDuration.x, _settings.spellDuration.y);
            return Mathf.Max(1f, lo + (float)_rng.NextDouble() * (hi - lo));
        }
    }
}

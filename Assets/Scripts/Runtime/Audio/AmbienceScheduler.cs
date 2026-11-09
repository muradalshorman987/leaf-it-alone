using UnityEngine;

namespace FallenYard.Audio
{
    /// <summary>
    /// Decides when a bird sings. Calls are spaced randomly between a min and max delay,
    /// and go quiet for a while after a wind gust (birds hunker down). Seeded, so tests
    /// and replays are repeatable.
    /// </summary>
    public sealed class AmbienceScheduler
    {
        readonly SynthRandom _rng;
        float _min, _max;
        float _timer;
        float _suppressed;

        public AmbienceScheduler(int seed, float minDelay = 4f, float maxDelay = 12f)
        {
            _rng = new SynthRandom(seed);
            SetRange(minDelay, maxDelay);
            _timer = NextDelay();
        }

        public bool Enabled { get; set; } = true;

        /// <summary>How chatty the birds are (1 = normal, 0.5 = half as often, 0 = silent). Set from the weather.</summary>
        public float Rate { get; set; } = 1f;

        /// <summary>Seconds until the next call (ignoring suppression).</summary>
        public float TimeToNext => _timer;
        public float SuppressedFor => _suppressed;

        public void SetRange(float minDelay, float maxDelay)
        {
            _min = Mathf.Max(0.1f, minDelay);
            _max = Mathf.Max(_min, maxDelay);
        }

        public float NextDelay() => _rng.Range(_min, _max);

        /// <summary>Silences calls for at least <paramref name="seconds"/>.</summary>
        public void Suppress(float seconds) => _suppressed = Mathf.Max(_suppressed, seconds);

        /// <summary>Advances time. Returns true when a bird should sing this tick.</summary>
        public bool Tick(float dt)
        {
            if (dt <= 0f) return false;
            if (_suppressed > 0f)
            {
                _suppressed = Mathf.Max(0f, _suppressed - dt);
                return false;
            }
            if (!Enabled || Rate <= 0f) return false;
            _timer -= dt * Rate;
            if (_timer > 0f) return false;
            _timer = NextDelay();
            return true;
        }

        /// <summary>Random variant index and stereo pan for a call.</summary>
        public void PickCall(int variants, out int variant, out float pan, out float pitch)
        {
            variant = _rng.Range(0, Mathf.Max(1, variants));
            pan = _rng.Range(-0.8f, 0.8f);
            pitch = _rng.Range(0.9f, 1.12f);
        }
    }

    /// <summary>
    /// How the weather (<see cref="FallenYard.Core.GameEvents.WeatherChanged"/>, WP10) colours the
    /// ambience: windier weather raises the wind bed and sends the birds into cover.
    /// </summary>
    public static class WeatherAmbience
    {
        /// <param name="weatherId">"clear", "breezy", "overcast" or "blustery". Unknown ids sound like clear.</param>
        /// <param name="windGain">Multiplier for the wind bed level.</param>
        /// <param name="birdRate">Multiplier for how often birds sing.</param>
        public static void For(string weatherId, out float windGain, out float birdRate)
        {
            switch (weatherId)
            {
                case "breezy": windGain = 1.35f; birdRate = 0.8f; break;
                case "overcast": windGain = 1.1f; birdRate = 0.45f; break;
                case "blustery": windGain = 1.8f; birdRate = 0.15f; break;
                default: windGain = 1f; birdRate = 1f; break;
            }
        }
    }
}

using System;

namespace FallenYard.Audio
{
    /// <summary>
    /// Deterministic random numbers for sound generation (xorshift32). Same seed, same sound,
    /// on every machine, so generated clips are stable between runs and testable.
    /// </summary>
    public sealed class SynthRandom
    {
        uint _state;

        public SynthRandom(int seed)
        {
            _state = (uint)seed * 747796405u + 2891336453u;
            if (_state == 0) _state = 0x9E3779B9u;
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>0 (inclusive) .. 1 (exclusive).</summary>
        public float Next01() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>-1 .. 1.</summary>
        public float NextSigned() => Next01() * 2f - 1f;

        public float Range(float min, float max) => min + (max - min) * Next01();

        /// <summary>min (inclusive) .. max (exclusive).</summary>
        public int Range(int min, int max) => max <= min ? min : min + (int)(NextUInt() % (uint)(max - min));
    }

    /// <summary>
    /// Topology-preserving state variable filter (Zavalishin). Stable at any cutoff, so it
    /// can be swept per sample for wind and blower sounds.
    /// </summary>
    public sealed class SvFilter
    {
        readonly int _sampleRate;
        float _ic1, _ic2;
        float _a1, _a2, _a3, _k;

        public float Low { get; private set; }
        public float Band { get; private set; }
        public float High { get; private set; }

        public SvFilter(int sampleRate, float cutoff, float q = 0.707f)
        {
            _sampleRate = sampleRate;
            Set(cutoff, q);
        }

        public void Set(float cutoff, float q)
        {
            float nyquistSafe = _sampleRate * 0.49f;
            if (cutoff < 10f) cutoff = 10f;
            if (cutoff > nyquistSafe) cutoff = nyquistSafe;
            if (q < 0.05f) q = 0.05f;
            float g = (float)Math.Tan(Math.PI * cutoff / _sampleRate);
            _k = 1f / q;
            _a1 = 1f / (1f + g * (g + _k));
            _a2 = g * _a1;
            _a3 = g * _a2;
        }

        public void Process(float x)
        {
            float v3 = x - _ic2;
            float v1 = _a1 * _ic1 + _a2 * v3;
            float v2 = _ic2 + _a2 * _ic1 + _a3 * v3;
            _ic1 = 2f * v1 - _ic1;
            _ic2 = 2f * v2 - _ic2;
            Low = v2;
            Band = v1;
            High = x - _k * v1 - v2;
        }
    }

    /// <summary>Small helpers shared by the procedural sound recipes.</summary>
    public static class Synth
    {
        public const float TwoPi = (float)(Math.PI * 2.0);

        public static int Samples(float seconds, int sampleRate) => Math.Max(1, (int)Math.Round(seconds * sampleRate));

        public static float Sin(double phase) => (float)Math.Sin(phase);

        public static float MidiToHz(float midi) => 440f * (float)Math.Pow(2.0, (midi - 69f) / 12.0);

        /// <summary>Exponential decay that falls to about -60 dB after <paramref name="t60"/> seconds.</summary>
        public static float Decay(float t, float t60) => t60 <= 0f ? 0f : (float)Math.Exp(-6.9078f * t / t60);

        /// <summary>Linear attack then exponential decay.</summary>
        public static float AttackDecay(float t, float attack, float t60)
        {
            if (t < 0f) return 0f;
            if (t < attack) return attack <= 0f ? 1f : t / attack;
            return Decay(t - attack, t60);
        }

        /// <summary>Smooth 0..1..0 hump over [0, length], peaking at <paramref name="peakAt"/> (0..1).</summary>
        public static float Swell(float t, float length, float peakAt = 0.4f)
        {
            if (t <= 0f || t >= length || length <= 0f) return 0f;
            float x = t / length;
            float y = x < peakAt ? x / peakAt : 1f - (x - peakAt) / (1f - peakAt);
            return y * y * (3f - 2f * y);
        }

        /// <summary>
        /// Sine oscillator that rotates a phasor instead of calling Math.Sin every sample
        /// (about 10x faster, and drift is negligible over a few seconds in double precision).
        /// </summary>
        public struct Osc
        {
            double _re, _im, _cr, _ci;

            public Osc(float hz, int sampleRate, double phase = 0)
            {
                double w = 2.0 * Math.PI * hz / sampleRate;
                _cr = Math.Cos(w);
                _ci = Math.Sin(w);
                _re = Math.Cos(phase);
                _im = Math.Sin(phase);
            }

            /// <summary>Returns the current sample and advances one step.</summary>
            public float Next()
            {
                float v = (float)_im;
                double re = _re * _cr - _im * _ci;
                _im = _re * _ci + _im * _cr;
                _re = re;
                return v;
            }
        }

        /// <summary>Per-sample multiplier that makes a level fall by 60 dB over <paramref name="t60"/> seconds.</summary>
        public static double DecayFactor(float t60, int sampleRate) => t60 <= 0f ? 0.0 : Math.Exp(-6.9078 / (t60 * sampleRate));

        static int Index(int start, int i, int length, bool wrap)
        {
            int idx = start + i;
            if (wrap) return ((idx % length) + length) % length;
            return idx < length ? idx : -1;
        }

        /// <summary>Adds a sine bell (fundamental plus inharmonic partials) into the buffer.</summary>
        public static void AddBell(float[] buf, int sampleRate, float startSeconds, float hz, float amp, float t60, bool wrap = false)
        {
            float[] ratios = { 1f, 2.0f, 2.76f, 5.4f };
            float[] gains = { 1f, 0.35f, 0.22f, 0.08f };
            var osc = new Osc[ratios.Length];
            var env = new double[ratios.Length];
            var fall = new double[ratios.Length];
            for (int p = 0; p < ratios.Length; p++)
            {
                osc[p] = new Osc(hz * ratios[p], sampleRate);
                env[p] = gains[p];
                // Higher partials die away faster, which is what makes it sound like a bell.
                fall[p] = DecayFactor(t60 / (1f + p * 1.5f), sampleRate);
            }
            int start = (int)(startSeconds * sampleRate);
            int len = Samples(t60 * 1.1f, sampleRate);
            int attack = Samples(0.003f, sampleRate);
            for (int i = 0; i < len; i++)
            {
                float s = 0f;
                for (int p = 0; p < ratios.Length; p++)
                {
                    s += (float)env[p] * osc[p].Next();
                    env[p] *= fall[p];
                }
                int idx = Index(start, i, buf.Length, wrap);
                if (idx < 0) break;
                buf[idx] += amp * (i < attack ? (float)i / attack : 1f) * s;
            }
        }

        /// <summary>Adds a soft plucked tone (a few decaying harmonics) into the buffer.</summary>
        public static void AddPluck(float[] buf, int sampleRate, float startSeconds, float hz, float amp, float t60, bool wrap = false)
        {
            var o1 = new Osc(hz, sampleRate);
            var o2 = new Osc(hz * 2f, sampleRate);
            var o3 = new Osc(hz * 3f, sampleRate);
            double e1 = 1.0, e2 = 0.30, e3 = 0.12;
            double f1 = DecayFactor(t60, sampleRate), f2 = DecayFactor(t60 * 0.4f, sampleRate), f3 = DecayFactor(t60 * 0.2f, sampleRate);
            int start = (int)(startSeconds * sampleRate);
            int len = Samples(t60, sampleRate);
            int attack = Samples(0.004f, sampleRate);
            for (int i = 0; i < len; i++)
            {
                float s = (float)e1 * o1.Next() + (float)(e1 * e2) * o2.Next() + (float)(e1 * e3) * o3.Next();
                e1 *= f1;
                e2 *= f2 / f1;
                e3 *= f3 / f1;
                int idx = Index(start, i, buf.Length, wrap);
                if (idx < 0) break;
                buf[idx] += amp * (i < attack ? (float)i / attack : 1f) * s;
            }
        }

        /// <summary>Adds a slow-attack pad note (two slightly detuned sines plus a soft octave).</summary>
        public static void AddPad(float[] buf, int sampleRate, float startSeconds, float lengthSeconds, float hz, float amp,
            float attack = 0.8f, float release = 1.2f, bool wrap = false)
        {
            var o1 = new Osc(hz, sampleRate);
            var o2 = new Osc(hz * 1.0025f, sampleRate, 1.0);
            var o3 = new Osc(hz * 2f, sampleRate, 0.5);
            int start = (int)(startSeconds * sampleRate);
            int hold = Samples(lengthSeconds, sampleRate);
            int att = Math.Max(1, Samples(attack, sampleRate));
            int rel = Math.Max(1, Samples(release, sampleRate));
            int len = hold + rel;
            for (int i = 0; i < len; i++)
            {
                float env = i < att ? (float)i / att : 1f;
                if (i > hold) env *= Math.Max(0f, 1f - (float)(i - hold) / rel);
                env *= env;
                float s = 0.5f * o1.Next() + 0.5f * o2.Next() + 0.18f * o3.Next();
                int idx = Index(start, i, buf.Length, wrap);
                if (idx < 0) break;
                buf[idx] += amp * env * s;
            }
        }

        /// <summary>Scales the buffer so its loudest sample is <paramref name="peak"/>. Silent buffers are left alone.</summary>
        public static void Normalize(float[] buf, float peak)
        {
            float max = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float a = Math.Abs(buf[i]);
                if (a > max) max = a;
            }
            if (max < 1e-6f) return;
            float k = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= k;
        }

        /// <summary>Short linear fades at both ends so one-shots never click.</summary>
        public static void FadeEdges(float[] buf, int sampleRate, float fadeInSeconds = 0.002f, float fadeOutSeconds = 0.02f)
        {
            int fi = Math.Min(buf.Length, Samples(fadeInSeconds, sampleRate));
            int fo = Math.Min(buf.Length, Samples(fadeOutSeconds, sampleRate));
            for (int i = 0; i < fi; i++) buf[i] *= (float)i / fi;
            for (int i = 0; i < fo; i++) buf[buf.Length - 1 - i] *= (float)i / fo;
        }

        /// <summary>
        /// Turns a buffer into a seamless loop by cross-fading its last <paramref name="fadeSamples"/>
        /// into its start (equal power). The result is <c>src.Length - fadeSamples</c> long.
        /// </summary>
        public static float[] MakeSeamlessLoop(float[] src, int fadeSamples)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            fadeSamples = Math.Max(1, Math.Min(fadeSamples, src.Length / 2));
            int n = src.Length - fadeSamples;
            var dst = new float[n];
            Array.Copy(src, dst, n);
            for (int i = 0; i < fadeSamples; i++)
            {
                double w = (double)i / fadeSamples * Math.PI * 0.5;
                dst[i] = src[i] * (float)Math.Sin(w) + src[n + i] * (float)Math.Cos(w);
            }
            return dst;
        }

        public static float Rms(float[] buf)
        {
            if (buf == null || buf.Length == 0) return 0f;
            double sum = 0;
            for (int i = 0; i < buf.Length; i++) sum += buf[i] * buf[i];
            return (float)Math.Sqrt(sum / buf.Length);
        }

        public static float Peak(float[] buf)
        {
            float max = 0f;
            if (buf == null) return 0f;
            for (int i = 0; i < buf.Length; i++) max = Math.Max(max, Math.Abs(buf[i]));
            return max;
        }
    }
}

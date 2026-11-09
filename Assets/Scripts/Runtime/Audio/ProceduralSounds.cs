using System;

namespace FallenYard.Audio
{
    /// <summary>Mono PCM samples (-1..1) produced by a recipe, before they become an AudioClip.</summary>
    public sealed class SoundBuffer
    {
        public readonly float[] samples;
        public readonly int sampleRate;
        public readonly bool loop;

        public SoundBuffer(float[] samples, int sampleRate, bool loop)
        {
            this.samples = samples;
            this.sampleRate = sampleRate;
            this.loop = loop;
        }

        public float Seconds => sampleRate > 0 ? (float)samples.Length / sampleRate : 0f;
    }

    /// <summary>
    /// Recipes for every <see cref="SoundCue"/>, synthesized from noise, filters and sine
    /// oscillators. Nothing is sampled or downloaded, so every sound is original to this
    /// project (see docs/AUDIO.md). Pure C#: no Unity calls, so recipes run on any thread
    /// and in EditMode tests.
    /// </summary>
    public static class ProceduralSounds
    {
        /// <summary>Sample rate for short, bright sounds.</summary>
        public const int SfxRate = 44100;
        /// <summary>Sample rate for long, soft beds and music (keeps memory and build time down).</summary>
        public const int BedRate = 22050;

        /// <summary>How many different takes a cue has. Callers pick one at random to avoid repetition.</summary>
        public static int VariantCount(SoundCue cue)
        {
            switch (cue)
            {
                case SoundCue.BagRustle: return 3;
                case SoundCue.WindGust: return 2;
                case SoundCue.Birdsong: return 4;
                case SoundCue.CompostThud: return 2;
                default: return 1;
            }
        }

        public static bool IsLoop(SoundCue cue)
        {
            switch (cue)
            {
                case SoundCue.RakeRustle:
                case SoundCue.BlowerLoop:
                case SoundCue.VacuumLoop:
                case SoundCue.MulcherLoop:
                case SoundCue.WindBed:
                case SoundCue.MusicTitle:
                case SoundCue.MusicYard:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Renders a cue. Deterministic: the same cue and variant always give the same samples.</summary>
        public static SoundBuffer Generate(SoundCue cue, int variant = 0)
        {
            int count = VariantCount(cue);
            variant = ((variant % count) + count) % count;
            int seed = 1000 + (int)cue * 37 + variant * 7919;
            switch (cue)
            {
                case SoundCue.RakeRustle: return RakeRustle(seed);
                case SoundCue.BlowerLoop: return EngineLoop(seed, motorHz: 118f, noiseCutoff: 2600f, noiseGain: 0.55f, whineHz: 1760f, whineGain: 0.04f, tubeHz: 0f, chopHz: 0f);
                case SoundCue.VacuumLoop: return EngineLoop(seed, motorHz: 196f, noiseCutoff: 1300f, noiseGain: 0.45f, whineHz: 3150f, whineGain: 0.06f, tubeHz: 850f, chopHz: 0f);
                case SoundCue.MulcherLoop: return EngineLoop(seed, motorHz: 74f, noiseCutoff: 900f, noiseGain: 0.5f, whineHz: 0f, whineGain: 0f, tubeHz: 520f, chopHz: 12f);
                case SoundCue.ToolEquip: return ToolEquip(seed);
                case SoundCue.BagRustle: return BagRustle(seed);
                case SoundCue.CoinChime: return Bells(new[] { 88f, 95f }, 0.07f, 0.55f, 0.75f);
                case SoundCue.Purchase: return Bells(new[] { 84f, 88f, 91f }, 0.06f, 0.5f, 0.8f);
                case SoundCue.Upgrade: return Bells(new[] { 79f, 84f, 88f, 91f, 96f }, 0.055f, 0.8f, 1.3f);
                case SoundCue.CompostThud: return CompostThud(seed);
                case SoundCue.CompostReady: return Marimba(new[] { 67f, 74f, 79f }, 0.11f, 1.3f);
                case SoundCue.RestoreSparkle: return Sparkle(seed);
                case SoundCue.YardComplete: return YardComplete();
                case SoundCue.UiClick: return UiClick(seed);
                case SoundCue.WindBed: return WindBed(seed);
                case SoundCue.WindGust: return WindGust(seed);
                case SoundCue.Birdsong: return Birdsong(seed);
                case SoundCue.MusicTitle: return Music(seed, MusicStyle.Title);
                case SoundCue.MusicYard: return Music(seed, MusicStyle.Yard);
                default: throw new ArgumentOutOfRangeException(nameof(cue), cue, "No recipe for this cue.");
            }
        }

        // ------------------------------------------------------------------ tools

        /// <summary>Dry leaves under tines: a soft band of noise plus lots of tiny crackles.</summary>
        static SoundBuffer RakeRustle(int seed)
        {
            const int sr = SfxRate;
            const float loopSeconds = 2f, fadeSeconds = 0.25f;
            var rng = new SynthRandom(seed);
            int n = Synth.Samples(loopSeconds + fadeSeconds, sr);
            var raw = new float[n];

            // Crackle grains: short bursts of noise with a fast decay.
            int grains = (int)((loopSeconds + fadeSeconds) * 160f);
            for (int g = 0; g < grains; g++)
            {
                int start = rng.Range(0, n);
                int len = Synth.Samples(rng.Range(0.002f, 0.012f), sr);
                float amp = rng.Range(0.15f, 1f);
                amp *= amp;
                for (int i = 0; i < len && start + i < n; i++)
                    raw[start + i] += amp * rng.NextSigned() * Synth.Decay((float)i / sr, len / (float)sr);
            }

            var crisp = new SvFilter(sr, 3800f, 0.6f);
            var body = new SvFilter(sr, 900f, 0.7f);
            var outBuf = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float flutter = 0.6f + 0.4f * Synth.Sin(Synth.TwoPi * 3f * t + 1.3f * Synth.Sin(Synth.TwoPi * 0.5f * t));
                float bed = rng.NextSigned() * 0.12f * flutter;
                crisp.Process(raw[i] + bed);
                body.Process(raw[i] * 0.5f + bed);
                outBuf[i] = crisp.Band * 1.1f + body.Low * 0.35f;
            }
            var loop = Synth.MakeSeamlessLoop(outBuf, Synth.Samples(fadeSeconds, sr));
            Synth.Normalize(loop, 0.8f);
            return new SoundBuffer(loop, sr, true);
        }

        /// <summary>
        /// Small two-stroke style engine: harmonic motor tone plus filtered air noise. Tone
        /// frequencies are rounded to whole multiples of 1/loop so the loop point is seamless.
        /// </summary>
        static SoundBuffer EngineLoop(int seed, float motorHz, float noiseCutoff, float noiseGain,
            float whineHz, float whineGain, float tubeHz, float chopHz)
        {
            const int sr = SfxRate;
            const float loopSeconds = 2f, fadeSeconds = 0.2f;
            motorHz = SnapToLoop(motorHz, loopSeconds);
            whineHz = SnapToLoop(whineHz, loopSeconds);
            chopHz = SnapToLoop(chopHz, loopSeconds);

            var rng = new SynthRandom(seed);
            int n = Synth.Samples(loopSeconds + fadeSeconds, sr);
            var buf = new float[n];
            var air = new SvFilter(sr, noiseCutoff, 0.6f);
            var hiss = new SvFilter(sr, 6000f, 0.5f);
            var tube = tubeHz > 0f ? new SvFilter(sr, tubeHz, 4f) : null;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                // 4 Hz wobble: the motor is never perfectly steady.
                float wobble = 1f + 0.05f * Synth.Sin(Synth.TwoPi * 4f * t);
                float motor = 0f;
                for (int h = 1; h <= 8; h++)
                    motor += Synth.Sin(Synth.TwoPi * motorHz * h * t + h * 0.7f) / (h * (h % 2 == 0 ? 1.6f : 1f));
                motor *= 0.22f * wobble;

                float white = rng.NextSigned();
                air.Process(white);
                hiss.Process(white);
                float noise = air.Low * noiseGain + hiss.High * 0.06f;
                if (tube != null)
                {
                    tube.Process(white);
                    noise += tube.Band * 0.35f;
                }
                if (chopHz > 0f)
                {
                    float c = Math.Abs(Synth.Sin(Synth.TwoPi * chopHz * 0.5f * t));
                    noise *= 0.35f + 0.9f * c * c * c * c;
                }
                float whine = whineHz > 0f ? whineGain * Synth.Sin(Synth.TwoPi * whineHz * t) : 0f;
                buf[i] = motor + noise + whine;
            }
            var loop = Synth.MakeSeamlessLoop(buf, Synth.Samples(fadeSeconds, sr));
            Synth.Normalize(loop, 0.75f);
            return new SoundBuffer(loop, sr, true);
        }

        /// <summary>Rounds a frequency so a whole number of cycles fits in the loop.</summary>
        static float SnapToLoop(float hz, float loopSeconds) => (float)Math.Round(hz * loopSeconds) / loopSeconds;

        static SoundBuffer ToolEquip(int seed)
        {
            const int sr = SfxRate;
            var rng = new SynthRandom(seed);
            int n = Synth.Samples(0.22f, sr);
            var buf = new float[n];
            var click = new SvFilter(sr, 2500f, 0.8f);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float hz = 90f + 90f * Synth.Decay(t, 0.05f);
                phase += Synth.TwoPi * hz / sr;
                float thump = Synth.Sin(phase) * Synth.AttackDecay(t, 0.002f, 0.09f);
                click.Process(rng.NextSigned());
                float tick = click.Band * Synth.Decay(t, 0.012f);
                // A second, quieter knock: the handle settling in the hand.
                float t2 = t - 0.07f;
                float knock = t2 > 0f ? Synth.Sin(Synth.TwoPi * 240f * t2) * Synth.AttackDecay(t2, 0.001f, 0.04f) * 0.4f : 0f;
                buf[i] = thump * 0.8f + tick * 0.9f + knock;
            }
            Synth.FadeEdges(buf, sr);
            Synth.Normalize(buf, 0.7f);
            return new SoundBuffer(buf, sr, false);
        }

        /// <summary>A paper leaf bag being shaken and closed: crinkles over a soft swoosh.</summary>
        static SoundBuffer BagRustle(int seed)
        {
            const int sr = SfxRate;
            var rng = new SynthRandom(seed);
            float length = rng.Range(0.55f, 0.75f);
            int n = Synth.Samples(length, sr);
            var raw = new float[n];
            int grains = (int)(length * rng.Range(320f, 420f));
            for (int g = 0; g < grains; g++)
            {
                int start = rng.Range(0, n);
                int len = Synth.Samples(rng.Range(0.001f, 0.006f), sr);
                float amp = rng.Range(0.2f, 1f);
                for (int i = 0; i < len && start + i < n; i++)
                    raw[start + i] += amp * rng.NextSigned() * Synth.Decay((float)i / sr, len / (float)sr);
            }
            var crinkle = new SvFilter(sr, rng.Range(4200f, 5600f), 0.8f);
            var swoosh = new SvFilter(sr, 700f, 0.6f);
            var buf = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float env = Synth.Swell(t, length, 0.25f);
                float white = rng.NextSigned();
                crinkle.Process(raw[i]);
                swoosh.Process(white);
                buf[i] = (crinkle.Band * 1.2f + swoosh.Low * 0.6f) * env;
            }
            Synth.FadeEdges(buf, sr, 0.003f, 0.05f);
            Synth.Normalize(buf, 0.8f);
            return new SoundBuffer(buf, sr, false);
        }

        // ------------------------------------------------------------------ rewards

        static SoundBuffer Bells(float[] midiNotes, float spacing, float t60, float length)
        {
            const int sr = SfxRate;
            var buf = new float[Synth.Samples(length, sr)];
            for (int k = 0; k < midiNotes.Length; k++)
            {
                float amp = 0.5f * (k == midiNotes.Length - 1 ? 1.1f : 0.9f);
                Synth.AddBell(buf, sr, k * spacing, Synth.MidiToHz(midiNotes[k]), amp, t60);
            }
            Synth.FadeEdges(buf, sr, 0.001f, 0.06f);
            Synth.Normalize(buf, 0.7f);
            return new SoundBuffer(buf, sr, false);
        }

        static SoundBuffer Marimba(float[] midiNotes, float spacing, float length)
        {
            const int sr = SfxRate;
            var buf = new float[Synth.Samples(length, sr)];
            for (int k = 0; k < midiNotes.Length; k++)
            {
                float hz = Synth.MidiToHz(midiNotes[k]);
                int start = Synth.Samples(k * spacing, sr);
                for (int i = 0; start + i < buf.Length; i++)
                {
                    float t = (float)i / sr;
                    float s = Synth.Sin(Synth.TwoPi * hz * t) * Synth.AttackDecay(t, 0.002f, 0.7f)
                              + 0.25f * Synth.Sin(Synth.TwoPi * hz * 4f * t) * Synth.Decay(t, 0.08f);
                    buf[start + i] += 0.5f * s;
                }
            }
            Synth.FadeEdges(buf, sr, 0.001f, 0.08f);
            Synth.Normalize(buf, 0.7f);
            return new SoundBuffer(buf, sr, false);
        }

        static SoundBuffer CompostThud(int seed)
        {
            const int sr = SfxRate;
            var rng = new SynthRandom(seed);
            int n = Synth.Samples(0.45f, sr);
            var buf = new float[n];
            var soil = new SvFilter(sr, rng.Range(350f, 500f), 0.7f);
            double phase = 0;
            float baseHz = rng.Range(48f, 58f);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float hz = baseHz + 30f * Synth.Decay(t, 0.08f);
                phase += Synth.TwoPi * hz / sr;
                soil.Process(rng.NextSigned());
                buf[i] = Synth.Sin(phase) * Synth.AttackDecay(t, 0.003f, 0.2f) * 0.9f
                         + soil.Low * Synth.AttackDecay(t, 0.002f, 0.16f) * 1.4f;
            }
            Synth.FadeEdges(buf, sr);
            Synth.Normalize(buf, 0.75f);
            return new SoundBuffer(buf, sr, false);
        }

        /// <summary>Grass and flowers coming back: a scatter of high pentatonic pings.</summary>
        static SoundBuffer Sparkle(int seed)
        {
            const int sr = SfxRate;
            var rng = new SynthRandom(seed);
            var buf = new float[Synth.Samples(1.5f, sr)];
            int[] scale = { 0, 2, 4, 7, 9 };
            for (int k = 0; k < 9; k++)
            {
                float midi = 84 + scale[rng.Range(0, scale.Length)] + 12 * rng.Range(0, 2);
                float start = k * 0.075f + rng.Range(0f, 0.03f);
                Synth.AddBell(buf, sr, start, Synth.MidiToHz(midi), 0.35f * (1f - k / 12f), 0.45f);
            }
            Synth.FadeEdges(buf, sr, 0.001f, 0.1f);
            Synth.Normalize(buf, 0.6f);
            return new SoundBuffer(buf, sr, false);
        }

        /// <summary>Short cheerful flourish for finishing a yard: rising arpeggio into a held chord.</summary>
        static SoundBuffer YardComplete()
        {
            const int sr = SfxRate;
            var buf = new float[Synth.Samples(3f, sr)];
            float[] run = { 72, 74, 76, 79, 81, 84 };
            for (int k = 0; k < run.Length; k++)
                Synth.AddPluck(buf, sr, k * 0.09f, Synth.MidiToHz(run[k]), 0.4f, 0.9f);
            float chordAt = run.Length * 0.09f + 0.05f;
            foreach (float m in new float[] { 60, 64, 67, 72 })
                Synth.AddPad(buf, sr, chordAt, 1.1f, Synth.MidiToHz(m), 0.22f, 0.05f, 1.2f);
            Synth.AddBell(buf, sr, chordAt, Synth.MidiToHz(96), 0.25f, 1.2f);
            Synth.FadeEdges(buf, sr, 0.001f, 0.2f);
            Synth.Normalize(buf, 0.75f);
            return new SoundBuffer(buf, sr, false);
        }

        static SoundBuffer UiClick(int seed)
        {
            const int sr = SfxRate;
            var rng = new SynthRandom(seed);
            int n = Synth.Samples(0.06f, sr);
            var buf = new float[n];
            var f = new SvFilter(sr, 3000f, 1.2f);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                f.Process(rng.NextSigned());
                buf[i] = Synth.Sin(Synth.TwoPi * 1800f * t) * Synth.Decay(t, 0.025f) * 0.6f + f.Band * Synth.Decay(t, 0.008f);
            }
            Synth.FadeEdges(buf, sr, 0.0005f, 0.01f);
            Synth.Normalize(buf, 0.5f);
            return new SoundBuffer(buf, sr, false);
        }

        // ------------------------------------------------------------------ ambience

        /// <summary>Steady outdoor air: soft brown noise with slow swells. 10 s seamless loop.</summary>
        static SoundBuffer WindBed(int seed)
        {
            const int sr = BedRate;
            const float loopSeconds = 10f, fadeSeconds = 1f;
            var rng = new SynthRandom(seed);
            int n = Synth.Samples(loopSeconds + fadeSeconds, sr);
            var buf = new float[n];
            var lp = new SvFilter(sr, 450f, 0.5f);
            var whistle = new SvFilter(sr, 900f, 3f);
            float brown = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float white = rng.NextSigned();
                brown = brown * 0.985f + white * 0.15f;
                // Swell periods divide the loop length so the motion repeats cleanly too.
                float swell = 0.6f + 0.25f * Synth.Sin(Synth.TwoPi * 0.1f * t) + 0.15f * Synth.Sin(Synth.TwoPi * 0.3f * t + 2f);
                whistle.Set(800f + 300f * Synth.Sin(Synth.TwoPi * 0.2f * t), 3f);
                lp.Process(brown);
                whistle.Process(white);
                buf[i] = (lp.Low + whistle.Band * 0.04f) * swell;
            }
            var loop = Synth.MakeSeamlessLoop(buf, Synth.Samples(fadeSeconds, sr));
            Synth.Normalize(loop, 0.6f);
            return new SoundBuffer(loop, sr, true);
        }

        /// <summary>A gust rolling through: swept band of noise that swells and fades.</summary>
        static SoundBuffer WindGust(int seed)
        {
            const int sr = BedRate;
            var rng = new SynthRandom(seed);
            float length = rng.Range(3.2f, 4.2f);
            float peakAt = rng.Range(0.3f, 0.45f);
            int n = Synth.Samples(length, sr);
            var buf = new float[n];
            var band = new SvFilter(sr, 400f, 0.9f);
            var rumble = new SvFilter(sr, 160f, 0.6f);
            float brown = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float env = Synth.Swell(t, length, peakAt);
                float white = rng.NextSigned();
                brown = brown * 0.98f + white * 0.2f;
                if ((i & 31) == 0) band.Set(300f + 1100f * env + 120f * Synth.Sin(Synth.TwoPi * 1.7f * t), 0.9f);
                band.Process(white);
                rumble.Process(brown);
                buf[i] = (band.Band * 0.8f + rumble.Low * 1.2f) * env;
            }
            Synth.FadeEdges(buf, sr, 0.01f, 0.1f);
            Synth.Normalize(buf, 0.8f);
            return new SoundBuffer(buf, sr, false);
        }

        /// <summary>A short made-up songbird phrase: a few whistled, swooping syllables.</summary>
        static SoundBuffer Birdsong(int seed)
        {
            const int sr = SfxRate;
            var rng = new SynthRandom(seed);
            int syllables = rng.Range(2, 7);
            float baseHz = rng.Range(2600f, 4200f);
            float sweep = rng.Range(-1400f, 1400f);
            float sylLen = rng.Range(0.06f, 0.13f);
            float gap = rng.Range(0.03f, 0.09f);
            float length = syllables * (sylLen + gap) + 0.1f;
            var buf = new float[Synth.Samples(length, sr)];
            for (int s = 0; s < syllables; s++)
            {
                float start = s * (sylLen + gap);
                // Each syllable varies a little, and the phrase drifts down at the end.
                float hz0 = baseHz * rng.Range(0.92f, 1.08f) * (s == syllables - 1 ? 0.85f : 1f);
                float thisSweep = sweep * rng.Range(0.6f, 1.2f);
                int st = Synth.Samples(start, sr);
                int len = Synth.Samples(sylLen, sr);
                double phase = 0;
                for (int i = 0; i < len && st + i < buf.Length; i++)
                {
                    float x = (float)i / len;
                    float hz = hz0 + thisSweep * (x - 0.5f) + 180f * Synth.Sin(Synth.TwoPi * 28f * x * sylLen);
                    phase += Synth.TwoPi * hz / sr;
                    float env = Synth.Sin(Math.PI * x);
                    env *= env;
                    buf[st + i] += (Synth.Sin(phase) + 0.12f * Synth.Sin(phase * 2.0)) * env;
                }
            }
            Synth.FadeEdges(buf, sr, 0.002f, 0.03f);
            Synth.Normalize(buf, 0.5f);
            return new SoundBuffer(buf, sr, false);
        }

        // ------------------------------------------------------------------ music

        enum MusicStyle { Title, Yard }

        /// <summary>
        /// Gentle generative loop: warm pad chords, a soft bass and a plucked pentatonic melody
        /// picked by a seeded random walk. Notes are written with wrap-around, so the tail of
        /// the last bar rings into the first and the loop has no seam.
        /// </summary>
        static SoundBuffer Music(int seed, MusicStyle style)
        {
            const int sr = BedRate;
            var rng = new SynthRandom(seed);

            float bpm;
            int[][] chords; // MIDI notes, one chord per bar
            if (style == MusicStyle.Title)
            {
                bpm = 68f;
                chords = new[]
                {
                    new[] { 53, 57, 60, 64 }, // Fmaj7
                    new[] { 48, 55, 59, 64 }, // Cmaj7
                    new[] { 45, 55, 60, 64 }, // Am7
                    new[] { 43, 55, 57, 62 }, // G6sus
                    new[] { 53, 57, 60, 64 },
                    new[] { 48, 55, 59, 64 },
                    new[] { 50, 57, 60, 65 }, // Dm7
                    new[] { 43, 55, 59, 62 }, // G
                };
            }
            else
            {
                bpm = 84f;
                chords = new[]
                {
                    new[] { 48, 55, 60, 64 }, // C
                    new[] { 45, 52, 57, 60 }, // Am
                    new[] { 41, 53, 57, 60 }, // F
                    new[] { 43, 55, 59, 62 }, // G
                    new[] { 48, 55, 60, 64 },
                    new[] { 40, 55, 59, 64 }, // Em
                    new[] { 41, 53, 57, 62 }, // F6
                    new[] { 43, 55, 59, 65 }, // G7
                };
            }

            float beat = 60f / bpm;
            float bar = beat * 4f;
            float loopSeconds = bar * chords.Length;
            var buf = new float[Synth.Samples(loopSeconds, sr)];
            int[] penta = { 0, 2, 4, 7, 9 }; // C major pentatonic: always consonant over these chords

            for (int b = 0; b < chords.Length; b++)
            {
                float barStart = b * bar;
                int[] chord = chords[b];

                // Pad: upper chord tones, held for the bar and overlapping into the next.
                for (int k = 1; k < chord.Length; k++)
                    Synth.AddPad(buf, sr, barStart, bar * 0.95f, Synth.MidiToHz(chord[k]), 0.07f, 0.9f, 1.4f, wrap: true);

                // Bass: root on beat 1, fifth-ish on beat 3 in the yard tune.
                Synth.AddPluck(buf, sr, barStart, Synth.MidiToHz(chord[0] - 12), 0.22f, 1.6f, wrap: true);
                if (style == MusicStyle.Yard)
                    Synth.AddPluck(buf, sr, barStart + 2f * beat, Synth.MidiToHz(chord[0] - 5), 0.14f, 1.2f, wrap: true);
            }

            // Melody: eighth-note grid, sparse and stepwise.
            int degree = 2, octave = 72;
            float density = style == MusicStyle.Title ? 0.35f : 0.5f;
            int steps = chords.Length * 8;
            for (int s = 0; s < steps; s++)
            {
                bool downbeat = s % 8 == 0;
                if (!downbeat && rng.Next01() > density) continue;
                degree += rng.Range(-2, 3);
                if (degree < 0) { degree += penta.Length; octave -= 12; }
                if (degree >= penta.Length) { degree -= penta.Length; octave += 12; }
                if (octave < 72) octave = 72;
                if (octave > 84) octave = 84;
                float t = s * beat * 0.5f + rng.Range(0f, 0.012f); // a touch of human timing
                float amp = (downbeat ? 0.16f : 0.11f) * rng.Range(0.8f, 1f);
                Synth.AddPluck(buf, sr, t, Synth.MidiToHz(octave + penta[degree]), amp, style == MusicStyle.Title ? 1.6f : 1.1f, wrap: true);
            }

            Synth.Normalize(buf, 0.6f);
            return new SoundBuffer(buf, sr, true);
        }
    }
}

using System;
using System.Collections.Generic;
using FallenYard.Audio;
using NUnit.Framework;

namespace FallenYard.Tests.Audio
{
    public class ProceduralSoundsTests
    {
        static IEnumerable<SoundCue> AllCues() => (SoundCue[])Enum.GetValues(typeof(SoundCue));

        [Test, TestCaseSource(nameof(AllCues))]
        public void EveryCue_IsAudible_Finite_AndNeverClips(SoundCue cue)
        {
            for (int v = 0; v < ProceduralSounds.VariantCount(cue); v++)
            {
                var b = ProceduralSounds.Generate(cue, v);
                Assert.Greater(b.samples.Length, 100, $"{cue} v{v} is empty");
                Assert.That(b.sampleRate, Is.EqualTo(ProceduralSounds.SfxRate).Or.EqualTo(ProceduralSounds.BedRate));
                foreach (float s in b.samples)
                    Assert.IsFalse(float.IsNaN(s) || float.IsInfinity(s), $"{cue} v{v} has a non-finite sample");
                Assert.LessOrEqual(Synth.Peak(b.samples), 1f, $"{cue} v{v} clips");
                Assert.Greater(Synth.Rms(b.samples), 0.02f, $"{cue} v{v} is near silent");
            }
        }

        [Test, TestCaseSource(nameof(AllCues))]
        public void EveryCue_IsDeterministic(SoundCue cue)
        {
            var a = ProceduralSounds.Generate(cue).samples;
            var b = ProceduralSounds.Generate(cue).samples;
            CollectionAssert.AreEqual(a, b);
        }

        [Test, TestCaseSource(nameof(AllCues))]
        public void LoopFlag_MatchesIsLoop(SoundCue cue)
        {
            Assert.AreEqual(ProceduralSounds.IsLoop(cue), ProceduralSounds.Generate(cue).loop);
        }

        [Test, TestCaseSource(nameof(AllCues))]
        public void Loops_JoinWithoutAClick_AndOneShots_StartAndEndSilent(SoundCue cue)
        {
            var s = ProceduralSounds.Generate(cue).samples;
            if (ProceduralSounds.IsLoop(cue))
            {
                // The jump across the loop point should be no bigger than an ordinary step inside the clip.
                float maxStep = 0f;
                for (int i = 1; i < s.Length; i++) maxStep = Math.Max(maxStep, Math.Abs(s[i] - s[i - 1]));
                Assert.LessOrEqual(Math.Abs(s[s.Length - 1] - s[0]), Math.Max(0.03f, maxStep), $"{cue} clicks at the loop point");
            }
            else
            {
                Assert.AreEqual(0f, s[0], 1e-3f, $"{cue} starts with a click");
                Assert.AreEqual(0f, s[s.Length - 1], 1e-3f, $"{cue} ends with a click");
            }
        }

        [Test]
        public void Variants_SoundDifferent()
        {
            foreach (SoundCue cue in AllCues())
            {
                if (ProceduralSounds.VariantCount(cue) < 2) continue;
                var a = ProceduralSounds.Generate(cue, 0).samples;
                var b = ProceduralSounds.Generate(cue, 1).samples;
                bool same = a.Length == b.Length;
                for (int i = 0; same && i < a.Length; i++) same = a[i] == b[i];
                Assert.IsFalse(same, $"{cue} variants 0 and 1 are identical");
            }
        }

        [Test]
        public void Variant_WrapsAround()
        {
            int n = ProceduralSounds.VariantCount(SoundCue.Birdsong);
            CollectionAssert.AreEqual(ProceduralSounds.Generate(SoundCue.Birdsong, 1).samples,
                ProceduralSounds.Generate(SoundCue.Birdsong, 1 + n).samples);
            CollectionAssert.AreEqual(ProceduralSounds.Generate(SoundCue.Birdsong, n - 1).samples,
                ProceduralSounds.Generate(SoundCue.Birdsong, -1).samples);
        }

        [Test]
        public void MusicLoops_AreLongEnoughNotToFeelRepetitive()
        {
            Assert.Greater(ProceduralSounds.Generate(SoundCue.MusicTitle).Seconds, 15f);
            Assert.Greater(ProceduralSounds.Generate(SoundCue.MusicYard).Seconds, 15f);
        }
    }

    public class SynthTests
    {
        [Test]
        public void Random_IsDeterministic_AndInRange()
        {
            var a = new SynthRandom(42);
            var b = new SynthRandom(42);
            for (int i = 0; i < 1000; i++)
            {
                float x = a.Next01();
                Assert.AreEqual(x, b.Next01());
                Assert.That(x, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                int r = a.Range(3, 7);
                b.Range(3, 7);
                Assert.That(r, Is.InRange(3, 6));
            }
        }

        [Test]
        public void Random_ZeroSeed_StillProducesNumbers()
        {
            var r = new SynthRandom(0);
            Assert.AreNotEqual(r.NextUInt(), r.NextUInt());
        }

        [Test]
        public void Osc_MatchesMathSin()
        {
            var osc = new Synth.Osc(440f, 44100);
            for (int i = 0; i < 44100; i++)
            {
                float expected = (float)Math.Sin(2.0 * Math.PI * 440.0 * i / 44100.0);
                Assert.AreEqual(expected, osc.Next(), 1e-4f, $"sample {i}");
            }
        }

        [Test]
        public void MakeSeamlessLoop_ShortensByFade_AndBlendsTailIntoHead()
        {
            var src = new float[100];
            for (int i = 0; i < src.Length; i++) src[i] = i;
            var loop = Synth.MakeSeamlessLoop(src, 20);
            Assert.AreEqual(80, loop.Length);
            Assert.AreEqual(src[80], loop[0], 1e-4f, "first sample continues from the old tail");
            Assert.AreEqual(src[79], loop[79], 1e-4f, "after the fade the clip is unchanged");
        }

        [Test]
        public void Normalize_HitsPeak_AndLeavesSilenceAlone()
        {
            var buf = new[] { 0.1f, -0.4f, 0.2f };
            Synth.Normalize(buf, 0.8f);
            Assert.AreEqual(0.8f, Synth.Peak(buf), 1e-5f);

            var silent = new float[10];
            Synth.Normalize(silent, 0.8f);
            Assert.AreEqual(0f, Synth.Peak(silent));
        }

        [Test]
        public void FadeEdges_ZeroesBothEnds()
        {
            var buf = new float[4000];
            for (int i = 0; i < buf.Length; i++) buf[i] = 1f;
            Synth.FadeEdges(buf, 44100);
            Assert.AreEqual(0f, buf[0]);
            Assert.Less(buf[buf.Length - 1], 0.01f);
            Assert.AreEqual(1f, buf[2000]);
        }

        [Test]
        public void SvFilter_LowPassPassesDc_HighPassBlocksIt()
        {
            var f = new SvFilter(44100, 1000f);
            for (int i = 0; i < 5000; i++) f.Process(1f);
            Assert.AreEqual(1f, f.Low, 1e-3f);
            Assert.AreEqual(0f, f.High, 1e-3f);
        }

        [Test]
        public void MidiToHz_A4Is440()
        {
            Assert.AreEqual(440f, Synth.MidiToHz(69), 1e-3f);
            Assert.AreEqual(880f, Synth.MidiToHz(81), 1e-2f);
        }
    }
}

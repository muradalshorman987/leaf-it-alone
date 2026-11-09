using FallenYard.Audio;
using FallenYard.Core;
using FallenYard.Settings;
using NUnit.Framework;

namespace FallenYard.Tests.Audio
{
    public class ToolLoopModelTests
    {
        [TestCase("rake", SoundCue.RakeRustle)]
        [TestCase("blower", SoundCue.BlowerLoop)]
        [TestCase("vacuum", SoundCue.VacuumLoop)]
        [TestCase("mulcher", SoundCue.MulcherLoop)]
        public void CueForTool_MapsKnownTools(string id, SoundCue expected)
        {
            Assert.AreEqual(expected, ToolLoopModel.CueForTool(id));
        }

        [Test]
        public void CueForTool_UnknownOrBag_IsSilent()
        {
            Assert.IsNull(ToolLoopModel.CueForTool("bag"));
            Assert.IsNull(ToolLoopModel.CueForTool(null));
        }

        [Test]
        public void Equip_ReportsClipChangesOnly()
        {
            var m = new ToolLoopModel();
            Assert.IsTrue(m.Equip("blower"));
            Assert.IsFalse(m.Equip("blower"));
            Assert.IsTrue(m.Equip("bag"));
            Assert.AreEqual(0f, m.Volume);
        }

        [Test]
        public void Engine_IdlesWhenHeld_AndRevsWithThrottle()
        {
            var m = new ToolLoopModel();
            m.Equip("blower");
            m.Tick(0.1f);
            float idleVolume = m.Volume, idlePitch = m.Pitch;
            Assert.AreEqual(m.tuning.engineIdle, idleVolume, 1e-4f);

            m.SetThrottle(1f);
            for (int i = 0; i < 60; i++) m.Tick(1f / 60f);
            Assert.Greater(m.Volume, idleVolume);
            Assert.Greater(m.Pitch, idlePitch);
            Assert.AreEqual(1f, m.SmoothedThrottle, 0.01f);
        }

        [Test]
        public void Throttle_IsSmoothed_NotInstant()
        {
            var m = new ToolLoopModel();
            m.Equip("vacuum");
            m.SetThrottle(1f);
            m.Tick(0.01f);
            Assert.Greater(m.SmoothedThrottle, 0f);
            Assert.Less(m.SmoothedThrottle, 0.5f);
        }

        [Test]
        public void ReleaseIsSlowerThanAttack()
        {
            var up = new ToolLoopModel();
            up.Equip("blower");
            up.SetThrottle(1f);
            up.Tick(0.1f);
            float rise = up.SmoothedThrottle;

            var down = new ToolLoopModel();
            down.Equip("blower");
            down.SetThrottle(1f);
            for (int i = 0; i < 100; i++) down.Tick(0.05f);
            down.SetThrottle(0f);
            down.Tick(0.1f);
            float fall = 1f - down.SmoothedThrottle;
            Assert.Greater(rise, fall);
        }

        [Test]
        public void Rake_IsSilentUntilSweeping()
        {
            var m = new ToolLoopModel();
            m.Equip("rake");
            m.Tick(0.1f);
            Assert.AreEqual(0f, m.Volume);
            m.SetThrottle(0.8f);
            for (int i = 0; i < 30; i++) m.Tick(1f / 30f);
            Assert.Greater(m.Volume, 0.5f);
        }

        [Test]
        public void Suspended_FadesEvenTheIdleOut()
        {
            var m = new ToolLoopModel();
            m.Equip("mulcher");
            m.SetThrottle(1f);
            for (int i = 0; i < 30; i++) m.Tick(0.05f);
            m.Suspended = true;
            for (int i = 0; i < 100; i++) m.Tick(0.05f);
            Assert.AreEqual(0f, m.Volume, 1e-3f);
        }
    }

    public class AudioBusesTests
    {
        [Test]
        public void Apply_CopiesChannelSliders()
        {
            var b = new AudioBuses();
            b.Apply(new GameSettings { masterVolume = 0.5f, musicVolume = 0.2f, sfxVolume = 0.4f, ambienceVolume = 0.6f });
            Assert.AreEqual(0.5f, b.Channel(AudioChannel.Master));
            Assert.AreEqual(0.2f, b.Channel(AudioChannel.Music));
            Assert.AreEqual(0.4f, b.Channel(AudioChannel.Sfx));
            Assert.AreEqual(0.6f, b.Channel(AudioChannel.Ambience));
        }

        [Test]
        public void SourceGain_UsesChannelButNotMaster()
        {
            // Master is applied once on the AudioListener by SettingsManager.
            var b = new AudioBuses();
            b.Apply(new GameSettings { masterVolume = 0.1f, sfxVolume = 0.5f });
            Assert.AreEqual(0.4f, b.SourceGain(AudioChannel.Sfx, 0.8f), 1e-5f);
        }

        [Test]
        public void SourceGain_WithMixer_LeavesChannelToTheMixer()
        {
            var b = new AudioBuses { MixerDrivesChannels = true };
            b.Apply(new GameSettings { sfxVolume = 0.5f });
            Assert.AreEqual(0.8f, b.SourceGain(AudioChannel.Sfx, 0.8f), 1e-5f);
            Assert.AreEqual(GameSettings.LinearToDecibels(0.5f), b.MixerDecibels(AudioChannel.Sfx), 1e-4f);
        }

        [Test]
        public void MusicDuck_OnlyAffectsMusic()
        {
            var b = new AudioBuses { MusicDuck = 0.5f };
            b.Apply(new GameSettings { musicVolume = 1f, sfxVolume = 1f, ambienceVolume = 1f });
            Assert.AreEqual(0.5f, b.SourceGain(AudioChannel.Music, 1f), 1e-5f);
            Assert.AreEqual(1f, b.SourceGain(AudioChannel.Sfx, 1f), 1e-5f);
            Assert.AreEqual(1f, b.SourceGain(AudioChannel.Ambience, 1f), 1e-5f);
        }

        [Test]
        public void MutedChannel_IsSilent()
        {
            var b = new AudioBuses();
            b.Apply(new GameSettings { ambienceVolume = 0f });
            Assert.AreEqual(0f, b.SourceGain(AudioChannel.Ambience, 1f));
        }
    }

    public class MusicDirectorTests
    {
        [TestCase(GamePhase.Boot, MusicTrack.None)]
        [TestCase(GamePhase.TitleMenu, MusicTrack.Title)]
        [TestCase(GamePhase.Playing, MusicTrack.Yard)]
        [TestCase(GamePhase.Paused, MusicTrack.Yard)]
        [TestCase(GamePhase.Shop, MusicTrack.Yard)]
        [TestCase(GamePhase.YardComplete, MusicTrack.Yard)]
        public void TrackFor_Phase(GamePhase phase, MusicTrack expected)
        {
            Assert.AreEqual(expected, MusicDirector.TrackFor(phase));
        }

        [Test]
        public void MenusDuck_AndPauseMuffles()
        {
            var d = new MusicDirector();
            d.SetPhase(GamePhase.Playing);
            Assert.AreEqual(1f, d.Duck);
            Assert.AreEqual(MusicDirector.OpenCutoff, d.Cutoff);

            d.SetPhase(GamePhase.Paused);
            Assert.Less(d.Duck, 1f);
            Assert.AreEqual(MusicDirector.MuffledCutoff, d.Cutoff);

            d.SetPhase(GamePhase.Shop);
            Assert.Less(d.Duck, 1f);
            Assert.AreEqual(MusicDirector.OpenCutoff, d.Cutoff);
        }

        [Test]
        public void Stinger_DucksThenRecovers()
        {
            var d = new MusicDirector();
            d.SetPhase(GamePhase.Playing);
            d.StartStinger(2f);
            Assert.AreEqual(d.StingerDuckLevel, d.Duck);
            d.Tick(2.1f);
            Assert.AreEqual(1f, d.Duck);
        }

        [Test]
        public void Boot_FallsBackToYardMusic_WhenNoMenuTakesOver()
        {
            var d = new MusicDirector { BootFallbackSeconds = 1f };
            Assert.AreEqual(MusicTrack.None, d.Track);
            d.Tick(1.1f);
            Assert.AreEqual(MusicTrack.Yard, d.Track);
            d.SetPhase(GamePhase.TitleMenu);
            Assert.AreEqual(MusicTrack.Title, d.Track);
        }

        [Test]
        public void Override_WinsOverPhase()
        {
            var d = new MusicDirector();
            d.SetPhase(GamePhase.Playing);
            d.Override = MusicTrack.Title;
            Assert.AreEqual(MusicTrack.Title, d.Track);
            d.Override = null;
            Assert.AreEqual(MusicTrack.Yard, d.Track);
        }

        [Test]
        public void CueFor_Track()
        {
            Assert.AreEqual(SoundCue.MusicTitle, MusicDirector.CueFor(MusicTrack.Title));
            Assert.AreEqual(SoundCue.MusicYard, MusicDirector.CueFor(MusicTrack.Yard));
            Assert.IsNull(MusicDirector.CueFor(MusicTrack.None));
        }
    }

    public class AmbienceSchedulerTests
    {
        [Test]
        public void Calls_ArriveWithinTheConfiguredRange()
        {
            var s = new AmbienceScheduler(7, 2f, 5f);
            float t = 0f, last = 0f;
            int calls = 0;
            while (t < 120f)
            {
                t += 0.05f;
                if (!s.Tick(0.05f)) continue;
                float gap = t - last;
                if (calls > 0) Assert.That(gap, Is.InRange(1.9f, 5.1f));
                last = t;
                calls++;
            }
            Assert.Greater(calls, 20);
        }

        [Test]
        public void Suppress_SilencesForAWhile()
        {
            var s = new AmbienceScheduler(7, 0.1f, 0.2f);
            s.Suppress(3f);
            for (int i = 0; i < 50; i++) Assert.IsFalse(s.Tick(0.05f), $"tick {i}");
            bool any = false;
            for (int i = 0; i < 20; i++) any |= s.Tick(0.05f);
            Assert.IsTrue(any);
        }

        [Test]
        public void Disabled_NeverCalls()
        {
            var s = new AmbienceScheduler(7, 0.1f, 0.2f) { Enabled = false };
            for (int i = 0; i < 100; i++) Assert.IsFalse(s.Tick(0.1f));
        }

        [Test]
        public void SameSeed_SameTiming()
        {
            var a = new AmbienceScheduler(3);
            var b = new AmbienceScheduler(3);
            for (int i = 0; i < 2000; i++) Assert.AreEqual(a.Tick(0.05f), b.Tick(0.05f));
        }

        static int CallsIn120Seconds(float rate)
        {
            var s = new AmbienceScheduler(5, 2f, 4f) { Rate = rate };
            int n = 0;
            for (int i = 0; i < 2400; i++) if (s.Tick(0.05f)) n++;
            return n;
        }

        [Test]
        public void Rate_ScalesHowOftenBirdsSing()
        {
            Assert.Greater(CallsIn120Seconds(1f), CallsIn120Seconds(0.3f));
            Assert.AreEqual(0, CallsIn120Seconds(0f));
        }

        [Test]
        public void WeatherAmbience_WindierMeansFewerBirds()
        {
            WeatherAmbience.For("clear", out float clearWind, out float clearBirds);
            WeatherAmbience.For("blustery", out float gustyWind, out float gustyBirds);
            WeatherAmbience.For("something-new", out float unknownWind, out float unknownBirds);
            Assert.Greater(gustyWind, clearWind);
            Assert.Less(gustyBirds, clearBirds);
            Assert.AreEqual(clearWind, unknownWind);
            Assert.AreEqual(clearBirds, unknownBirds);
        }

        [Test]
        public void PickCall_StaysInRange()
        {
            var s = new AmbienceScheduler(1);
            for (int i = 0; i < 100; i++)
            {
                s.PickCall(4, out int v, out float pan, out float pitch);
                Assert.That(v, Is.InRange(0, 3));
                Assert.That(pan, Is.InRange(-1f, 1f));
                Assert.That(pitch, Is.InRange(0.5f, 2f));
            }
        }
    }

    public class RewardPitchTests
    {
        [Test]
        public void Streak_RisesThenResetsAfterAPause()
        {
            var r = new RewardPitch { Window = 2f };
            Assert.AreEqual(1f, r.Next(0f), 1e-5f);
            float second = r.Next(1f);
            float third = r.Next(2f);
            Assert.Greater(second, 1f);
            Assert.Greater(third, second);
            Assert.AreEqual(1f, r.Next(10f), 1e-5f);
        }

        [Test]
        public void Streak_TopsOutAtAnOctave()
        {
            var r = new RewardPitch();
            float p = 1f;
            for (int i = 0; i < 50; i++) p = r.Next(i * 0.1f);
            Assert.AreEqual(2f, p, 1e-4f);
        }
    }
}

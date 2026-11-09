using FallenYard.Audio;
using FallenYard.Core;
using FallenYard.Settings;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Audio
{
    /// <summary>
    /// Drives the AudioManager through the shared events, the way the game does.
    /// Nothing actually plays in EditMode; the tests check which cue, clip, volume and pitch were chosen.
    /// </summary>
    public class AudioManagerTests
    {
        AudioManager _audio;

        [SetUp]
        public void SetUp()
        {
            GameEvents.ClearAllSubscribers();
            SettingsManager.ClearListeners();
            var go = new GameObject("AudioManager (test)");
            _audio = go.AddComponent<AudioManager>();
            _audio.prewarm = false;
            TestUtil.Call(_audio, "Awake");
            TestUtil.Call(_audio, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            if (_audio != null) TestUtil.Call(_audio, "OnDisable");
            TestUtil.DestroySingleton(_audio);
            GameEvents.ClearAllSubscribers();
            SettingsManager.ClearListeners();
        }

        [Test]
        public void Awake_RegistersInstance_AndBuildsSources()
        {
            Assert.AreSame(_audio, AudioManager.Instance);
            Assert.IsNotNull(_audio.ToolSource);
            Assert.IsNotNull(_audio.WindBedSource);
            Assert.IsNotNull(_audio.ActiveMusicSource);
            Assert.GreaterOrEqual(_audio.GetComponentsInChildren<AudioSource>().Length, 6);
        }

        [Test]
        public void ToolEquipped_SwapsTheLoop_AndPlaysEquipSound()
        {
            GameEvents.RaiseToolEquipped("blower");
            Assert.AreEqual(SoundCue.BlowerLoop, _audio.ToolLoop.Cue);
            Assert.IsNotNull(_audio.ToolSource.clip);
            Assert.AreEqual(SoundCue.ToolEquip, _audio.LastCue);

            GameEvents.RaiseToolEquipped("bag");
            Assert.IsNull(_audio.ToolLoop.Cue);
            Assert.IsNull(_audio.ToolSource.clip);
        }

        [Test]
        public void ToolThrottle_RaisesLoopVolumeAndPitch()
        {
            GameEvents.RaiseToolEquipped("vacuum");
            _audio.Tick(0.1f);
            float idleVolume = _audio.ToolSource.volume;
            float idlePitch = _audio.ToolSource.pitch;
            Assert.Greater(idleVolume, 0f, "engines idle audibly while held");

            GameEvents.RaiseToolThrottle(1f);
            for (int i = 0; i < 30; i++) _audio.Tick(1f / 30f);
            Assert.Greater(_audio.ToolSource.volume, idleVolume);
            Assert.Greater(_audio.ToolSource.pitch, idlePitch);
        }

        [Test]
        public void Pause_SilencesTools_AndDucksMusic()
        {
            GameEvents.RaiseStateChanged(GamePhase.Playing);
            GameEvents.RaiseToolEquipped("blower");
            GameEvents.RaiseToolThrottle(1f);
            for (int i = 0; i < 30; i++) _audio.Tick(0.05f);
            Assert.Greater(_audio.ToolSource.volume, 0f);

            GameEvents.RaiseStateChanged(GamePhase.Paused);
            for (int i = 0; i < 100; i++) _audio.Tick(0.05f);
            Assert.AreEqual(0f, _audio.ToolSource.volume, 1e-4f);
            Assert.Less(_audio.Buses.MusicDuck, 1f);
        }

        [Test]
        public void PileBagged_PlaysRustleThenChime_AndStreaksRaisePitch()
        {
            int before = _audio.CuesPlayed;
            GameEvents.RaisePileBagged(150, 5);
            Assert.AreEqual(before + 2, _audio.CuesPlayed);
            Assert.AreEqual(SoundCue.CoinChime, _audio.LastCue);
            Assert.AreEqual(0, _audio.Rewards.Streak);

            _audio.Tick(0.5f);
            GameEvents.RaisePileBagged(150, 5);
            Assert.AreEqual(1, _audio.Rewards.Streak);
        }

        [Test]
        public void PileBagged_WithoutCoins_HasNoChime()
        {
            GameEvents.RaisePileBagged(10, 0);
            Assert.AreEqual(SoundCue.BagRustle, _audio.LastCue);
        }

        [Test]
        public void SpendingCoins_PlaysPurchase_EarningDoesNot()
        {
            GameEvents.RaiseCoinsChanged(50);
            Assert.IsNull(_audio.LastCue);
            GameEvents.RaiseCoinsChanged(80);
            Assert.IsNull(_audio.LastCue);
            GameEvents.RaiseCoinsChanged(30);
            Assert.AreEqual(SoundCue.Purchase, _audio.LastCue);
        }

        [Test]
        public void ToolUpgraded_PlaysUpgrade()
        {
            GameEvents.RaiseToolUpgraded("rake", 2);
            Assert.AreEqual(SoundCue.Upgrade, _audio.LastCue);
        }

        [Test]
        public void Compost_ThudsOnFill_AndChimesWhenABatchIsReady()
        {
            GameEvents.RaiseCompostChanged(0.1f, 0);
            Assert.IsNull(_audio.LastCue, "first report only sets the baseline");
            GameEvents.RaiseCompostChanged(0.3f, 0);
            Assert.AreEqual(SoundCue.CompostThud, _audio.LastCue);
            GameEvents.RaiseCompostChanged(0f, 1);
            Assert.AreEqual(SoundCue.CompostReady, _audio.LastCue);
        }

        [Test]
        public void Restoration_Sparkles_WhenItGoesUp()
        {
            GameEvents.RaiseYardRestorationChanged(0.2f);
            Assert.IsNull(_audio.LastCue);
            GameEvents.RaiseYardRestorationChanged(0.4f);
            Assert.AreEqual(SoundCue.RestoreSparkle, _audio.LastCue);
        }

        [Test]
        public void WindGust_PlaysGust_AndQuietsBirds()
        {
            GameEvents.RaiseWindGust(Vector3.right, 5f);
            Assert.AreEqual(SoundCue.WindGust, _audio.LastCue);
            Assert.Greater(_audio.Birds.SuppressedFor, 0f);
        }

        [Test]
        public void WeatherChanged_RaisesWindBed_AndQuietsBirds()
        {
            _audio.Tick(0.1f);
            float calm = _audio.WindBedSource.volume;
            GameEvents.RaiseWeatherChanged("blustery");
            for (int i = 0; i < 200; i++) _audio.Tick(0.1f);
            Assert.Greater(_audio.WindBedSource.volume, calm);
            Assert.Less(_audio.Birds.Rate, 1f);
        }

        [Test]
        public void YardCompleted_PlaysStinger_AndDucksMusic()
        {
            GameEvents.RaiseStateChanged(GamePhase.Playing);
            GameEvents.RaiseYardCompleted("starter");
            Assert.AreEqual(SoundCue.YardComplete, _audio.LastCue);
            for (int i = 0; i < 20; i++) _audio.Tick(0.05f);
            Assert.Less(_audio.Buses.MusicDuck, 1f);
        }

        [Test]
        public void StateChanged_PicksMusicTrack()
        {
            GameEvents.RaiseStateChanged(GamePhase.TitleMenu);
            _audio.Tick(0.1f);
            Assert.AreEqual(MusicTrack.Title, _audio.ActiveTrack);
            Assert.IsNotNull(_audio.ActiveMusicSource.clip);

            GameEvents.RaiseStateChanged(GamePhase.Playing);
            _audio.Tick(0.1f);
            Assert.AreEqual(MusicTrack.Yard, _audio.ActiveTrack);
        }

        [Test]
        public void SettingsChanged_UpdatesBusVolumes()
        {
            string dir = TestUtil.UseTempSaveFolder();
            var settings = new GameObject("SettingsManager (test)").AddComponent<SettingsManager>();
            try
            {
                settings.applyToEngine = false;
                TestUtil.Call(settings, "Awake");
                settings.Modify(s => s.sfxVolume = 0.25f, save: false);
                Assert.AreEqual(0.25f, _audio.Buses.Channel(AudioChannel.Sfx), 1e-5f);

                var src = _audio.PlayCue(SoundCue.UiClick, 1f);
                Assert.AreEqual(0.25f * _audio.sfxLevel, src.volume, 1e-4f);
            }
            finally
            {
                TestUtil.DestroySingleton(settings);
                TestUtil.ReleaseTempSaveFolder(dir);
            }
        }

        [Test]
        public void PlayCue_RaisesAudioCuePlayed_ForCaptions()
        {
            string heard = null;
            GameEvents.AudioCuePlayed += id => heard = id;
            _audio.PlayCue(SoundCue.CoinChime);
            Assert.AreEqual("CoinChime", heard);
        }

        [Test]
        public void PlayCue_UsesLibraryOverride_WhenSet()
        {
            var lib = ScriptableObject.CreateInstance<AudioLibrary>();
            var clip = AudioClip.Create("override", 100, 1, 44100, false);
            try
            {
                lib.entries.Add(new AudioLibrary.Entry { cue = SoundCue.CoinChime, clips = new[] { clip }, gain = 0.5f });
                _audio.library = lib;
                var src = _audio.PlayCue(SoundCue.CoinChime, 1f);
                Assert.AreSame(clip, src.clip);
                Assert.AreEqual(0.5f * _audio.sfxLevel, src.volume, 1e-4f);
            }
            finally
            {
                _audio.library = null;
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(lib);
            }
        }

        [Test]
        public void PlayCue_ReusesVoices_WhenAllAreBusy()
        {
            for (int i = 0; i < _audio.oneShotVoices * 3; i++)
                Assert.IsNotNull(_audio.PlayCue(SoundCue.UiClick));
        }

        [Test]
        public void Disable_StopsListening()
        {
            TestUtil.Call(_audio, "OnDisable");
            GameEvents.RaiseToolUpgraded("rake", 1);
            Assert.IsNull(_audio.LastCue);
        }
    }

    public class GeneratedClipsTests
    {
        [Test]
        public void Get_CachesClips()
        {
            var a = GeneratedClips.Get(SoundCue.UiClick);
            var b = GeneratedClips.Get(SoundCue.UiClick);
            Assert.AreSame(a, b);
            Assert.IsTrue(GeneratedClips.IsCached(SoundCue.UiClick));
            Assert.AreEqual(ProceduralSounds.SfxRate, a.frequency);
            Assert.AreEqual(1, a.channels);
        }

        [Test]
        public void Resolve_FallsBackToGenerated()
        {
            Assert.IsNotNull(AudioLibrary.Resolve(null, SoundCue.CoinChime, 0));
            var lib = ScriptableObject.CreateInstance<AudioLibrary>();
            try
            {
                Assert.IsNotNull(AudioLibrary.Resolve(lib, SoundCue.CoinChime, 0));
                Assert.AreEqual(ProceduralSounds.VariantCount(SoundCue.Birdsong), AudioLibrary.Variants(lib, SoundCue.Birdsong));
            }
            finally
            {
                Object.DestroyImmediate(lib);
            }
        }
    }
}

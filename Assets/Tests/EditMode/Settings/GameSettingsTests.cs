using FallenYard.Settings;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Settings
{
    public class GameSettingsTests
    {
        [Test]
        public void Defaults_AreAlreadySane()
        {
            var a = new GameSettings();
            var b = new GameSettings().Sanitize();
            Assert.AreEqual(JsonUtility.ToJson(a), JsonUtility.ToJson(b));
        }

        [Test]
        public void Sanitize_ClampsOutOfRangeValues()
        {
            var s = new GameSettings
            {
                masterVolume = 3f,
                musicVolume = -1f,
                sfxVolume = float.NaN,
                mouseSensitivity = 999f,
                fieldOfView = 10f,
                qualityLevel = 12,
                resolutionWidth = -5,
                resolutionHeight = 1080,
                targetFrameRate = 5,
                bindingOverridesJson = null,
            }.Sanitize(qualityLevelCount: 6);

            Assert.AreEqual(1f, s.masterVolume);
            Assert.AreEqual(0f, s.musicVolume);
            Assert.AreEqual(1f, s.sfxVolume, "NaN falls back to the default");
            Assert.AreEqual(GameSettings.MaxSensitivity, s.mouseSensitivity);
            Assert.AreEqual(GameSettings.MinFov, s.fieldOfView);
            Assert.AreEqual(-1, s.qualityLevel);
            Assert.AreEqual(0, s.resolutionWidth);
            Assert.AreEqual(0, s.resolutionHeight);
            Assert.AreEqual(GameSettings.MinFrameRate, s.targetFrameRate);
            Assert.AreEqual("", s.bindingOverridesJson);
        }

        [Test]
        public void Sanitize_KeepsUnlimitedFrameRate()
        {
            Assert.AreEqual(0, new GameSettings { targetFrameRate = 0 }.Sanitize().targetFrameRate);
        }

        [Test]
        public void Volume_GetSetByChannel()
        {
            var s = new GameSettings();
            s.SetVolume(AudioChannel.Music, 0.25f);
            s.SetVolume(AudioChannel.Sfx, 2f);
            Assert.AreEqual(0.25f, s.GetVolume(AudioChannel.Music));
            Assert.AreEqual(1f, s.GetVolume(AudioChannel.Sfx));
        }

        [Test]
        public void EffectiveVolume_IsMasterTimesChannel()
        {
            var s = new GameSettings { masterVolume = 0.5f, musicVolume = 0.5f };
            Assert.AreEqual(0.25f, s.GetEffectiveVolume(AudioChannel.Music), 1e-6f);
            Assert.AreEqual(0.5f, s.GetEffectiveVolume(AudioChannel.Master), 1e-6f);
        }

        [Test]
        public void LinearToDecibels_MapsEnds()
        {
            Assert.AreEqual(0f, GameSettings.LinearToDecibels(1f), 1e-4f);
            Assert.AreEqual(-80f, GameSettings.LinearToDecibels(0f));
            Assert.AreEqual(-6.02f, GameSettings.LinearToDecibels(0.5f), 0.01f);
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var a = new GameSettings { fieldOfView = 80f };
            var b = a.Clone();
            b.fieldOfView = 90f;
            Assert.AreEqual(80f, a.fieldOfView);
        }
    }
}

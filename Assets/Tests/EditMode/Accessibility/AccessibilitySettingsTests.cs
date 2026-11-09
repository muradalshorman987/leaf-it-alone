using FallenYard.Settings;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Accessibility
{
    public class AccessibilitySettingsTests
    {
        [Test]
        public void Defaults_AreConservative()
        {
            var s = new GameSettings();
            Assert.AreEqual(2, GameSettings.CurrentVersion);
            Assert.IsFalse(s.captions);
            Assert.AreEqual(CaptionSize.Medium, s.captionSize);
            Assert.IsTrue(s.captionBackground);
            Assert.AreEqual(ColorBlindMode.Off, s.colorBlindMode);
            Assert.IsFalse(s.toggleSprint);
            Assert.IsFalse(s.toggleToolUse);
            Assert.IsTrue(s.tutorialHints);
        }

        [Test]
        public void Sanitize_FixesOutOfRangeEnums()
        {
            var s = new GameSettings { captionSize = (CaptionSize)42, colorBlindMode = (ColorBlindMode)(-3) }.Sanitize();
            Assert.AreEqual(CaptionSize.Medium, s.captionSize);
            Assert.AreEqual(ColorBlindMode.Off, s.colorBlindMode);
        }

        [Test]
        public void Version1File_LoadsWithAccessibilityDefaults()
        {
            // A v1 settings file has none of the new fields.
            const string v1 = "{\"version\":1,\"masterVolume\":0.5,\"mouseSensitivity\":3.0,\"headBob\":false}";
            var s = JsonUtility.FromJson<GameSettings>(v1).Sanitize();
            Assert.AreEqual(GameSettings.CurrentVersion, s.version);
            Assert.AreEqual(0.5f, s.masterVolume, 1e-4f);
            Assert.AreEqual(3f, s.mouseSensitivity, 1e-4f);
            Assert.IsTrue(s.tutorialHints);
            Assert.AreEqual(ColorBlindMode.Off, s.colorBlindMode);
        }

        [Test]
        public void AccessibilityFields_RoundTripThroughJson()
        {
            var s = new GameSettings
            {
                captions = true,
                captionSize = CaptionSize.Large,
                captionBackground = false,
                colorBlindMode = ColorBlindMode.Tritanopia,
                toggleSprint = true,
                toggleToolUse = true,
                tutorialHints = false,
            };
            var back = JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(s)).Sanitize();
            Assert.IsTrue(back.captions);
            Assert.AreEqual(CaptionSize.Large, back.captionSize);
            Assert.IsFalse(back.captionBackground);
            Assert.AreEqual(ColorBlindMode.Tritanopia, back.colorBlindMode);
            Assert.IsTrue(back.toggleSprint);
            Assert.IsTrue(back.toggleToolUse);
            Assert.IsFalse(back.tutorialHints);
        }
    }
}

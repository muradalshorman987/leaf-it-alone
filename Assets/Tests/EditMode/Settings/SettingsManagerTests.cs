using System.IO;
using FallenYard.Core;
using FallenYard.Settings;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Settings
{
    public class SettingsStoreTests
    {
        string _dir;

        [SetUp] public void SetUp() => _dir = TestUtil.UseTempSaveFolder();
        [TearDown] public void TearDown() => TestUtil.ReleaseTempSaveFolder(_dir);

        [Test]
        public void FilePath_IsInSaveFolder()
        {
            StringAssert.StartsWith(_dir, SettingsStore.FilePath);
        }

        [Test]
        public void Load_NoFile_ReturnsDefaults()
        {
            Assert.IsFalse(SettingsStore.Exists);
            var s = SettingsStore.Load();
            Assert.AreEqual(new GameSettings().fieldOfView, s.fieldOfView);
        }

        [Test]
        public void SaveThenLoad_RoundTrips()
        {
            var s = new GameSettings
            {
                masterVolume = 0.3f,
                mouseSensitivity = 4.5f,
                invertY = true,
                fieldOfView = 95f,
                qualityLevel = 2,
                fullScreenMode = FullScreenMode.Windowed,
                vSync = false,
                targetFrameRate = 120,
                bindingOverridesJson = "{\"bindings\":[]}",
            };
            SettingsStore.Save(s);
            var loaded = SettingsStore.Load();

            Assert.AreEqual(0.3f, loaded.masterVolume);
            Assert.AreEqual(4.5f, loaded.mouseSensitivity);
            Assert.IsTrue(loaded.invertY);
            Assert.AreEqual(95f, loaded.fieldOfView);
            Assert.AreEqual(2, loaded.qualityLevel);
            Assert.AreEqual(FullScreenMode.Windowed, loaded.fullScreenMode);
            Assert.IsFalse(loaded.vSync);
            Assert.AreEqual(120, loaded.targetFrameRate);
            Assert.AreEqual("{\"bindings\":[]}", loaded.bindingOverridesJson);
            Assert.IsFalse(File.Exists(SettingsStore.FilePath + ".tmp"));
        }

        [Test]
        public void Load_CorruptFile_ReturnsDefaults()
        {
            File.WriteAllText(SettingsStore.FilePath, "not json at all");
            Assert.AreEqual(new GameSettings().masterVolume, SettingsStore.Load().masterVolume);
        }

        [Test]
        public void Load_HandEditedFile_IsSanitized()
        {
            File.WriteAllText(SettingsStore.FilePath, "{ \"masterVolume\": 7, \"fieldOfView\": 400 }");
            var s = SettingsStore.Load();
            Assert.AreEqual(1f, s.masterVolume);
            Assert.AreEqual(GameSettings.MaxFov, s.fieldOfView);
        }

        [Test]
        public void DeletingTheSave_KeepsSettings()
        {
            SettingsStore.Save(new GameSettings { fieldOfView = 99f });
            FallenYard.Save.SaveSystem.Delete();
            Assert.IsTrue(SettingsStore.Exists);
        }
    }

    public class SettingsManagerTests
    {
        string _dir;
        GameManager _gm;
        SettingsManager _settings;

        [SetUp]
        public void SetUp()
        {
            SettingsManager.ClearListeners();
            _dir = TestUtil.UseTempSaveFolder();
            _gm = TestUtil.CreateGameManager();
        }

        [TearDown]
        public void TearDown()
        {
            TestUtil.DestroySingleton(_settings);
            TestUtil.DestroySingleton(_gm);
            TestUtil.ReleaseTempSaveFolder(_dir);
            SettingsManager.ClearListeners();
        }

        SettingsManager CreateManager()
        {
            var m = new GameObject("SettingsManager (test)").AddComponent<SettingsManager>();
            m.applyToEngine = false;
            TestUtil.Call(m, "Awake");
            return m;
        }

        [Test]
        public void FirstRun_SeedsSensitivityFromOldSave_AndWritesFile()
        {
            _gm.Data.mouseSensitivity = 3.25f;
            _settings = CreateManager();

            Assert.AreEqual(3.25f, _settings.Current.mouseSensitivity);
            Assert.IsTrue(SettingsStore.Exists);
        }

        [Test]
        public void ExistingFile_WinsOverOldSaveValue()
        {
            SettingsStore.Save(new GameSettings { mouseSensitivity = 5f });
            _gm.Data.mouseSensitivity = 1f;
            _settings = CreateManager();

            Assert.AreEqual(5f, _settings.Current.mouseSensitivity);
            Assert.AreEqual(5f, _gm.Data.mouseSensitivity, "Legacy save field is kept in sync for FirstPersonController.");
        }

        [Test]
        public void Apply_SanitizesSavesAndNotifies()
        {
            _settings = CreateManager();
            GameSettings notified = null;
            SettingsManager.Changed += s => notified = s;

            var edit = _settings.Current.Clone();
            edit.fieldOfView = 500f;
            edit.musicVolume = 0.1f;
            _settings.Apply(edit);

            Assert.AreSame(_settings.Current, notified);
            Assert.AreEqual(GameSettings.MaxFov, _settings.Current.fieldOfView);
            Assert.AreEqual(0.1f, SettingsStore.Load().musicVolume);
            Assert.AreNotSame(edit, _settings.Current, "Manager keeps its own copy.");
        }

        [Test]
        public void Apply_WithoutSave_DoesNotWrite()
        {
            _settings = CreateManager();
            _settings.Modify(s => s.fieldOfView = 100f, save: false);

            Assert.AreEqual(100f, _settings.Current.fieldOfView);
            Assert.AreNotEqual(100f, SettingsStore.Load().fieldOfView);
        }

        [Test]
        public void ResetToDefaults_RestoresDefaults()
        {
            _settings = CreateManager();
            _settings.Modify(s => { s.invertY = true; s.masterVolume = 0.1f; });
            _settings.ResetToDefaults();

            Assert.IsFalse(_settings.Current.invertY);
            Assert.AreEqual(new GameSettings().masterVolume, _settings.Current.masterVolume);
        }
    }
}

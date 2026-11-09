using System.IO;
using FallenYard.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FallenYard.Tests.Save
{
    public class SaveBackupTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = TestUtil.UseTempSaveFolder();
            SaveRegistry.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            SaveRegistry.Clear();
            TestUtil.ReleaseTempSaveFolder(_dir);
        }

        [Test]
        public void Save_KeepsPreviousSaveAsBackup()
        {
            SaveSystem.Save(new SaveData { coins = 1 });
            SaveSystem.Save(new SaveData { coins = 2 });

            Assert.IsTrue(File.Exists(SaveSystem.BackupPath));
            Assert.AreEqual(1, SaveSystem.FromJson(File.ReadAllText(SaveSystem.BackupPath), out _).coins);
        }

        [Test]
        public void Load_CorruptMain_FallsBackToBackup()
        {
            SaveSystem.Save(new SaveData { coins = 10 });
            SaveSystem.Save(new SaveData { coins = 20 });
            File.WriteAllText(SaveSystem.FilePath, "{ broken");

            LogAssert.ignoreFailingMessages = true;
            var data = SaveSystem.Load();
            LogAssert.ignoreFailingMessages = false;

            Assert.AreEqual(10, data.coins);
            Assert.AreEqual(SaveLoadSource.Backup, SaveSystem.LastLoadSource);
        }

        [Test]
        public void Load_MissingMain_FallsBackToBackup()
        {
            SaveSystem.Save(new SaveData { coins = 10 });
            SaveSystem.Save(new SaveData { coins = 20 });
            File.Delete(SaveSystem.FilePath);

            LogAssert.ignoreFailingMessages = true;
            Assert.AreEqual(10, SaveSystem.Load().coins);
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void Save_DoesNotBackUpACorruptFile()
        {
            SaveSystem.Save(new SaveData { coins = 10 });
            SaveSystem.Save(new SaveData { coins = 20 }); // backup = 10
            File.WriteAllText(SaveSystem.FilePath, "{ broken");
            SaveSystem.Save(new SaveData { coins = 30 });

            Assert.AreEqual(10, SaveSystem.FromJson(File.ReadAllText(SaveSystem.BackupPath), out _).coins);
            Assert.AreEqual(30, SaveSystem.Load().coins);
        }

        [Test]
        public void Save_StampsTimeAndRaisesSaved()
        {
            SaveData raised = null;
            SaveSystem.Saved += OnSaved;
            try
            {
                var data = new SaveData();
                SaveSystem.Save(data);
                Assert.AreSame(data, raised);
                Assert.Greater(data.savedAtUtcTicks, 0);
            }
            finally
            {
                SaveSystem.Saved -= OnSaved;
            }

            void OnSaved(SaveData d) => raised = d;
        }

        [Test]
        public void Load_NothingOnDisk_IsFresh()
        {
            var data = SaveSystem.Load();
            Assert.AreEqual(SaveLoadSource.Fresh, SaveSystem.LastLoadSource);
            Assert.AreEqual(-1, SaveSystem.LastLoadedFileVersion);
            Assert.AreEqual(0, data.coins);
        }

        [Test]
        public void Delete_RemovesBackupsToo()
        {
            File.WriteAllText(SaveSystem.FilePath, "{ \"version\": 1, \"coins\": 3 }");
            SaveSystem.Save(SaveSystem.Load()); // creates .bak and .v1.bak
            Assert.IsTrue(File.Exists(SaveSystem.BackupPath));
            Assert.IsTrue(File.Exists(SaveSystem.VersionBackupPath(1)));

            SaveSystem.Delete();

            Assert.IsFalse(File.Exists(SaveSystem.FilePath));
            Assert.IsFalse(File.Exists(SaveSystem.BackupPath));
            Assert.IsFalse(File.Exists(SaveSystem.VersionBackupPath(1)));
        }

        class CounterSaveable : ISaveable
        {
            public int value;
            public void CaptureState(SaveData data) => data.totalLeavesBagged = value;
            public void RestoreState(SaveData data) => value = (int)data.totalLeavesBagged;
        }

        class ThrowingSaveable : ISaveable
        {
            public void CaptureState(SaveData data) => throw new System.Exception("boom");
            public void RestoreState(SaveData data) => throw new System.Exception("boom");
        }

        [Test]
        public void Save_CapturesRegisteredSaveables()
        {
            var counter = new CounterSaveable { value = 99 };
            SaveRegistry.Register(counter);

            SaveSystem.Save(new SaveData());

            Assert.AreEqual(99, SaveSystem.Load().totalLeavesBagged);
        }

        [Test]
        public void Register_WithData_RestoresImmediately()
        {
            var counter = new CounterSaveable();
            SaveRegistry.Register(counter, new SaveData { totalLeavesBagged = 5 });
            Assert.AreEqual(5, counter.value);
            Assert.AreEqual(1, SaveRegistry.Count);

            SaveRegistry.Register(counter);
            Assert.AreEqual(1, SaveRegistry.Count, "Registering twice must not duplicate.");
        }

        [Test]
        public void BrokenSaveable_DoesNotStopTheSave()
        {
            SaveRegistry.Register(new ThrowingSaveable());
            var counter = new CounterSaveable { value = 7 };
            SaveRegistry.Register(counter);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("ThrowingSaveable failed to capture"));
            SaveSystem.Save(new SaveData());

            Assert.AreEqual(7, SaveSystem.Load().totalLeavesBagged);
        }

        [Test]
        public void Unregister_StopsCapturing()
        {
            var counter = new CounterSaveable { value = 7 };
            SaveRegistry.Register(counter);
            SaveRegistry.Unregister(counter);

            SaveSystem.Save(new SaveData());

            Assert.AreEqual(0, SaveSystem.Load().totalLeavesBagged);
        }
    }
}

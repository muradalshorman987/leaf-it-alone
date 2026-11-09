using System.IO;
using FallenYard.Save;
using NUnit.Framework;

namespace FallenYard.Tests.Save
{
    public class SaveMigrationTests
    {
        string _dir;

        [SetUp] public void SetUp() => _dir = TestUtil.UseTempSaveFolder();
        [TearDown] public void TearDown() => TestUtil.ReleaseTempSaveFolder(_dir);

        // A save exactly as the v1 build wrote it.
        const string V1Json = @"{
    ""version"": 1,
    ""coins"": 420,
    ""totalLeavesBagged"": 1234,
    ""tools"": [ { ""toolId"": ""rake"", ""level"": 2, ""unlocked"": false },
                { ""toolId"": ""blower"", ""level"": 0, ""unlocked"": true } ],
    ""completedYards"": [ ""yard_01"", ""yard_02"" ],
    ""mouseSensitivity"": 3.0
}";

        [Test]
        public void MigrationChain_CoversEveryVersion()
        {
            Assert.IsTrue(SaveMigrator.HasCompleteChain());
        }

        [Test]
        public void V1Json_MigratesToCurrent_KeepingEverything()
        {
            var data = SaveSystem.FromJson(V1Json, out int fileVersion);

            Assert.AreEqual(1, fileVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(420, data.coins);
            Assert.AreEqual(1234, data.totalLeavesBagged);
            Assert.AreEqual(2, data.GetTool("rake", false).level);
            Assert.IsTrue(data.GetTool("blower", false).unlocked);
            Assert.AreEqual(3f, data.mouseSensitivity);
            CollectionAssert.AreEqual(new[] { "yard_01", "yard_02" }, data.completedYards);
        }

        [Test]
        public void V1ToV2_SeedsUnlocksAndRecordsFromCompletedYards()
        {
            var data = SaveSystem.FromJson(V1Json, out _);

            CollectionAssert.AreEquivalent(new[] { "yard_01", "yard_02" }, data.unlockedYards);
            Assert.AreEqual(1, data.GetYardRecord("yard_01", false).timesCompleted);
            Assert.AreEqual(1f, data.GetYardRecord("yard_02", false).bestCleanFraction);
            Assert.AreEqual("yard_02", data.lastYardId);
        }

        [Test]
        public void JsonWithoutVersion_IsTreatedAsVersionZero()
        {
            var data = SaveSystem.FromJson(@"{ ""coins"": 5, ""completedYards"": [""yard_01""] }", out int fileVersion);

            Assert.AreEqual(0, fileVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(5, data.coins);
            Assert.IsTrue(data.IsYardUnlocked("yard_01"));
        }

        [Test]
        public void Migrate_RepairsBadValues()
        {
            var data = new SaveData { version = 1, coins = -50, playTimeSeconds = double.NaN, lastYardId = null };
            data.tools.Add(null);
            data.tools.Add(new ToolSaveEntry { toolId = "rake", level = -3 });
            data.completedYards.Add("");
            data.yardRecords.Add(new YardRecord { yardId = "yard_01", bestCleanFraction = 7f, timesCompleted = -1 });

            data.Migrate();

            Assert.AreEqual(0, data.coins);
            Assert.AreEqual(0.0, data.playTimeSeconds);
            Assert.AreEqual("", data.lastYardId);
            Assert.AreEqual(1, data.tools.Count);
            Assert.AreEqual(0, data.tools[0].level);
            Assert.IsEmpty(data.completedYards);
            Assert.AreEqual(1f, data.yardRecords[0].bestCleanFraction);
            Assert.AreEqual(0, data.yardRecords[0].timesCompleted);
        }

        [Test]
        public void Migrate_CurrentVersion_IsANoOp()
        {
            var data = new SaveData { coins = 10 };
            data.UnlockYard("yard_01");
            int from = SaveMigrator.Migrate(data);
            Assert.AreEqual(SaveData.CurrentVersion, from);
            Assert.AreEqual(10, data.coins);
            CollectionAssert.AreEqual(new[] { "yard_01" }, data.unlockedYards);
        }

        [Test]
        public void Load_OldFile_KeepsAVersionBackupAndReportsVersion()
        {
            File.WriteAllText(SaveSystem.FilePath, V1Json);

            var data = SaveSystem.Load();

            Assert.AreEqual(420, data.coins);
            Assert.AreEqual(SaveLoadSource.Main, SaveSystem.LastLoadSource);
            Assert.AreEqual(1, SaveSystem.LastLoadedFileVersion);
            Assert.IsTrue(File.Exists(SaveSystem.VersionBackupPath(1)));
            Assert.AreEqual(V1Json, File.ReadAllText(SaveSystem.VersionBackupPath(1)));
        }

        [Test]
        public void Load_ThenSave_WritesCurrentVersion()
        {
            File.WriteAllText(SaveSystem.FilePath, V1Json);
            SaveSystem.Save(SaveSystem.Load());

            SaveSystem.FromJson(File.ReadAllText(SaveSystem.FilePath), out int fileVersion);
            Assert.AreEqual(SaveData.CurrentVersion, fileVersion);
        }

        [Test]
        public void Load_FileFromNewerBuild_IsReadAndBackedUp()
        {
            int future = SaveData.CurrentVersion + 1;
            File.WriteAllText(SaveSystem.FilePath, "{ \"version\": " + future + ", \"coins\": 77, \"someFutureField\": 1 }");

            var data = SaveSystem.Load();

            Assert.AreEqual(77, data.coins);
            Assert.AreEqual(future, data.version, "Must not pretend a newer file is ours.");
            Assert.IsTrue(File.Exists(SaveSystem.VersionBackupPath(future)));
        }
    }
}

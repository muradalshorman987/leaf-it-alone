using System.IO;
using FallenYard.Save;
using NUnit.Framework;

namespace FallenYard.Tests.Save
{
    public class SaveSystemTests
    {
        string _dir;

        [SetUp] public void SetUp() => _dir = TestUtil.UseTempSaveFolder();
        [TearDown] public void TearDown() => TestUtil.ReleaseTempSaveFolder(_dir);

        [Test]
        public void FilePath_UsesOverrideFolder()
        {
            StringAssert.StartsWith(_dir, SaveSystem.FilePath);
        }

        [Test]
        public void Load_WithNoFile_ReturnsFreshData()
        {
            var data = SaveSystem.Load();
            Assert.IsNotNull(data);
            Assert.AreEqual(0, data.coins);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.IsEmpty(data.tools);
            Assert.IsEmpty(data.completedYards);
        }

        [Test]
        public void SaveThenLoad_RoundTripsEveryField()
        {
            var data = new SaveData { coins = 321, totalLeavesBagged = 9876543210L, mouseSensitivity = 3.5f };
            data.GetTool("rake", true).level = 2;
            var blower = data.GetTool("blower", true);
            blower.unlocked = true;
            blower.level = 1;
            data.completedYards.Add("yard_01");

            SaveSystem.Save(data);
            var loaded = SaveSystem.Load();

            Assert.AreEqual(321, loaded.coins);
            Assert.AreEqual(9876543210L, loaded.totalLeavesBagged);
            Assert.AreEqual(3.5f, loaded.mouseSensitivity);
            Assert.AreEqual(2, loaded.GetTool("rake", false).level);
            Assert.IsTrue(loaded.GetTool("blower", false).unlocked);
            Assert.AreEqual(1, loaded.GetTool("blower", false).level);
            CollectionAssert.AreEqual(new[] { "yard_01" }, loaded.completedYards);
        }

        [Test]
        public void Save_LeavesNoTempFileBehind()
        {
            SaveSystem.Save(new SaveData { coins = 1 });
            SaveSystem.Save(new SaveData { coins = 2 }); // overwrite path
            Assert.IsTrue(File.Exists(SaveSystem.FilePath));
            Assert.IsFalse(File.Exists(SaveSystem.FilePath + ".tmp"));
            Assert.AreEqual(2, SaveSystem.Load().coins);
        }

        [Test]
        public void Load_CorruptFile_FallsBackToFreshData()
        {
            File.WriteAllText(SaveSystem.FilePath, "{ this is not json");
            var data = SaveSystem.Load();
            Assert.IsNotNull(data);
            Assert.AreEqual(0, data.coins);
        }

        [Test]
        public void Delete_RemovesFile()
        {
            SaveSystem.Save(new SaveData());
            SaveSystem.Delete();
            Assert.IsFalse(File.Exists(SaveSystem.FilePath));
        }
    }
}

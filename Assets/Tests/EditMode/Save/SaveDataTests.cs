using FallenYard.Save;
using NUnit.Framework;

namespace FallenYard.Tests.Save
{
    public class SaveDataTests
    {
        [Test]
        public void GetTool_WithoutCreate_ReturnsNullForUnknownTool()
        {
            var data = new SaveData();
            Assert.IsNull(data.GetTool("rake", false));
            Assert.IsEmpty(data.tools);
        }

        [Test]
        public void GetTool_WithCreate_AddsOnceAndReturnsSameEntry()
        {
            var data = new SaveData();
            var a = data.GetTool("rake", true);
            var b = data.GetTool("rake", true);
            Assert.AreSame(a, b);
            Assert.AreEqual(1, data.tools.Count);
            Assert.AreEqual("rake", a.toolId);
        }

        [Test]
        public void Migrate_FillsMissingListsAndBumpsVersion()
        {
            var data = new SaveData { version = 0, tools = null, completedYards = null };
            var migrated = data.Migrate();
            Assert.AreSame(data, migrated);
            Assert.IsNotNull(migrated.tools);
            Assert.IsNotNull(migrated.completedYards);
            Assert.AreEqual(SaveData.CurrentVersion, migrated.version);
        }
    }
}

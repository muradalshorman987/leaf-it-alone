using FallenYard.Levels;
using FallenYard.Progression;
using FallenYard.Save;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Levels
{
    /// <summary>Shared fixture: the real five-yard catalog built from code.</summary>
    public static class TownFixture
    {
        public static YardCatalog Create()
        {
            var catalog = ScriptableObject.CreateInstance<YardCatalog>();
            catalog.yards = YardLayouts.CreateAll();
            catalog.unlockTable = ScriptableObject.CreateInstance<YardUnlockTable>();
            catalog.unlockTable.yards = YardLayouts.CreateUnlockRules();
            return catalog;
        }

        public static void Destroy(YardCatalog catalog)
        {
            if (catalog == null) return;
            foreach (var y in catalog.yards) Object.DestroyImmediate(y);
            Object.DestroyImmediate(catalog.unlockTable);
            Object.DestroyImmediate(catalog);
        }

        /// <summary>A save that has the default yards opened, like a fresh game after MetaProgressionService.Initialize.</summary>
        public static SaveData FreshSave(YardCatalog catalog)
        {
            var data = new SaveData();
            MetaProgression.RefreshUnlocks(data, catalog.unlockTable);
            return data;
        }
    }

    public class YardFlowTests
    {
        YardCatalog _catalog;

        [SetUp]
        public void SetUp() => _catalog = TownFixture.Create();

        [TearDown]
        public void TearDown() => TownFixture.Destroy(_catalog);

        [Test]
        public void Catalog_Validates()
        {
            CollectionAssert.IsEmpty(_catalog.Validate(), string.Join("; ", _catalog.Validate()));
        }

        [Test]
        public void Catalog_FindsYardsAndTheNextOne()
        {
            Assert.AreEqual(1, _catalog.IndexOf(YardLayouts.CornerLot));
            Assert.AreEqual(YardLayouts.Orchard, _catalog.NextAfter(YardLayouts.CornerLot).yardId);
            Assert.IsNull(_catalog.NextAfter(YardLayouts.HilltopManor));
            Assert.IsNull(_catalog.NextAfter("nowhere"));
            Assert.AreEqual(YardLayouts.ChurchGreen, _catalog.FindByScene("Yard_ChurchGreen").yardId);
            Assert.AreEqual("Yard_Orchard", YardFlow.SceneFor(_catalog, YardLayouts.Orchard));
            Assert.IsNull(YardFlow.SceneFor(_catalog, "nowhere"));
        }

        [Test]
        public void Catalog_ReportsMismatchWithTheUnlockTable()
        {
            _catalog.unlockTable.yards.RemoveAt(4);
            _catalog.unlockTable.yards.Add(new YardUnlockRule { yardId = "ghost_yard", requiredYards = new[] { YardLayouts.StarterLawn } });
            var problems = string.Join("\n", _catalog.Validate());
            StringAssert.Contains("hilltop_manor", problems);
            StringAssert.Contains("ghost_yard", problems);
        }

        [Test]
        public void NewGame_ContinuesAtTheStarterLawn()
        {
            var data = TownFixture.FreshSave(_catalog);
            Assert.AreEqual(YardLayouts.StarterLawn, YardFlow.ContinueTarget(data, _catalog));
            Assert.AreEqual(YardLayouts.StarterLawn, YardFlow.ContinueTarget(null, _catalog), "no save yet");
        }

        [Test]
        public void AfterTheStarterLawn_ContinueGoesToTheCornerLot()
        {
            var data = TownFixture.FreshSave(_catalog);
            MetaProgression.RecordCompletion(data, _catalog.unlockTable, YardLayouts.StarterLawn);
            Assert.AreEqual(YardLayouts.CornerLot, YardFlow.ContinueTarget(data, _catalog));
        }

        [Test]
        public void Continue_ReturnsToAnUnfinishedYardYouWereIn()
        {
            var data = TownFixture.FreshSave(_catalog);
            MetaProgression.RecordCompletion(data, _catalog.unlockTable, YardLayouts.StarterLawn);
            MetaProgression.RecordCompletion(data, _catalog.unlockTable, YardLayouts.CornerLot);
            data.UnlockYard(YardLayouts.Orchard);
            data.lastYardId = YardLayouts.Orchard;
            Assert.AreEqual(YardLayouts.Orchard, YardFlow.ContinueTarget(data, _catalog));
        }

        [Test]
        public void Continue_SkipsALockedLastYard()
        {
            var data = TownFixture.FreshSave(_catalog);
            data.lastYardId = YardLayouts.HilltopManor;
            Assert.AreEqual(YardLayouts.StarterLawn, YardFlow.ContinueTarget(data, _catalog));
        }

        [Test]
        public void Next_ReportsStatusOfTheFollowingYard()
        {
            var data = TownFixture.FreshSave(_catalog);
            var (next, status) = YardFlow.Next(data, _catalog, YardLayouts.StarterLawn);
            Assert.AreEqual(YardLayouts.CornerLot, next.yardId);
            Assert.AreEqual(YardStatus.Locked, status);

            MetaProgression.RecordCompletion(data, _catalog.unlockTable, YardLayouts.StarterLawn);
            Assert.AreEqual(YardStatus.Unlocked, YardFlow.Next(data, _catalog, YardLayouts.StarterLawn).status);

            MetaProgression.RecordCompletion(data, _catalog.unlockTable, YardLayouts.CornerLot);
            var orchard = YardFlow.Next(data, _catalog, YardLayouts.CornerLot);
            Assert.AreEqual(YardLayouts.Orchard, orchard.yard.yardId);
            Assert.AreEqual(YardStatus.Purchasable, orchard.status, "the orchard costs coins");

            Assert.IsNull(YardFlow.Next(data, _catalog, YardLayouts.HilltopManor).yard);
        }

        [Test]
        public void IsPlayable_NeedsTheYardInTheCatalog()
        {
            var data = TownFixture.FreshSave(_catalog);
            Assert.IsTrue(YardFlow.IsPlayable(data, _catalog, YardLayouts.StarterLawn));
            Assert.IsFalse(YardFlow.IsPlayable(data, _catalog, YardLayouts.CornerLot));
            data.UnlockYard("not_in_catalog");
            Assert.IsFalse(YardFlow.IsPlayable(data, _catalog, "not_in_catalog"));
        }
    }
}

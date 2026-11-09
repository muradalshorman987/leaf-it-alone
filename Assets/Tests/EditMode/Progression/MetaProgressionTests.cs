using System.Linq;
using FallenYard.Progression;
using FallenYard.Save;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Progression
{
    public class MetaProgressionTests
    {
        YardUnlockTable _table;
        SaveData _data;

        [SetUp]
        public void SetUp()
        {
            _table = ScriptableObject.CreateInstance<YardUnlockTable>();
            _table.yards.Add(new YardUnlockRule { yardId = "starter", unlockedByDefault = true, rewardToolIds = new[] { "blower" } });
            _table.yards.Add(new YardUnlockRule { yardId = "corner", requiredYards = new[] { "starter" } });
            _table.yards.Add(new YardUnlockRule { yardId = "orchard", requiredYards = new[] { "corner" }, unlockCost = 300, rewardToolIds = new[] { "vacuum" } });
            _table.yards.Add(new YardUnlockRule { yardId = "church", requiredCompletedCount = 2 });
            _data = new SaveData();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_table);

        [Test]
        public void TableIsValid()
        {
            CollectionAssert.IsEmpty(_table.Validate());
        }

        [Test]
        public void Validate_FindsDesignerMistakes()
        {
            var bad = ScriptableObject.CreateInstance<YardUnlockTable>();
            bad.yards.Add(new YardUnlockRule { yardId = "a", requiredYards = new[] { "a" } });
            bad.yards.Add(new YardUnlockRule { yardId = "a" });
            bad.yards.Add(new YardUnlockRule { yardId = "b", requiredYards = new[] { "ghost" }, unlockCost = -1 });

            var problems = bad.Validate();
            Object.DestroyImmediate(bad);

            Assert.IsTrue(problems.Any(p => p.Contains("Duplicate")));
            Assert.IsTrue(problems.Any(p => p.Contains("requires itself")));
            Assert.IsTrue(problems.Any(p => p.Contains("unknown yard 'ghost'")));
            Assert.IsTrue(problems.Any(p => p.Contains("negative")));
            Assert.IsTrue(problems.Any(p => p.Contains("No yard is unlocked by default")));
        }

        [Test]
        public void FreshSave_OnlyStarterIsPlayable()
        {
            var added = MetaProgression.RefreshUnlocks(_data, _table);

            CollectionAssert.AreEqual(new[] { "starter" }, added);
            Assert.AreEqual(YardStatus.Unlocked, MetaProgression.GetStatus(_data, _table, "starter"));
            Assert.AreEqual(YardStatus.Locked, MetaProgression.GetStatus(_data, _table, "corner"));
            Assert.AreEqual(YardStatus.Locked, MetaProgression.GetStatus(_data, _table, "orchard"));
            Assert.AreEqual(YardStatus.Locked, MetaProgression.GetStatus(_data, _table, "unknown"));
        }

        [Test]
        public void CompletingStarter_UnlocksCornerAndGrantsTool()
        {
            MetaProgression.RefreshUnlocks(_data, _table);
            var result = MetaProgression.RecordCompletion(_data, _table, "starter");

            Assert.IsTrue(result.firstCompletion);
            CollectionAssert.AreEqual(new[] { "corner" }, result.newlyUnlockedYards);
            CollectionAssert.AreEqual(new[] { "blower" }, result.grantedTools);
            Assert.IsTrue(_data.GetTool("blower", false).unlocked);
            Assert.AreEqual(YardStatus.Completed, MetaProgression.GetStatus(_data, _table, "starter"));
            Assert.IsTrue(MetaProgression.IsPlayable(_data, _table, "corner"));
        }

        [Test]
        public void SecondCompletion_DoesNotGrantAgain()
        {
            MetaProgression.RecordCompletion(_data, _table, "starter");
            var again = MetaProgression.RecordCompletion(_data, _table, "starter");

            Assert.IsFalse(again.firstCompletion);
            Assert.IsEmpty(again.grantedTools);
            Assert.IsEmpty(again.newlyUnlockedYards);
            Assert.AreEqual(2, _data.GetYardRecord("starter", false).timesCompleted);
            Assert.AreEqual(1, _data.completedYards.Count);
        }

        [Test]
        public void ToolAlreadyBought_IsNotReportedAsGranted()
        {
            _data.GetTool("blower", true).unlocked = true;
            var result = MetaProgression.RecordCompletion(_data, _table, "starter");
            Assert.IsEmpty(result.grantedTools);
        }

        [Test]
        public void CompletedCountRule_UnlocksAfterAnyTwoYards()
        {
            MetaProgression.RecordCompletion(_data, _table, "starter");
            Assert.AreEqual(YardStatus.Locked, MetaProgression.GetStatus(_data, _table, "church"));

            var result = MetaProgression.RecordCompletion(_data, _table, "corner");
            CollectionAssert.Contains(result.newlyUnlockedYards, "church");
        }

        [Test]
        public void PaidYard_BecomesPurchasable_ThenNeedsCoins()
        {
            MetaProgression.RecordCompletion(_data, _table, "starter");
            var result = MetaProgression.RecordCompletion(_data, _table, "corner");

            CollectionAssert.AreEqual(new[] { "orchard" }, result.newlyPurchasableYards);
            Assert.AreEqual(YardStatus.Purchasable, MetaProgression.GetStatus(_data, _table, "orchard"));
            Assert.IsFalse(MetaProgression.IsPlayable(_data, _table, "orchard"));

            int wallet = 100;
            bool TrySpend(int cost) { if (wallet < cost) return false; wallet -= cost; return true; }

            Assert.IsFalse(MetaProgression.TryPurchase(_data, _table, "orchard", TrySpend));
            Assert.AreEqual(100, wallet);

            wallet = 500;
            Assert.IsTrue(MetaProgression.TryPurchase(_data, _table, "orchard", TrySpend));
            Assert.AreEqual(200, wallet);
            Assert.AreEqual(YardStatus.Unlocked, MetaProgression.GetStatus(_data, _table, "orchard"));
            Assert.IsFalse(MetaProgression.TryPurchase(_data, _table, "orchard", TrySpend), "Can't buy twice.");
        }

        [Test]
        public void LockedYard_CannotBePurchased()
        {
            Assert.IsFalse(MetaProgression.TryPurchase(_data, _table, "orchard", _ => true));
        }

        [Test]
        public void RecordProgress_KeepsBestAndLastYard()
        {
            MetaProgression.RecordProgress(_data, "starter", 0.6f);
            MetaProgression.RecordProgress(_data, "starter", 0.4f);

            Assert.AreEqual(0.6f, _data.GetYardRecord("starter", false).bestCleanFraction);
            Assert.AreEqual("starter", _data.lastYardId);
        }

        [Test]
        public void OverallCompletion_CountsTableYards()
        {
            MetaProgression.RecordCompletion(_data, _table, "starter");
            Assert.AreEqual(0.25f, MetaProgression.OverallCompletion(_data, _table));
        }

        [Test]
        public void ProgressSurvivesSaveAndLoad()
        {
            string dir = TestUtil.UseTempSaveFolder();
            try
            {
                MetaProgression.RefreshUnlocks(_data, _table);
                MetaProgression.RecordCompletion(_data, _table, "starter");
                SaveSystem.Save(_data);

                var loaded = SaveSystem.Load();
                Assert.AreEqual(YardStatus.Completed, MetaProgression.GetStatus(loaded, _table, "starter"));
                Assert.AreEqual(YardStatus.Unlocked, MetaProgression.GetStatus(loaded, _table, "corner"));
                Assert.AreEqual(1, loaded.GetYardRecord("starter", false).timesCompleted);
                Assert.IsTrue(loaded.GetTool("blower", false).unlocked);
            }
            finally
            {
                TestUtil.ReleaseTempSaveFolder(dir);
            }
        }
    }
}

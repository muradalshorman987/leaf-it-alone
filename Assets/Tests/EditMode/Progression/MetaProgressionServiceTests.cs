using FallenYard.Core;
using FallenYard.Progression;
using FallenYard.Save;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Progression
{
    public class MetaProgressionServiceTests
    {
        string _dir;
        GameManager _gm;
        YardUnlockTable _table;
        MetaProgressionService _service;

        [SetUp]
        public void SetUp()
        {
            GameEvents.ClearAllSubscribers();
            SaveRegistry.Clear();
            _dir = TestUtil.UseTempSaveFolder();
            _gm = TestUtil.CreateGameManager();

            _table = ScriptableObject.CreateInstance<YardUnlockTable>();
            _table.yards.Add(new YardUnlockRule { yardId = "starter", unlockedByDefault = true, rewardToolIds = new[] { "blower" } });
            _table.yards.Add(new YardUnlockRule { yardId = "corner", requiredYards = new[] { "starter" } });
            _table.yards.Add(new YardUnlockRule { yardId = "orchard", requiredYards = new[] { "corner" }, unlockCost = 300 });

            _service = new GameObject("MetaProgressionService (test)").AddComponent<MetaProgressionService>();
            _service.unlockTable = _table;
            _service.currentYardId = "starter";
            TestUtil.Call(_service, "Awake");
            TestUtil.Call(_service, "OnEnable");
            _service.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            if (_service != null) TestUtil.Call(_service, "OnDisable");
            TestUtil.DestroySingleton(_service);
            Object.DestroyImmediate(_table);
            TestUtil.DestroySingleton(_gm);
            TestUtil.ReleaseTempSaveFolder(_dir);
            SaveRegistry.Clear();
            GameEvents.ClearAllSubscribers();
        }

        [Test]
        public void Initialize_UnlocksDefaultYards()
        {
            Assert.IsTrue(_gm.Data.IsYardUnlocked("starter"));
            Assert.AreEqual("starter", _gm.Data.lastYardId);
            Assert.AreEqual(3, _service.GetAllYards().Count);
        }

        [Test]
        public void YardCompletedEvent_UnlocksNextYardGrantsToolAndSaves()
        {
            string unlocked = null, granted = null;
            _service.YardUnlocked += y => unlocked = y;
            _service.ToolGranted += t => granted = t;

            _gm.MarkYardCompleted("starter"); // raises GameEvents.YardCompleted

            Assert.AreEqual("corner", unlocked);
            Assert.AreEqual("blower", granted);
            Assert.IsTrue(_service.LastCompletion.firstCompletion);

            var onDisk = SaveSystem.Load();
            Assert.IsTrue(onDisk.IsYardUnlocked("corner"));
            Assert.IsTrue(onDisk.GetTool("blower", false).unlocked);
        }

        [Test]
        public void CoinsEarned_AreCountedPerYardAndLifetime()
        {
            _gm.AddCoins(50);
            _gm.TrySpend(30);
            _gm.AddCoins(10);

            Assert.AreEqual(60, _gm.Data.lifetimeCoinsEarned);
            Assert.AreEqual(60, _gm.Data.GetYardRecord("starter", false).coinsEarned);
        }

        [Test]
        public void CleanProgress_IsRecorded()
        {
            GameEvents.RaiseYardCleanChanged(0.4f);
            GameEvents.RaiseYardCleanChanged(0.3f);
            Assert.AreEqual(0.4f, _service.GetRecord("starter").bestCleanFraction, 1e-6f);
        }

        [Test]
        public void TryPurchaseYard_SpendsCoins()
        {
            _gm.MarkYardCompleted("starter");
            _gm.MarkYardCompleted("corner");
            Assert.AreEqual(YardStatus.Purchasable, _service.GetStatus("orchard"));

            Assert.IsFalse(_service.TryPurchaseYard("orchard"));
            _gm.AddCoins(300);
            Assert.IsTrue(_service.TryPurchaseYard("orchard"));
            Assert.AreEqual(0, _gm.Coins);
            Assert.IsTrue(_service.IsPlayable("orchard"));
        }
    }
}

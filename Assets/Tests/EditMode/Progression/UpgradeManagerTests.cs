using FallenYard.Core;
using FallenYard.Progression;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Progression
{
    public class UpgradeManagerTests
    {
        string _saveDir;
        GameManager _gm;
        UpgradeManager _upgrades;
        ToolUpgradeDefinition _rake, _blower;

        [SetUp]
        public void SetUp()
        {
            GameEvents.ClearAllSubscribers();
            _saveDir = TestUtil.UseTempSaveFolder();
            _gm = TestUtil.CreateGameManager();

            _rake = ScriptableObject.CreateInstance<ToolUpgradeDefinition>();
            _rake.toolId = "rake";
            _rake.unlockedByDefault = true;
            _rake.levels = new[]
            {
                new ToolUpgradeDefinition.UpgradeLevel { cost = 50, radiusMultiplier = 1.25f },
                new ToolUpgradeDefinition.UpgradeLevel { cost = 120, radiusMultiplier = 1.6f },
            };

            _blower = ScriptableObject.CreateInstance<ToolUpgradeDefinition>();
            _blower.toolId = "blower";
            _blower.unlockedByDefault = false;
            _blower.unlockCost = 200;
            _blower.levels = new[] { new ToolUpgradeDefinition.UpgradeLevel { cost = 80, strengthMultiplier = 1.5f } };

            _upgrades = new GameObject("UpgradeManager (test)").AddComponent<UpgradeManager>();
            _upgrades.definitions = new[] { _rake, _blower };
            TestUtil.Call(_upgrades, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            TestUtil.DestroySingleton(_upgrades);
            TestUtil.DestroySingleton(_gm);
            Object.DestroyImmediate(_rake);
            Object.DestroyImmediate(_blower);
            TestUtil.ReleaseTempSaveFolder(_saveDir);
            GameEvents.ClearAllSubscribers();
        }

        [Test]
        public void BaseTool_HasLevelZeroAndNeutralMultipliers()
        {
            Assert.AreEqual(0, _upgrades.GetLevel("rake"));
            Assert.AreEqual(1f, _upgrades.GetMultiplier("rake", UpgradeStat.Radius));
            Assert.AreEqual(50, _upgrades.NextUpgradeCost("rake"));
            Assert.AreEqual(2, _upgrades.MaxLevel("rake"));
        }

        [Test]
        public void UnknownTool_IsUnlockedWithNoUpgrades()
        {
            Assert.IsTrue(_upgrades.IsUnlocked("mystery"));
            Assert.AreEqual(-1, _upgrades.NextUpgradeCost("mystery"));
            Assert.AreEqual(1f, _upgrades.GetMultiplier("mystery", UpgradeStat.Strength));
            Assert.IsFalse(_upgrades.TryUpgrade("mystery"));
        }

        [Test]
        public void TryUpgrade_WithoutCoins_Fails()
        {
            Assert.IsFalse(_upgrades.TryUpgrade("rake"));
            Assert.AreEqual(0, _upgrades.GetLevel("rake"));
        }

        [Test]
        public void TryUpgrade_SpendsCoinsRaisesLevelAndEvents()
        {
            _gm.AddCoins(100);
            string upgradedTool = null;
            int upgradedLevel = 0, coinsReported = -1;
            GameEvents.ToolUpgraded += (id, l) => { upgradedTool = id; upgradedLevel = l; };
            GameEvents.CoinsChanged += c => coinsReported = c;

            Assert.IsTrue(_upgrades.TryUpgrade("rake"));

            Assert.AreEqual(1, _upgrades.GetLevel("rake"));
            Assert.AreEqual(50, _gm.Coins);
            Assert.AreEqual(1.25f, _upgrades.GetMultiplier("rake", UpgradeStat.Radius));
            Assert.AreEqual(120, _upgrades.NextUpgradeCost("rake"));
            Assert.AreEqual("rake", upgradedTool);
            Assert.AreEqual(1, upgradedLevel);
            Assert.AreEqual(50, coinsReported);
        }

        [Test]
        public void MaxedTool_CannotUpgradeFurther()
        {
            _gm.AddCoins(1000);
            Assert.IsTrue(_upgrades.TryUpgrade("rake"));
            Assert.IsTrue(_upgrades.TryUpgrade("rake"));
            Assert.AreEqual(-1, _upgrades.NextUpgradeCost("rake"));
            Assert.IsFalse(_upgrades.TryUpgrade("rake"));
            Assert.AreEqual(1000 - 50 - 120, _gm.Coins);
            Assert.AreEqual(1.6f, _upgrades.GetMultiplier("rake", UpgradeStat.Radius));
        }

        [Test]
        public void LockedTool_MustBeUnlockedBeforeUpgrading()
        {
            _gm.AddCoins(500);
            Assert.IsFalse(_upgrades.IsUnlocked("blower"));
            Assert.IsFalse(_upgrades.TryUpgrade("blower"));

            Assert.IsTrue(_upgrades.TryUnlock("blower"));
            Assert.IsTrue(_upgrades.IsUnlocked("blower"));
            Assert.AreEqual(300, _gm.Coins);
            Assert.IsFalse(_upgrades.TryUnlock("blower"), "unlocking twice should fail");

            Assert.IsTrue(_upgrades.TryUpgrade("blower"));
            Assert.AreEqual(1.5f, _upgrades.GetMultiplier("blower", UpgradeStat.Strength));
        }

        [Test]
        public void UpgradeLevel_GetReturnsMatchingStat()
        {
            var level = new ToolUpgradeDefinition.UpgradeLevel
            {
                radiusMultiplier = 1.1f, strengthMultiplier = 1.2f, rangeMultiplier = 1.3f, capacityMultiplier = 1.4f,
            };
            Assert.AreEqual(1.1f, level.Get(UpgradeStat.Radius));
            Assert.AreEqual(1.2f, level.Get(UpgradeStat.Strength));
            Assert.AreEqual(1.3f, level.Get(UpgradeStat.Range));
            Assert.AreEqual(1.4f, level.Get(UpgradeStat.Capacity));
        }
    }
}

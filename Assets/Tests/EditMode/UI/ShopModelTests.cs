using System.Collections.Generic;
using FallenYard.Core;
using FallenYard.Progression;
using FallenYard.UI;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.UI
{
    public class ShopModelTests
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
            _rake.displayName = "Rake";
            _rake.unlockedByDefault = true;
            _rake.levels = new[]
            {
                new ToolUpgradeDefinition.UpgradeLevel { cost = 60, radiusMultiplier = 1.3f, strengthMultiplier = 1.15f },
                new ToolUpgradeDefinition.UpgradeLevel { cost = 150, radiusMultiplier = 1.6f, strengthMultiplier = 1.15f },
            };

            _blower = ScriptableObject.CreateInstance<ToolUpgradeDefinition>();
            _blower.toolId = "blower";
            _blower.displayName = "Leaf Blower";
            _blower.unlockedByDefault = false;
            _blower.unlockCost = 200;
            _blower.levels = new[] { new ToolUpgradeDefinition.UpgradeLevel { cost = 80, strengthMultiplier = 1.5f } };

            _upgrades = new GameObject("UpgradeManager (test)").AddComponent<UpgradeManager>();
            _upgrades.definitions = new[] { _rake, null, _blower };
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

        List<ShopRow> Rows() => ShopModel.BuildRows(_upgrades, _gm.Coins);

        [Test]
        public void BuildRows_NullManager_IsEmpty()
        {
            Assert.IsEmpty(ShopModel.BuildRows(null, 100));
        }

        [Test]
        public void BuildRows_SkipsNullDefinitions_AndKeepsOrder()
        {
            var rows = Rows();
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("rake", rows[0].ToolId);
            Assert.AreEqual("blower", rows[1].ToolId);
        }

        [Test]
        public void UnlockedTool_OffersUpgradeWithEffect()
        {
            var rake = Rows()[0];
            Assert.AreEqual(ShopAction.Upgrade, rake.Action);
            Assert.AreEqual(60, rake.Cost);
            Assert.AreEqual("Rake", rake.Title);
            Assert.AreEqual("Lv 0 / 2", rake.LevelText);
            Assert.AreEqual("+30% reach, +15% power", rake.EffectText);
            Assert.AreEqual("Upgrade  60", rake.ButtonText);
        }

        [Test]
        public void LockedTool_OffersUnlock()
        {
            var blower = Rows()[1];
            Assert.AreEqual(ShopAction.Unlock, blower.Action);
            Assert.AreEqual(200, blower.Cost);
            Assert.AreEqual("Leaf Blower", blower.Title);
        }

        [Test]
        public void CanAfford_FollowsCoins()
        {
            Assert.IsFalse(Rows()[0].Interactable);
            _gm.AddCoins(60);
            Assert.IsTrue(Rows()[0].Interactable);
            Assert.IsFalse(Rows()[1].Interactable);
        }

        [Test]
        public void TryBuy_Upgrade_SpendsAndAdvances()
        {
            _gm.AddCoins(100);
            Assert.IsTrue(ShopModel.TryBuy(_upgrades, Rows()[0]));
            Assert.AreEqual(40, _gm.Coins);

            var rake = Rows()[0];
            Assert.AreEqual("Lv 1 / 2", rake.LevelText);
            Assert.AreEqual(150, rake.Cost);
            Assert.AreEqual("+23% reach", rake.EffectText, "second level compares against the first, not base");
        }

        [Test]
        public void TryBuy_Unlock_ThenOffersUpgrade()
        {
            _gm.AddCoins(250);
            Assert.IsTrue(ShopModel.TryBuy(_upgrades, Rows()[1]));
            Assert.AreEqual(50, _gm.Coins);
            Assert.AreEqual(ShopAction.Upgrade, Rows()[1].Action);
        }

        [Test]
        public void TryBuy_NotAffordable_DoesNothing()
        {
            _gm.AddCoins(10);
            Assert.IsFalse(ShopModel.TryBuy(_upgrades, Rows()[0]));
            Assert.AreEqual(10, _gm.Coins);
        }

        [Test]
        public void MaxedTool_IsNotInteractable()
        {
            _gm.AddCoins(1000);
            ShopModel.TryBuy(_upgrades, Rows()[0]);
            ShopModel.TryBuy(_upgrades, Rows()[0]);
            var rake = Rows()[0];
            Assert.AreEqual(ShopAction.Maxed, rake.Action);
            Assert.AreEqual(-1, rake.Cost);
            Assert.AreEqual("Maxed", rake.ButtonText);
            Assert.IsFalse(rake.Interactable);
            Assert.IsFalse(ShopModel.TryBuy(_upgrades, rake));
        }

        [Test]
        public void DescribeNextLevel_OutOfRange_IsEmpty()
        {
            Assert.AreEqual("", ShopModel.DescribeNextLevel(_rake, 2));
            Assert.AreEqual("", ShopModel.DescribeNextLevel(null, 0));
        }
    }
}

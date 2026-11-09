using FallenYard.Core;
using FallenYard.Tools;
using NUnit.Framework;

namespace FallenYard.Tests.Tools
{
    public class LeafPayoutTests
    {
        string _saveDir;
        GameManager _gm;

        [SetUp]
        public void SetUp()
        {
            GameEvents.ClearAllSubscribers();
            _saveDir = TestUtil.UseTempSaveFolder();
            _gm = TestUtil.CreateGameManager();
        }

        [TearDown]
        public void TearDown()
        {
            TestUtil.DestroySingleton(_gm);
            TestUtil.ReleaseTempSaveFolder(_saveDir);
            GameEvents.ClearAllSubscribers();
        }

        [Test]
        public void Pay_AddsWholeCoins_AndCarriesTheRemainder()
        {
            var payout = new LeafPayout();
            int start = _gm.Coins;

            Assert.AreEqual(2, payout.Pay(5, 0.5f));
            Assert.AreEqual(0.5f, payout.Remainder, 1e-4f);
            Assert.AreEqual(3, payout.Pay(5, 0.5f), "the half coin carries over");
            Assert.AreEqual(start + 5, _gm.Coins);
            Assert.AreEqual(10, _gm.Data.totalLeavesBagged);
        }

        [Test]
        public void Pay_RaisesPileBagged()
        {
            int leaves = 0, coins = -1;
            GameEvents.PileBagged += (l, c) => { leaves = l; coins = c; };
            new LeafPayout().Pay(120, 0.5f);
            Assert.AreEqual(120, leaves);
            Assert.AreEqual(60, coins);
        }

        [Test]
        public void Pay_NothingForZeroLeaves()
        {
            bool raised = false;
            GameEvents.PileBagged += (l, c) => raised = true;
            Assert.AreEqual(0, new LeafPayout().Pay(0, 0.5f));
            Assert.IsFalse(raised);
        }
    }
}

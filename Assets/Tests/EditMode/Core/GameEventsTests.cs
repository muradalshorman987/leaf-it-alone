using FallenYard.Core;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Core
{
    public class GameEventsTests
    {
        [SetUp, TearDown]
        public void Clear() => GameEvents.ClearAllSubscribers();

        [Test]
        public void RaiseCoinsChanged_DeliversTotal()
        {
            int received = -1;
            GameEvents.CoinsChanged += v => received = v;
            GameEvents.RaiseCoinsChanged(42);
            Assert.AreEqual(42, received);
        }

        [Test]
        public void RaisePileBagged_DeliversLeavesAndCoins()
        {
            int leaves = 0, coins = 0;
            GameEvents.PileBagged += (l, c) => { leaves = l; coins = c; };
            GameEvents.RaisePileBagged(120, 30);
            Assert.AreEqual(120, leaves);
            Assert.AreEqual(30, coins);
        }

        [Test]
        public void FractionEvents_AreClampedToZeroOne()
        {
            float clean = -1f, throttle = -1f;
            GameEvents.YardCleanChanged += v => clean = v;
            GameEvents.ToolThrottle += v => throttle = v;
            GameEvents.RaiseYardCleanChanged(1.5f);
            GameEvents.RaiseToolThrottle(-0.2f);
            Assert.AreEqual(1f, clean);
            Assert.AreEqual(0f, throttle);
        }

        [Test]
        public void StringAndVectorEvents_DeliverPayload()
        {
            string equipped = null, upgraded = null, completed = null;
            int level = 0;
            Vector3 dir = Vector3.zero;
            float strength = 0f;
            GamePhase phase = GamePhase.Boot;

            GameEvents.ToolEquipped += id => equipped = id;
            GameEvents.ToolUpgraded += (id, l) => { upgraded = id; level = l; };
            GameEvents.YardCompleted += id => completed = id;
            GameEvents.WindGust += (d, s) => { dir = d; strength = s; };
            GameEvents.StateChanged += p => phase = p;

            GameEvents.RaiseToolEquipped("rake");
            GameEvents.RaiseToolUpgraded("blower", 2);
            GameEvents.RaiseYardCompleted("yard_01");
            GameEvents.RaiseWindGust(Vector3.right, 0.7f);
            GameEvents.RaiseStateChanged(GamePhase.Shop);

            Assert.AreEqual("rake", equipped);
            Assert.AreEqual("blower", upgraded);
            Assert.AreEqual(2, level);
            Assert.AreEqual("yard_01", completed);
            Assert.AreEqual(Vector3.right, dir);
            Assert.AreEqual(0.7f, strength);
            Assert.AreEqual(GamePhase.Shop, phase);
        }

        [Test]
        public void Raise_WithNoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                GameEvents.RaiseCoinsChanged(1);
                GameEvents.RaisePileBagged(1, 1);
                GameEvents.RaiseStateChanged(GamePhase.Playing);
            });
        }

        [Test]
        public void ClearAllSubscribers_RemovesListeners()
        {
            int calls = 0;
            GameEvents.CoinsChanged += _ => calls++;
            GameEvents.ClearAllSubscribers();
            GameEvents.RaiseCoinsChanged(5);
            Assert.AreEqual(0, calls);
        }
    }
}

using FallenYard.Core;
using FallenYard.Leaves;
using FallenYard.Progression;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Progression
{
    public class YardProgressTests
    {
        string _saveDir;
        GameManager _gm;
        LeafField _field;
        YardProgress _yard;

        [SetUp]
        public void SetUp()
        {
            GameEvents.ClearAllSubscribers();
            _saveDir = TestUtil.UseTempSaveFolder();
            _gm = TestUtil.CreateGameManager();
            _field = TestUtil.CreateLeafField(new Vector2(4f, 4f), 0.5f, 400);
            _yard = NewYard();
        }

        [TearDown]
        public void TearDown()
        {
            TestUtil.Destroy(_yard);
            TestUtil.DestroyLeafField(_field);
            TestUtil.DestroySingleton(_gm);
            TestUtil.ReleaseTempSaveFolder(_saveDir);
            GameEvents.ClearAllSubscribers();
        }

        static YardProgress NewYard()
        {
            var y = new GameObject("YardProgress (test)").AddComponent<YardProgress>();
            y.yardId = "yard_test";
            y.completeAtFraction = 0.95f;
            y.completionBonus = 250;
            return y;
        }

        [Test]
        public void DirtyYard_IsNotComplete()
        {
            TestUtil.Call(_yard, "Update");
            Assert.IsFalse(_yard.IsComplete);
            Assert.AreEqual(0f, _yard.CleanFraction);
            Assert.AreEqual(0, _gm.Coins);
        }

        [Test]
        public void Update_ReportsCleanFractionThroughGameEvents()
        {
            float reported = -1f;
            GameEvents.YardCleanChanged += f => reported = f;

            TestUtil.Call(_yard, "Update");
            Assert.AreEqual(0f, reported);

            _field.CollectInRadius(Vector3.zero, 10f, 100);
            TestUtil.Call(_yard, "Update");
            Assert.AreEqual(0.25f, reported, 1e-4f);
        }

        [Test]
        public void CleanYard_CompletesOnceAndPaysBonus()
        {
            bool cleanedEvent = false;
            int completedEvents = 0;
            _yard.OnYardCleaned += () => cleanedEvent = true;
            GameEvents.YardCompleted += _ => completedEvents++;

            _field.CollectInRadius(Vector3.zero, 10f);
            TestUtil.Call(_yard, "Update");
            TestUtil.Call(_yard, "Update");

            Assert.IsTrue(_yard.IsComplete);
            Assert.IsTrue(cleanedEvent);
            Assert.AreEqual(250, _gm.Coins);
            Assert.IsTrue(_gm.IsYardCompleted("yard_test"));
            Assert.AreEqual(1, completedEvents);
        }

        [Test]
        public void ReplayingFinishedYard_DoesNotPayBonusAgain()
        {
            _field.CollectInRadius(Vector3.zero, 10f);
            TestUtil.Call(_yard, "Update");
            Assert.AreEqual(250, _gm.Coins);

            var replay = NewYard();
            try
            {
                TestUtil.Call(replay, "Update");
                Assert.IsTrue(replay.IsComplete);
                Assert.AreEqual(250, _gm.Coins);
            }
            finally
            {
                TestUtil.Destroy(replay);
            }
        }
    }
}

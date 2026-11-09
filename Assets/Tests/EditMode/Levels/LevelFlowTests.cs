using System.Collections.Generic;
using FallenYard.Core;
using FallenYard.Levels;
using FallenYard.Progression;
using FallenYard.Save;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Levels
{
    public class LevelFlowTests
    {
        string _dir;
        GameManager _gm;
        YardCatalog _catalog;
        LevelFlow _flow;
        readonly List<string> _loaded = new List<string>();
        bool _loaderSucceeds;

        [SetUp]
        public void SetUp()
        {
            GameEvents.ClearAllSubscribers();
            GameState.SkipTitleOnNextLoad = false;
            _dir = TestUtil.UseTempSaveFolder();
            _gm = TestUtil.CreateGameManager();
            _catalog = TownFixture.Create();
            MetaProgression.RefreshUnlocks(_gm.Data, _catalog.unlockTable);

            _flow = new GameObject("LevelFlow (test)").AddComponent<LevelFlow>();
            _flow.catalog = _catalog;
            _flow.currentYardId = YardLayouts.StarterLawn;
            _loaded.Clear();
            _loaderSucceeds = true;
            _flow.sceneLoader = scene =>
            {
                _loaded.Add(scene);
                return _loaderSucceeds;
            };
            TestUtil.Call(_flow, "Awake");
            TestUtil.Call(_flow, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            if (_flow != null) TestUtil.Call(_flow, "OnDisable");
            TestUtil.DestroySingleton(_flow);
            TownFixture.Destroy(_catalog);
            TestUtil.DestroySingleton(_gm);
            TestUtil.ReleaseTempSaveFolder(_dir);
            GameState.SkipTitleOnNextLoad = false;
            GameEvents.ClearAllSubscribers();
        }

        [Test]
        public void Travel_ToAnOpenYard_SavesFlagsAndLoadsItsScene()
        {
            MetaProgression.RecordCompletion(_gm.Data, _catalog.unlockTable, YardLayouts.StarterLawn);
            Assert.IsTrue(_flow.TryTravel(YardLayouts.CornerLot));
            CollectionAssert.AreEqual(new[] { "Yard_CornerLot" }, _loaded);
            Assert.IsTrue(GameState.SkipTitleOnNextLoad);
            Assert.AreEqual(YardLayouts.CornerLot, _gm.Data.lastYardId);
            Assert.AreEqual(YardLayouts.CornerLot, SaveSystem.Load().lastYardId, "saved before leaving");
        }

        [Test]
        public void Travel_RefusesLockedUnknownAndCurrentYards()
        {
            Assert.IsFalse(_flow.TryTravel(YardLayouts.HilltopManor));
            Assert.IsFalse(_flow.TryTravel("nowhere"));
            Assert.IsFalse(_flow.TryTravel(YardLayouts.StarterLawn));
            Assert.IsFalse(_flow.TryTravel(null));
            CollectionAssert.IsEmpty(_loaded);
            Assert.IsFalse(GameState.SkipTitleOnNextLoad);
        }

        [Test]
        public void Travel_ClearsTheFlagWhenTheSceneCantLoad()
        {
            MetaProgression.RecordCompletion(_gm.Data, _catalog.unlockTable, YardLayouts.StarterLawn);
            _loaderSucceeds = false;
            Assert.IsFalse(_flow.TryTravel(YardLayouts.CornerLot));
            Assert.IsFalse(GameState.SkipTitleOnNextLoad);
        }

        [Test]
        public void TravelRequestedEvent_Travels()
        {
            MetaProgression.RecordCompletion(_gm.Data, _catalog.unlockTable, YardLayouts.StarterLawn);
            GameEvents.RaiseYardTravelRequested(YardLayouts.CornerLot);
            CollectionAssert.AreEqual(new[] { "Yard_CornerLot" }, _loaded);
        }

        [Test]
        public void ContinueElsewhere_OnlyWhenTheSavePointsAway()
        {
            Assert.IsFalse(_flow.TryContinueElsewhere(), "new game: play the starter lawn right here");
            MetaProgression.RecordCompletion(_gm.Data, _catalog.unlockTable, YardLayouts.StarterLawn);
            Assert.AreEqual(YardLayouts.CornerLot, _flow.ContinueTarget);
            Assert.IsTrue(_flow.TryContinueElsewhere());
            CollectionAssert.AreEqual(new[] { "Yard_CornerLot" }, _loaded);
        }

        [Test]
        public void NextYard_FollowsTheCurrentOne()
        {
            Assert.AreEqual(YardLayouts.CornerLot, _flow.NextYard.yard.yardId);
            Assert.AreEqual(YardLayouts.StarterLawn, _flow.CurrentYard.yardId);
            Assert.AreEqual(YardStatus.Unlocked, _flow.GetStatus(YardLayouts.StarterLawn));
        }

        [Test]
        public void ConsumeSkipTitle_ReadsOnce()
        {
            GameState.SkipTitleOnNextLoad = true;
            Assert.IsTrue(GameState.ConsumeSkipTitle());
            Assert.IsFalse(GameState.ConsumeSkipTitle());
        }
    }
}

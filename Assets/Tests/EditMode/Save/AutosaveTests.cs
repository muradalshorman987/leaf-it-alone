using FallenYard.Core;
using FallenYard.Save;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Save
{
    public class AutosaveSchedulerTests
    {
        [Test]
        public void Periodic_FiresAfterInterval()
        {
            var s = new AutosaveScheduler(interval: 60f, minGap: 5f, now: 0);
            Assert.IsFalse(s.ShouldSave(59, out _));
            Assert.IsTrue(s.ShouldSave(60, out string reason));
            Assert.AreEqual("periodic", reason);
        }

        [Test]
        public void ZeroInterval_DisablesPeriodic()
        {
            var s = new AutosaveScheduler(0f, 5f, 0);
            Assert.IsFalse(s.ShouldSave(100000, out _));
        }

        [Test]
        public void Request_WaitsForMinGap()
        {
            var s = new AutosaveScheduler(0f, 5f, 0);
            s.MarkSaved(10);
            s.Request("pile bagged");
            Assert.IsFalse(s.ShouldSave(12, out _));
            Assert.IsTrue(s.ShouldSave(15, out string reason));
            Assert.AreEqual("pile bagged", reason);
        }

        [Test]
        public void UrgentRequest_IgnoresMinGap()
        {
            var s = new AutosaveScheduler(0f, 5f, 0);
            s.MarkSaved(10);
            s.Request("pile bagged");
            s.Request("yard completed", urgent: true);
            Assert.IsTrue(s.ShouldSave(10.5, out string reason));
            Assert.AreEqual("yard completed", reason);
        }

        [Test]
        public void MarkSaved_ClearsRequestAndRestartsPeriodic()
        {
            var s = new AutosaveScheduler(60f, 5f, 0);
            s.Request("x", urgent: true);
            s.MarkSaved(30);
            Assert.IsFalse(s.HasPendingRequest);
            Assert.IsFalse(s.ShouldSave(89, out _));
            Assert.IsTrue(s.ShouldSave(90, out _));
        }

        [Test]
        public void BurstOfRequests_SavesOnce()
        {
            var s = new AutosaveScheduler(0f, 5f, 0);
            int saves = 0;
            for (int frame = 0; frame < 400; frame++)
            {
                double now = frame * 0.016;
                s.Request("pile bagged");
                if (s.ShouldSave(now, out _)) { saves++; s.MarkSaved(now); }
            }
            Assert.AreEqual(1, saves, "400 bag events over 6.4 s with a 5 s gap should write once.");
        }
    }

    public class AutosaverTests
    {
        string _dir;
        GameManager _gm;
        Autosaver _autosaver;

        [SetUp]
        public void SetUp()
        {
            GameEvents.ClearAllSubscribers();
            SaveRegistry.Clear();
            _dir = TestUtil.UseTempSaveFolder();
            _gm = TestUtil.CreateGameManager();
            _autosaver = new GameObject("Autosaver (test)").AddComponent<Autosaver>();
            _autosaver.periodicInterval = 0f;
            _autosaver.minGap = 5f;
            TestUtil.Call(_autosaver, "Awake");
            TestUtil.Call(_autosaver, "OnEnable");
            _autosaver.Scheduler.MarkSaved(0);
        }

        [TearDown]
        public void TearDown()
        {
            if (_autosaver != null) TestUtil.Call(_autosaver, "OnDisable");
            TestUtil.Destroy(_autosaver);
            TestUtil.DestroySingleton(_gm);
            TestUtil.ReleaseTempSaveFolder(_dir);
            SaveRegistry.Clear();
            GameEvents.ClearAllSubscribers();
        }

        [Test]
        public void YardCompleted_SavesOnNextTick()
        {
            _gm.Data.coins = 55;
            GameEvents.RaiseYardCompleted("yard_01");
            _autosaver.Tick(1, 0f);

            Assert.AreEqual(1, _autosaver.SaveCount);
            Assert.AreEqual("yard completed", _autosaver.LastSaveReason);
            Assert.AreEqual(55, SaveSystem.Load().coins);
        }

        [Test]
        public void PauseAndTitleMenu_SaveImmediately()
        {
            GameEvents.RaiseStateChanged(GamePhase.Paused);
            _autosaver.Tick(0.1, 0f);
            Assert.AreEqual(1, _autosaver.SaveCount);

            GameEvents.RaiseStateChanged(GamePhase.TitleMenu);
            _autosaver.Tick(0.2, 0f);
            Assert.AreEqual(2, _autosaver.SaveCount);
        }

        [Test]
        public void PileBagged_IsDebounced()
        {
            GameEvents.RaisePileBagged(100, 5);
            _autosaver.Tick(1, 0f);
            Assert.AreEqual(0, _autosaver.SaveCount);

            GameEvents.RaisePileBagged(100, 5);
            _autosaver.Tick(5, 0f);
            Assert.AreEqual(1, _autosaver.SaveCount);
        }

        [Test]
        public void PileBagged_CanBeTurnedOff()
        {
            _autosaver.saveOnPileBagged = false;
            GameEvents.RaisePileBagged(100, 5);
            _autosaver.Tick(10, 0f);
            Assert.AreEqual(0, _autosaver.SaveCount);
        }

        [Test]
        public void PlayTime_CountsOnlyWhilePlaying()
        {
            _autosaver.Tick(0.5, 0.5f);
            GameEvents.RaiseStateChanged(GamePhase.Paused);
            _autosaver.Tick(1.0, 0.5f);
            GameEvents.RaiseStateChanged(GamePhase.Playing);
            _autosaver.Tick(1.5, 0.5f);

            Assert.AreEqual(1.0, _gm.Data.playTimeSeconds, 1e-6);
        }

        [Test]
        public void OnDisable_StopsListening()
        {
            TestUtil.Call(_autosaver, "OnDisable");
            GameEvents.RaiseYardCompleted("yard_01");
            _autosaver.Tick(1, 0f);
            Assert.AreEqual(0, _autosaver.SaveCount);
            TestUtil.Call(_autosaver, "OnEnable"); // so TearDown's OnDisable is balanced
        }
    }
}

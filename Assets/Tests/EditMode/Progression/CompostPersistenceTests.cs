using FallenYard.Core;
using FallenYard.Progression;
using FallenYard.Save;
using FallenYard.Tools;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Progression
{
    /// <summary>Yard restoration and the compost bin survive a save and reload.</summary>
    public class CompostPersistenceTests
    {
        string _dir;
        GameManager _gm;
        CompostBin _bin;
        MetaProgressionService _service;

        [SetUp]
        public void SetUp()
        {
            GameEvents.ClearAllSubscribers();
            SaveRegistry.Clear();
            _dir = TestUtil.UseTempSaveFolder();
            _gm = TestUtil.CreateGameManager();
            _bin = CreateBin();

            _service = new GameObject("MetaProgressionService (test)").AddComponent<MetaProgressionService>();
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
            DestroyBin();
            TestUtil.DestroySingleton(_gm);
            TestUtil.ReleaseTempSaveFolder(_dir);
            SaveRegistry.Clear();
            GameEvents.ClearAllSubscribers();
            Shader.SetGlobalFloat(CompostBin.RestorationShaderId, 0f);
        }

        static CompostBin CreateBin()
        {
            var bin = new GameObject("CompostBin (test)").AddComponent<CompostBin>();
            bin.leavesPerBatch = 100;
            bin.compostSeconds = 10f;
            bin.restorePerBatch = 0.25f;
            TestUtil.Call(bin, "Awake");
            TestUtil.Call(bin, "OnEnable");
            return bin;
        }

        void DestroyBin()
        {
            if (_bin != null) TestUtil.Call(_bin, "OnDisable");
            TestUtil.DestroySingleton(_bin);
            _bin = null;
        }

        [Test]
        public void Save_CapturesBinIntoCurrentYardRecord()
        {
            _bin.AddLeaves(100);
            _bin.Tick(10f);
            Assert.IsTrue(_bin.TrySpread());
            _bin.AddLeaves(130); // starts a batch, 30 wait
            _bin.Tick(4f);

            _gm.Save();
            var r = SaveSystem.Load().GetYardRecord("starter", false);

            Assert.AreEqual(0.25f, r.restoration, 1e-5f);
            Assert.AreEqual(30, r.compostPendingLeaves);
            Assert.AreEqual(0, r.compostReadyBatches);
            Assert.IsTrue(r.compostIsComposting);
            Assert.AreEqual(0.4f, r.compostProgress, 1e-5f);
        }

        [Test]
        public void Reload_PutsBinBackAndRaisesRestoration()
        {
            _bin.AddLeaves(200);
            _bin.Tick(10f);
            _bin.TrySpread();
            _gm.Save();

            DestroyBin();
            _bin = CreateBin();
            Assert.AreEqual(0f, _bin.Model.Restoration, "fresh bin before restore");

            float raised = -1f;
            GameEvents.YardRestorationChanged += v => raised = v;
            _service.RestoreState(SaveSystem.Load());

            Assert.AreEqual(0.25f, _bin.Model.Restoration, 1e-5f);
            Assert.AreEqual(0.25f, raised, 1e-5f);
            Assert.AreEqual(0.25f, Shader.GetGlobalFloat(CompostBin.RestorationShaderId), 1e-5f);
            Assert.IsTrue(_bin.Model.IsComposting, "second batch was composting when saved");
        }

        [Test]
        public void RestorationEvent_IsRecordedForCurrentYard()
        {
            GameEvents.RaiseYardRestorationChanged(0.6f);
            Assert.AreEqual(0.6f, _service.GetRestoration("starter"), 1e-5f);
            Assert.AreEqual(0f, _service.GetRestoration("other"));
        }

        [Test]
        public void Model_RestoreState_ClampsAndRestartsBatches()
        {
            var m = new CompostModel(100, 10f, 0.25f);
            m.RestoreState(2f, -5, 2, false, 0.7f);
            Assert.AreEqual(1f, m.Restoration);
            Assert.AreEqual(0, m.PendingLeaves);
            Assert.AreEqual(2, m.ReadyBatches);
            Assert.IsFalse(m.IsComposting);
            Assert.AreEqual(0f, m.CompostProgress);

            m.RestoreState(0f, 150, 0, false, 0f);
            Assert.IsTrue(m.IsComposting, "a full batch waiting starts composting");
            Assert.AreEqual(50, m.PendingLeaves);
        }

        [Test]
        public void V2Save_MigratesWithEmptyCompostState()
        {
            const string v2 = @"{ ""version"": 2, ""yardRecords"": [ { ""yardId"": ""starter"", ""timesCompleted"": 1, ""bestCleanFraction"": 1.0 } ] }";
            var data = SaveSystem.FromJson(v2, out int fileVersion);

            Assert.AreEqual(2, fileVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            var r = data.GetYardRecord("starter", false);
            Assert.AreEqual(1, r.timesCompleted);
            Assert.AreEqual(0f, r.restoration);
            Assert.IsFalse(r.compostIsComposting);
        }
    }
}

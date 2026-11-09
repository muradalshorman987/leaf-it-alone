using FallenYard.Core;
using FallenYard.Tools;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Tools
{
    public class CompostTests
    {
        CompostBin _bin;

        [SetUp]
        public void SetUp() => GameEvents.ClearAllSubscribers();

        [TearDown]
        public void TearDown()
        {
            if (_bin != null) TestUtil.Call(_bin, "OnDisable");
            TestUtil.DestroySingleton(_bin);
            _bin = null;
            GameEvents.ClearAllSubscribers();
            Shader.SetGlobalFloat(CompostBin.RestorationShaderId, 0f);
        }

        CompostBin CreateBin(int leavesPerBatch = 100, float seconds = 10f, float restore = 0.25f)
        {
            _bin = new GameObject("CompostBin (test)").AddComponent<CompostBin>();
            _bin.leavesPerBatch = leavesPerBatch;
            _bin.compostSeconds = seconds;
            _bin.restorePerBatch = restore;
            TestUtil.Call(_bin, "Awake");
            TestUtil.Call(_bin, "OnEnable");
            return _bin;
        }

        // ---------------------------------------------------------------- CompostModel

        [Test]
        public void Model_FillsUntilBatch_ThenComposts()
        {
            var m = new CompostModel(100, 10f, 0.25f);
            m.AddLeaves(60);
            Assert.AreEqual(0.6f, m.Fill01, 1e-4f);
            Assert.IsFalse(m.IsComposting);

            m.AddLeaves(70);
            Assert.IsTrue(m.IsComposting, "100 leaves start a batch");
            Assert.AreEqual(30, m.PendingLeaves, "overflow waits for the next batch");
            Assert.AreEqual(0, m.ReadyBatches);
        }

        [Test]
        public void Model_BatchFinishesAfterCompostTime()
        {
            var m = new CompostModel(100, 10f, 0.25f);
            m.AddLeaves(100);
            Assert.IsFalse(m.Tick(9f));
            Assert.AreEqual(0.9f, m.CompostProgress, 1e-4f);
            Assert.IsTrue(m.Tick(1f));
            Assert.AreEqual(1, m.ReadyBatches);
            Assert.IsFalse(m.IsComposting);
        }

        [Test]
        public void Model_QueuedLeaves_StartNextBatchWhenOneFinishes()
        {
            var m = new CompostModel(100, 5f, 0.25f);
            m.AddLeaves(250);
            Assert.AreEqual(150, m.PendingLeaves, "only one batch composts at a time");
            m.Tick(5f);
            Assert.AreEqual(1, m.ReadyBatches);
            Assert.IsTrue(m.IsComposting, "second batch starts right away");
            Assert.AreEqual(50, m.PendingLeaves);
        }

        [Test]
        public void Model_SpreadingRestoresYard_AndStopsAtFull()
        {
            var m = new CompostModel(10, 0f, 0.4f);
            Assert.IsFalse(m.TrySpread(), "nothing to spread yet");
            m.AddLeaves(10); m.Tick(0f);
            m.AddLeaves(10); m.Tick(0f);
            m.AddLeaves(10); m.Tick(0f);
            Assert.AreEqual(3, m.ReadyBatches);

            Assert.IsTrue(m.TrySpread());
            Assert.IsTrue(m.TrySpread());
            Assert.AreEqual(0.8f, m.Restoration, 1e-4f);
            Assert.IsTrue(m.TrySpread());
            Assert.AreEqual(1f, m.Restoration, 1e-4f, "clamped at fully restored");
            Assert.IsTrue(m.IsFullyRestored);

            m.AddLeaves(10); m.Tick(0f);
            Assert.IsFalse(m.TrySpread(), "a fully restored yard doesn't use fertilizer");
            Assert.AreEqual(1, m.ReadyBatches);
        }

        [Test]
        public void Model_IgnoresNonPositiveLeaves()
        {
            var m = new CompostModel(100, 1f, 0.2f);
            m.AddLeaves(0);
            m.AddLeaves(-20);
            Assert.AreEqual(0, m.PendingLeaves);
        }

        // ---------------------------------------------------------------- CompostBin

        [Test]
        public void Bin_FillsFromPileBaggedEvents()
        {
            CreateBin(leavesPerBatch: 100);
            float fill = -1f;
            GameEvents.CompostChanged += (f, ready) => fill = f;

            GameEvents.RaisePileBagged(40, 20);
            Assert.AreEqual(40, _bin.Model.PendingLeaves);
            Assert.AreEqual(0.4f, fill, 1e-4f);
        }

        [Test]
        public void Bin_SpreadRaisesRestorationAndSetsShaderGlobal()
        {
            CreateBin(leavesPerBatch: 50, seconds: 2f, restore: 0.25f);
            float restoration = -1f;
            int ready = -1;
            GameEvents.YardRestorationChanged += r => restoration = r;
            GameEvents.CompostChanged += (f, r) => ready = r;

            _bin.AddLeaves(50);
            Assert.IsFalse(_bin.TrySpread(), "still composting");
            _bin.Tick(2f);
            Assert.AreEqual(1, ready);
            StringAssert.Contains("Spread", _bin.Prompt);

            Assert.IsTrue(_bin.TrySpread());
            Assert.AreEqual(0.25f, restoration, 1e-4f);
            Assert.AreEqual(0.25f, Shader.GetGlobalFloat(CompostBin.RestorationShaderId), 1e-4f);
            Assert.AreEqual(0, ready);
        }

        [Test]
        public void Bin_UpdatesFillVisualAndIndicators()
        {
            CreateBin(leavesPerBatch: 100, seconds: 1f);
            var fill = new GameObject("Fill").transform;
            var readyGo = new GameObject("Ready");
            _bin.fillVisual = fill;
            _bin.readyIndicator = readyGo;
            try
            {
                _bin.AddLeaves(50);
                Assert.AreEqual(0.5f, fill.localScale.y, 1e-4f);
                Assert.IsFalse(readyGo.activeSelf);

                _bin.AddLeaves(50);
                _bin.Tick(1f);
                Assert.IsTrue(readyGo.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(fill.gameObject);
                Object.DestroyImmediate(readyGo);
            }
        }

        [Test]
        public void Bin_StopsListeningWhenDisabled()
        {
            CreateBin(leavesPerBatch: 100);
            TestUtil.Call(_bin, "OnDisable");
            GameEvents.RaisePileBagged(40, 20);
            Assert.AreEqual(0, _bin.Model.PendingLeaves);
            TestUtil.Call(_bin, "OnEnable"); // TearDown disables it again
        }
    }
}

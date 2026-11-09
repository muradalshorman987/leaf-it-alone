using FallenYard.Core;
using FallenYard.VFX;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.VFX
{
    public class LeafVfxTests
    {
        LeafVfx _vfx;

        [SetUp]
        public void SetUp()
        {
            GameEvents.ClearAllSubscribers();
            _vfx = new GameObject("LeafVfx (test)").AddComponent<LeafVfx>();
            _vfx.maxLeaves = 120;
            TestUtil.Call(_vfx, "Awake");
            TestUtil.Call(_vfx, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            TestUtil.Call(_vfx, "OnDisable");
            TestUtil.Call(_vfx, "OnDestroy");
            TestUtil.Destroy(_vfx);
            GameEvents.ClearAllSubscribers();
        }

        [Test]
        public void Awake_BuildsEmitOnlySystemsWithinBudget()
        {
            Assert.IsNotNull(_vfx.Streaks);
            Assert.IsNotNull(_vfx.Dust);
            Assert.IsNotNull(_vfx.Leaves);
            Assert.AreEqual(120, _vfx.Leaves.main.maxParticles);
            int total = _vfx.Streaks.main.maxParticles + _vfx.Dust.main.maxParticles + _vfx.Leaves.main.maxParticles;
            Assert.LessOrEqual(total, 1000, "particle budget");
            foreach (var ps in new[] { _vfx.Streaks, _vfx.Dust, _vfx.Leaves })
            {
                Assert.IsFalse(ps.emission.enabled, ps.name + " is emit-only");
                Assert.AreEqual(ParticleSystemSimulationSpace.World, ps.main.simulationSpace);
            }
        }

        [Test]
        public void LeafParticles_AreInstancedMeshes()
        {
            var r = _vfx.Leaves.GetComponent<ParticleSystemRenderer>();
            Assert.AreEqual(ParticleSystemRenderMode.Mesh, r.renderMode);
            Assert.IsNotNull(r.mesh);
            Assert.IsTrue(r.enableGPUInstancing);
        }

        [Test]
        public void BaggingAPile_PlaysAPoofSizedByThePile()
        {
            GameEvents.RaiseLeavesBaggedAt(new Vector3(1f, 0f, 2f), 100);
            Assert.AreEqual(1, _vfx.PoofsPlayed);
            Assert.AreEqual(VfxMath.PoofCount(100, _vfx.poofMinLeaves, _vfx.poofMaxLeaves), _vfx.LastPoofCount);
        }

        [Test]
        public void NothingBagged_NoPoof()
        {
            GameEvents.RaiseLeavesBaggedAt(Vector3.zero, 0);
            Assert.AreEqual(0, _vfx.PoofsPlayed);
        }

        [Test]
        public void Density_ScalesThePoof()
        {
            _vfx.density = 0.5f;
            _vfx.Poof(Vector3.zero, 10000);
            Assert.AreEqual(Mathf.RoundToInt(_vfx.poofMaxLeaves * 0.5f), _vfx.LastPoofCount);
        }

        [Test]
        public void Unsubscribes_OnDisable()
        {
            TestUtil.Call(_vfx, "OnDisable");
            GameEvents.RaiseLeavesBaggedAt(Vector3.zero, 50);
            Assert.AreEqual(0, _vfx.PoofsPlayed);
            TestUtil.Call(_vfx, "OnEnable");
        }
    }
}

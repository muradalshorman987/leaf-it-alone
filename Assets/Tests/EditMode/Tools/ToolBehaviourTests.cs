using FallenYard.Core;
using FallenYard.Tools;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Tools
{
    /// <summary>
    /// Drives the real tool components against a <see cref="FakeLeafTarget"/>.
    /// The scene has no colliders, so tools aim at the fallback ground point
    /// straight ahead at their range.
    /// </summary>
    public class ToolBehaviourTests
    {
        FakeLeafTarget _leaves;
        GameObject _player;
        Camera _cam;

        [SetUp]
        public void SetUp()
        {
            GameEvents.ClearAllSubscribers();
            _leaves = new FakeLeafTarget();
            LeafTargets.Override = _leaves;

            _player = new GameObject("Player (test)");
            var camGo = new GameObject("Camera (test)");
            camGo.transform.SetParent(_player.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            camGo.transform.localRotation = Quaternion.Euler(30f, 0f, 0f); // looking ahead (+z) and down
            _cam = camGo.AddComponent<Camera>();
        }

        [TearDown]
        public void TearDown()
        {
            LeafTargets.Override = null;
            Object.DestroyImmediate(_player);
            GameEvents.ClearAllSubscribers();
        }

        T AddTool<T>(string id) where T : LeafTool
        {
            var tool = _player.AddComponent<T>();
            tool.toolId = id;
            tool.groundMask = 0; // never hit anything: use the fallback aim point
            return tool;
        }

        // ---------------------------------------------------------------- Rake

        [Test]
        public void Rake_NotHeld_DoesNothing()
        {
            var rake = AddTool<Rake>("rake");
            rake.Tick(_cam, false, 0.02f);
            Assert.AreEqual(0, _leaves.Pushes.Count);
            Assert.AreEqual(0f, rake.Throttle);
        }

        [Test]
        public void Rake_Held_PullsLeavesTowardPlayer()
        {
            var rake = AddTool<Rake>("rake");
            rake.baseRange = 2.6f;
            rake.baseRadius = 0.9f;
            rake.Tick(_cam, true, 0.02f);

            Assert.AreEqual(1, _leaves.Pushes.Count);
            var push = _leaves.Pushes[0];
            Assert.Greater(push.center.z, 2f, "head sits ahead of the player at the rake's range");
            Assert.Less(push.target.z, 0f, "leaves are drawn back toward the player");
            Assert.AreEqual(0.9f, push.radius, 1e-4f);
            Assert.Greater(rake.Throttle, 0f, "a resting rake still scrapes");
        }

        [Test]
        public void Rake_SweepingSideways_DragsLeavesWithTheHead()
        {
            var rake = AddTool<Rake>("rake");
            rake.velocitySmoothing = 0f;
            rake.Tick(_cam, true, 0.02f);
            _player.transform.position += Vector3.right * 0.04f; // 2 m/s sideways sweep
            rake.Tick(_cam, true, 0.02f);

            var push = _leaves.Pushes[1];
            Assert.Greater(push.target.x, 1f, "leaves follow the head to the right");
            Assert.Greater(rake.Throttle, 0.15f);
        }

        // ---------------------------------------------------------------- Blower

        [Test]
        public void Blower_SpinsUp_AndBlowsCoreAndSkirt()
        {
            var blower = AddTool<LeafBlower>("blower");
            blower.baseStrength = 20f;
            blower.coneHalfAngle = 20f;
            blower.coreAngleFraction = 0.4f;
            blower.coreShare = 0.5f;
            blower.spinUpSeconds = 0.2f;

            blower.Tick(_cam, true, 0.1f);
            Assert.AreEqual(0.5f, blower.Throttle, 1e-4f);
            float halfPowerForce = _leaves.Cones[0].force + _leaves.Cones[1].force;
            Assert.Less(halfPowerForce, 20f, "motor is still spinning up");

            _leaves.ClearCalls();
            blower.Tick(_cam, true, 0.1f);
            Assert.AreEqual(1f, blower.Throttle, 1e-4f);
            Assert.AreEqual(2, _leaves.Cones.Count);
            Assert.AreEqual(8f, _leaves.Cones[0].halfAngle, 1e-4f, "core cone");
            Assert.AreEqual(20f, _leaves.Cones[1].halfAngle, 1e-4f, "skirt cone");
            Assert.AreEqual(20f, _leaves.Cones[0].force + _leaves.Cones[1].force, 1e-3f);
            Assert.Greater(Vector3.Dot(_leaves.Cones[0].dir, _cam.transform.forward), 0.99f);
        }

        [Test]
        public void Blower_KeepsBlowingBrieflyAfterRelease()
        {
            var blower = AddTool<LeafBlower>("blower");
            blower.spinUpSeconds = 0f;
            blower.spinDownSeconds = 0.5f;
            blower.Tick(_cam, true, 0.1f);
            _leaves.ClearCalls();

            blower.Tick(_cam, false, 0.1f);
            Assert.AreEqual(0.8f, blower.Throttle, 1e-4f);
            Assert.AreEqual(2, _leaves.Cones.Count, "still coasting");

            blower.Tick(_cam, false, 1f);
            _leaves.ClearCalls();
            blower.Tick(_cam, false, 0.1f);
            Assert.AreEqual(0, _leaves.Cones.Count, "motor stopped");
        }

        // ---------------------------------------------------------------- Vacuum

        LeafVacuum MakeVacuum(int capacity)
        {
            var vac = AddTool<LeafVacuum>("vacuum");
            vac.baseHopperCapacity = capacity;
            vac.leavesPerSecond = 100f;
            vac.spinUpSeconds = 0f;
            vac.spinDownSeconds = 0f;
            return vac;
        }

        [Test]
        public void Vacuum_SucksLeavesIntoHopper_AtItsRate()
        {
            var vac = MakeVacuum(50);
            vac.Tick(_cam, true, 0.1f);
            Assert.AreEqual(10, vac.Hopper.Count);
            Assert.AreEqual(10, _leaves.Collected);
            StringAssert.Contains("10/50", vac.StatusText);
        }

        [Test]
        public void Vacuum_StopsWhenHopperIsFull()
        {
            var vac = MakeVacuum(25);
            for (int i = 0; i < 20; i++) vac.Tick(_cam, true, 0.1f);
            Assert.AreEqual(25, vac.Hopper.Count);
            Assert.AreEqual(25, _leaves.Collected, "never takes more leaves than fit");
            Assert.AreEqual(0f, vac.Throttle, "a full hopper chokes the motor");
            StringAssert.Contains("full", vac.StatusText);
        }

        [Test]
        public void Vacuum_PointingAtBareGrass_DoesNotBankABurst()
        {
            var vac = MakeVacuum(500);
            _leaves.Available = 0;
            for (int i = 0; i < 50; i++) vac.Tick(_cam, true, 0.1f);
            _leaves.Available = 1000;
            vac.Tick(_cam, true, 0.1f);
            Assert.LessOrEqual(vac.Hopper.Count, 11);
        }

        [Test]
        public void Vacuum_EmptyHopper_PaysAndRaisesPileBagged()
        {
            var vac = MakeVacuum(50);
            vac.coinsPerLeaf = 0.5f;
            int leaves = 0, coins = 0;
            GameEvents.PileBagged += (l, c) => { leaves = l; coins = c; };

            for (int i = 0; i < 5; i++) vac.Tick(_cam, true, 0.1f);
            Assert.AreEqual(50, vac.EmptyHopper());
            Assert.AreEqual(50, leaves);
            Assert.AreEqual(25, coins);
            Assert.IsTrue(vac.Hopper.IsEmpty);
            Assert.AreEqual(0, vac.EmptyHopper(), "nothing left");
        }

        // ---------------------------------------------------------------- Mulcher

        Mulcher MakeMulcher(bool pileFound)
        {
            var m = AddTool<Mulcher>("mulcher");
            m.secondsPerPile = 1f;
            m.basePileCapacity = 250;
            m.coinsPerLeaf = 0.5f;
            m.compostMultiplier = 2f;
            m.findPile = (p, d) => (pileFound, new Vector3(0f, 0f, 2.5f), 1f);
            return m;
        }

        [Test]
        public void Mulcher_NoPile_DoesNothing()
        {
            var m = MakeMulcher(false);
            for (int i = 0; i < 10; i++) m.Tick(_cam, true, 0.5f);
            Assert.IsFalse(m.HasTarget);
            Assert.AreEqual(0, _leaves.Collected);
            Assert.AreEqual(0f, m.Throttle);
        }

        [Test]
        public void Mulcher_HoldingOnPile_ShredsItAndPays()
        {
            var m = MakeMulcher(true);
            _leaves.Available = 120;
            int leaves = 0, coins = 0;
            GameEvents.PileBagged += (l, c) => { leaves = l; coins = c; };

            m.Tick(_cam, true, 0.5f);
            Assert.AreEqual(0.5f, m.Progress, 1e-4f);
            Assert.AreEqual(0, _leaves.Collected, "not done yet");
            Assert.AreEqual(1f, m.Throttle);

            m.Tick(_cam, true, 0.5f);
            Assert.AreEqual(120, _leaves.Collected);
            Assert.AreEqual(120, leaves);
            Assert.AreEqual(60, coins);
        }

        [Test]
        public void Mulcher_LettingGo_DrainsProgress()
        {
            var m = MakeMulcher(true);
            m.drainPerSecond = 1f;
            m.Tick(_cam, true, 0.6f);
            m.Tick(_cam, false, 0.4f);
            Assert.AreEqual(0.2f, m.Progress, 1e-4f);
            Assert.AreEqual(0f, m.Throttle);
        }

        [Test]
        public void Mulcher_AddsCompostBonusToTheBin()
        {
            var bin = new GameObject("CompostBin (test)").AddComponent<CompostBin>();
            bin.leavesPerBatch = 10000;
            TestUtil.Call(bin, "Awake");
            TestUtil.Call(bin, "OnEnable");
            try
            {
                var m = MakeMulcher(true);
                _leaves.Available = 100;
                m.Tick(_cam, true, 1f);
                Assert.AreEqual(200, bin.Model.PendingLeaves, "100 from bagging + 100 mulch bonus");
            }
            finally
            {
                TestUtil.Call(bin, "OnDisable");
                TestUtil.DestroySingleton(bin);
            }
        }

        // ---------------------------------------------------------------- ToolController

        [Test]
        public void ToolController_EquipAndStep_RaiseEquippedAndThrottle()
        {
            var blower = AddTool<LeafBlower>("blower");
            blower.spinUpSeconds = 0f;
            blower.spinDownSeconds = 0f;
            var rake = AddTool<Rake>("rake");
            var controller = _player.AddComponent<ToolController>();
            controller.playerCamera = _cam;
            controller.tools.Add(rake);
            controller.tools.Add(blower);
            TestUtil.Call(controller, "Awake");

            string equipped = null;
            float throttle = -1f;
            int throttleEvents = 0;
            GameEvents.ToolEquipped += id => equipped = id;
            GameEvents.ToolThrottle += t => { throttle = t; throttleEvents++; };

            Assert.IsTrue(controller.Equip(1));
            Assert.AreEqual("blower", equipped);
            Assert.IsTrue(blower.IsEquipped);
            Assert.IsFalse(rake.IsEquipped);
            Assert.IsFalse(controller.Equip(1), "already equipped");
            Assert.IsFalse(controller.Equip(7), "no such slot");

            controller.Step(true, 0.1f);
            Assert.AreEqual(1f, throttle);
            int afterSpinUp = throttleEvents;
            controller.Step(true, 0.1f);
            Assert.AreEqual(afterSpinUp, throttleEvents, "unchanged throttle is not re-sent");

            controller.Step(false, 0.1f);
            Assert.AreEqual(0f, throttle);
        }
    }
}

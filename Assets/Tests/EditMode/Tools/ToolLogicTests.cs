using FallenYard.Tools;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Tools
{
    public class ToolLogicTests
    {
        // ---------------------------------------------------------------- ThrottleRamp

        [Test]
        public void ThrottleRamp_SpinsUpOverSpinUpTime_AndCoastsDown()
        {
            var ramp = new ThrottleRamp();
            ramp.Tick(true, 0.1f, 0.2f, 0.4f);
            Assert.AreEqual(0.5f, ramp.Value, 1e-4f);
            ramp.Tick(true, 0.5f, 0.2f, 0.4f);
            Assert.AreEqual(1f, ramp.Value, 1e-4f, "clamped at full power");

            ramp.Tick(false, 0.1f, 0.2f, 0.4f);
            Assert.AreEqual(0.75f, ramp.Value, 1e-4f, "coasts down slower than it spins up");
            ramp.Tick(false, 1f, 0.2f, 0.4f);
            Assert.AreEqual(0f, ramp.Value, 1e-4f);
        }

        [Test]
        public void ThrottleRamp_ZeroTimes_SnapImmediately()
        {
            var ramp = new ThrottleRamp();
            Assert.AreEqual(1f, ramp.Tick(true, 0.01f, 0f, 0f));
            Assert.AreEqual(0f, ramp.Tick(false, 0.01f, 0f, 0f));
        }

        [Test]
        public void ThrottleRamp_Eased_IsSlowAtStartAndMatchesEnds()
        {
            var ramp = new ThrottleRamp();
            ramp.Tick(true, 0.25f, 1f, 1f);
            Assert.Less(ramp.Eased, ramp.Value);
            ramp.Tick(true, 1f, 1f, 1f);
            Assert.AreEqual(1f, ramp.Eased, 1e-4f);
        }

        // ---------------------------------------------------------------- BlowerCone

        [Test]
        public void BlowerCone_CentreLineForce_EqualsTotalForce()
        {
            BlowerCone.Layers(20f, 30f, 2f, 0.4f, 0.6f, out var core, out var skirt);
            Assert.AreEqual(30f, core.force + skirt.force, 1e-4f);
            Assert.AreEqual(30f, BlowerCone.ForceAtAngle(0f, core, skirt), 1e-4f);
        }

        [Test]
        public void BlowerCone_CoreIsNarrower_AndEdgeOnlyGetsSkirt()
        {
            BlowerCone.Layers(20f, 30f, 2f, 0.4f, 0.6f, out var core, out var skirt);
            Assert.AreEqual(8f, core.halfAngle, 1e-4f);
            Assert.AreEqual(20f, skirt.halfAngle, 1e-4f);

            float atCoreEdge = BlowerCone.ForceAtAngle(8f, core, skirt);
            float skirtOnly = skirt.force * (1f - 8f / 20f);
            Assert.AreEqual(skirtOnly, atCoreEdge, 1e-4f);
            Assert.AreEqual(0f, BlowerCone.ForceAtAngle(21f, core, skirt), 1e-4f);
        }

        [Test]
        public void BlowerCone_FallsOffMonotonicallyWithAngle()
        {
            BlowerCone.Layers(22f, 22f, 1.6f, 0.4f, 0.55f, out var core, out var skirt);
            float last = float.MaxValue;
            for (float a = 0f; a <= 22f; a += 1f)
            {
                float f = BlowerCone.ForceAtAngle(a, core, skirt);
                Assert.LessOrEqual(f, last + 1e-5f, $"force rose at {a} degrees");
                last = f;
            }
        }

        [Test]
        public void BlowerCone_Layering_ConcentratesForceNearTheAxis()
        {
            // A plain 22-degree cone keeps 1 - 5/22 = 77% of its force 5 degrees off-axis.
            BlowerCone.Layers(22f, 10f, 1f, 0.4f, 0.55f, out var core, out var skirt);
            float plainRatio = 1f - 5f / 22f;
            float layeredRatio = BlowerCone.ForceAtAngle(5f, core, skirt) / BlowerCone.ForceAtAngle(0f, core, skirt);
            Assert.Less(layeredRatio, plainRatio, "the stream should be tighter than a plain cone");
            Assert.Greater(layeredRatio, 0.4f, "but not so tight that it misses leaves beside the axis");
        }

        // ---------------------------------------------------------------- RakeStroke

        [Test]
        public void RakeStroke_GripCurve_SlipsOnFastFlicks()
        {
            var curve = RakeStroke.DefaultGripCurve();
            Assert.AreEqual(1f, curve.Evaluate(0f), 1e-3f);
            Assert.AreEqual(1f, curve.Evaluate(0.5f), 1e-3f);
            Assert.AreEqual(0.55f, curve.Evaluate(1f), 1e-3f);
            Assert.Less(curve.Evaluate(0.8f), curve.Evaluate(0.5f));
        }

        [Test]
        public void RakeStroke_StrokeTowardPlayer_GathersHarderThanAway()
        {
            Vector3 toPlayer = Vector3.back;
            Vector3 toward = RakeStroke.TargetVelocity(Vector3.back * 2f, toPlayer, 1f, 1f, 0f, 0.5f);
            Vector3 away = RakeStroke.TargetVelocity(Vector3.forward * 2f, toPlayer, 1f, 1f, 0f, 0.5f);
            Assert.AreEqual(3f, toward.magnitude, 1e-4f);
            Assert.AreEqual(2f, away.magnitude, 1e-4f);
        }

        [Test]
        public void RakeStroke_StillRake_OnlyPullsTowardPlayer_AndIgnoresHeight()
        {
            Vector3 v = RakeStroke.TargetVelocity(new Vector3(0f, 5f, 0f), new Vector3(1f, 3f, 0f), 1f, 1f, 1.4f, 0.5f);
            Assert.AreEqual(0f, v.y, 1e-5f);
            Assert.AreEqual(1.4f, v.x, 1e-4f);
        }

        [Test]
        public void RakeStroke_Throttle_IdlesAboveZeroAndCapsAtOne()
        {
            Assert.AreEqual(0.15f, RakeStroke.Throttle(0f, 8f), 1e-4f);
            Assert.AreEqual(1f, RakeStroke.Throttle(20f, 8f), 1e-4f);
        }

        // ---------------------------------------------------------------- LeafHopper

        [Test]
        public void LeafHopper_AcceptsUpToCapacity_ThenEmpties()
        {
            var hopper = new LeafHopper(100);
            Assert.AreEqual(70, hopper.Add(70));
            Assert.AreEqual(30, hopper.Add(70), "only the space left fits");
            Assert.IsTrue(hopper.IsFull);
            Assert.AreEqual(1f, hopper.Fill01, 1e-4f);
            Assert.AreEqual(100, hopper.Empty());
            Assert.IsTrue(hopper.IsEmpty);
            Assert.AreEqual(0, hopper.Add(-5));
        }

        [Test]
        public void LeafHopper_ShrinkingCapacity_KeepsLeaves()
        {
            var hopper = new LeafHopper(100);
            hopper.Add(80);
            hopper.SetCapacity(50);
            Assert.AreEqual(80, hopper.Count);
            Assert.AreEqual(0, hopper.Space);
            Assert.IsTrue(hopper.IsFull);
        }

        // ---------------------------------------------------------------- ChargeMeter

        [Test]
        public void ChargeMeter_FiresOnceWhenFull_ThenResets()
        {
            var meter = new ChargeMeter();
            Assert.IsFalse(meter.Tick(true, 0.5f, 1f, 1f));
            Assert.AreEqual(0.5f, meter.Value, 1e-4f);
            Assert.IsTrue(meter.Tick(true, 0.5f, 1f, 1f));
            Assert.AreEqual(0f, meter.Value, 1e-4f);
        }

        [Test]
        public void ChargeMeter_DrainsWhenReleased()
        {
            var meter = new ChargeMeter();
            meter.Tick(true, 0.8f, 1f, 2f);
            meter.Tick(false, 0.25f, 1f, 2f);
            Assert.AreEqual(0.3f, meter.Value, 1e-4f);
            meter.Tick(false, 1f, 1f, 2f);
            Assert.AreEqual(0f, meter.Value, 1e-4f);
        }
    }
}

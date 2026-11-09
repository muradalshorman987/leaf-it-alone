using FallenYard.Player;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Tools
{
    public class CameraFeelTests
    {
        [Test]
        public void BobOffset_ZeroIntensity_IsZero()
        {
            Assert.AreEqual(Vector3.zero, CameraFeel.BobOffset(1.3f, 0.05f, 0f));
        }

        [Test]
        public void BobOffset_OnlyDipsDown_WithinAmplitude()
        {
            for (float p = 0f; p < Mathf.PI * 4f; p += 0.1f)
            {
                Vector3 o = CameraFeel.BobOffset(p, 0.05f, 1f);
                Assert.LessOrEqual(o.y, 0f);
                Assert.GreaterOrEqual(o.y, -0.05f - 1e-5f);
                Assert.LessOrEqual(Mathf.Abs(o.x), 0.025f + 1e-5f);
            }
        }

        [Test]
        public void AdvancePhase_OneStepLength_IsHalfACycle()
        {
            float phase = CameraFeel.AdvancePhase(0f, 1f, 0.8f, 0.8f);
            Assert.AreEqual(Mathf.PI, phase, 1e-4f);
            Assert.AreEqual(0f, CameraFeel.AdvancePhase(0f, 0f, 1f, 0.8f), "standing still doesn't bob");
        }

        [Test]
        public void TargetFov_KicksOnlyWhenSprinting()
        {
            Assert.AreEqual(70f, CameraFeel.TargetFov(70f, 6f, 4f, 4f, 7f), 1e-4f);
            Assert.AreEqual(73f, CameraFeel.TargetFov(70f, 6f, 5.5f, 4f, 7f), 1e-4f);
            Assert.AreEqual(76f, CameraFeel.TargetFov(70f, 6f, 10f, 4f, 7f), 1e-4f);
        }

        [Test]
        public void Damp_MovesTowardTarget_WithoutOvershoot()
        {
            float v = CameraFeel.Damp(0f, 10f, 8f, 0.016f);
            Assert.Greater(v, 0f);
            Assert.Less(v, 10f);
            Assert.AreEqual(10f, CameraFeel.Damp(0f, 10f, 8f, 100f), 1e-3f);
        }
    }
}

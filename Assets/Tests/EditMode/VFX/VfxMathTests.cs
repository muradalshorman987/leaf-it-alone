using FallenYard.VFX;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.VFX
{
    public class VfxMathTests
    {
        [Test]
        public void Accumulate_EmitsTheRightAverage_EvenBelowOnePerFrame()
        {
            float carry = 0f;
            int total = 0;
            for (int i = 0; i < 600; i++) total += VfxMath.Accumulate(ref carry, 12f, 1f / 60f);
            Assert.AreEqual(120, total, 1);
        }

        [Test]
        public void Accumulate_ResetsWhenStopped()
        {
            float carry = 0.9f;
            Assert.AreEqual(0, VfxMath.Accumulate(ref carry, 0f, 0.1f));
            Assert.AreEqual(0f, carry);
        }

        [Test]
        public void BlowerRate_IsZeroAtIdle_FullAtFullThrottle_EasedBetween()
        {
            Assert.AreEqual(0f, VfxMath.BlowerRate(0f, 100f));
            Assert.AreEqual(100f, VfxMath.BlowerRate(1f, 100f), 1e-4f);
            Assert.Less(VfxMath.BlowerRate(0.5f, 100f), 50f);
            Assert.AreEqual(100f, VfxMath.BlowerRate(3f, 100f), 1e-4f, "throttle clamped");
        }

        [Test]
        public void PoofCount_GrowsWithPileButStaysInBudget()
        {
            Assert.AreEqual(0, VfxMath.PoofCount(0, 10, 50));
            int small = VfxMath.PoofCount(4, 10, 50);
            int medium = VfxMath.PoofCount(100, 10, 50);
            Assert.GreaterOrEqual(small, 10);
            Assert.Greater(medium, small);
            Assert.AreEqual(50, VfxMath.PoofCount(100000, 10, 50));
        }

        [Test]
        public void AirborneRate_RisesWithWindWeatherAndGusts()
        {
            float still = VfxMath.AirborneRate(0f, 0.5f, 0f, 20f);
            float windy = VfxMath.AirborneRate(5f, 0.5f, 0f, 20f);
            float gusty = VfxMath.AirborneRate(5f, 0.5f, 1f, 20f);
            Assert.Greater(windy, still);
            Assert.Greater(gusty, windy);
            Assert.LessOrEqual(gusty, 20f);
            Assert.AreEqual(0f, VfxMath.AirborneRate(5f, 0f, 0f, 20f));
        }

        [Test]
        public void ConeDirection_StaysInsideTheCone()
        {
            Vector3 fwd = new Vector3(0.3f, -0.2f, 1f).normalized;
            for (int i = 0; i < 50; i++)
            {
                Vector3 d = VfxMath.ConeDirection(fwd, 20f, i / 49f, (i * 7 % 50) / 49f);
                Assert.AreEqual(1f, d.magnitude, 1e-4f);
                Assert.LessOrEqual(Vector3.Angle(fwd, d), 20.01f);
            }
            Vector3 straight = VfxMath.ConeDirection(Vector3.up, 30f, 0f, 0.5f);
            Assert.Less(Vector3.Angle(Vector3.up, straight), 0.01f, "u = 0 is the centre line, even pointing straight up");
        }

        [Test]
        public void SoftDot_IsOpaqueInTheMiddleAndClearAtTheCorners()
        {
            var px = ParticleFactory.SoftDotPixels(16);
            Assert.AreEqual(256, px.Length);
            Assert.AreEqual(0, px[0].a);
            Assert.Greater(px[8 * 16 + 8].a, 200);
        }
    }
}

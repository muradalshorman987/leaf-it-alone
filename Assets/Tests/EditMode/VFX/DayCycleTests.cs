using FallenYard.VFX;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.VFX
{
    public class DayCycleTests
    {
        static DayCycleSettings Settings(float dayMinutes = 24f) => new DayCycleSettings
        {
            startHour = 12f, dayLengthMinutes = dayMinutes, sunriseHour = 6f, sunsetHour = 18f,
            maxSunElevation = 50f, maxMoonElevation = 30f, azimuthOffset = 0f, minElevation = 5f,
        };

        [Test]
        public void Wrap_KeepsHoursInZeroToTwentyFour()
        {
            Assert.AreEqual(1f, DayCycle.Wrap(25f), 1e-4f);
            Assert.AreEqual(23f, DayCycle.Wrap(-1f), 1e-4f);
            Assert.AreEqual(0f, DayCycle.Wrap(float.NaN));
        }

        [Test]
        public void SunHeight_PeaksAtNoon_ZeroAtSunriseAndSunset_LowestAtMidnight()
        {
            Assert.AreEqual(1f, DayCycle.SunHeight(12f, 6f, 18f), 1e-4f);
            Assert.AreEqual(0f, DayCycle.SunHeight(6f, 6f, 18f), 1e-4f);
            Assert.AreEqual(0f, DayCycle.SunHeight(18f, 6f, 18f), 1e-4f);
            Assert.AreEqual(-1f, DayCycle.SunHeight(0f, 6f, 18f), 1e-4f);
            Assert.Less(DayCycle.SunHeight(21f, 6f, 18f), 0f);
        }

        [Test]
        public void Daylight_IsOneAtNoonAndZeroAtMidnight()
        {
            Assert.AreEqual(1f, DayCycle.Daylight(12f, 6f, 18f), 1e-4f);
            Assert.AreEqual(0f, DayCycle.Daylight(0f, 6f, 18f), 1e-4f);
            float dusk = DayCycle.Daylight(18f, 6f, 18f);
            Assert.Greater(dusk, 0f);
            Assert.Less(dusk, 1f);
        }

        [Test]
        public void Tick_AdvancesAtDayLength_AndWrapsPastMidnight()
        {
            var c = new DayCycle(Settings(24f)); // 24 real minutes per day = 1 game hour per real minute
            c.Tick(60f);
            Assert.AreEqual(13f, c.Hour, 1e-3f);
            c.Tick(60f * 12f);
            Assert.AreEqual(1f, c.Hour, 1e-3f);
            Assert.AreEqual(1, c.DaysElapsed);
        }

        [Test]
        public void Tick_DoesNothingWhenFrozenOrPaused()
        {
            var frozen = new DayCycle(Settings(0f));
            frozen.Tick(1000f);
            Assert.AreEqual(12f, frozen.Hour, 1e-4f);

            var paused = new DayCycle(Settings()) { Paused = true };
            paused.Tick(1000f);
            Assert.AreEqual(12f, paused.Hour, 1e-4f);
        }

        [Test]
        public void LightRotation_AlwaysShinesDown_DayAndNight()
        {
            var s = Settings();
            for (float h = 0f; h < 24f; h += 0.5f)
            {
                Vector3 fwd = DayCycle.LightRotationAt(h, s) * Vector3.forward;
                Assert.Less(fwd.y, -0.05f, $"hour {h}");
            }
        }

        [Test]
        public void LightRotation_SunTravelsFromEastToWest()
        {
            var s = Settings();
            Vector3 morning = DayCycle.LightRotationAt(8f, s) * Vector3.forward;
            Vector3 evening = DayCycle.LightRotationAt(16f, s) * Vector3.forward;
            Assert.Less(morning.x, 0f, "morning sun in the east shines towards -X");
            Assert.Greater(evening.x, 0f, "evening sun in the west shines towards +X");
        }

        [Test]
        public void LightRotation_IsHighestAtNoon()
        {
            var s = Settings();
            float noon = (DayCycle.LightRotationAt(12f, s) * Vector3.forward).y;
            float morning = (DayCycle.LightRotationAt(8f, s) * Vector3.forward).y;
            Assert.Less(noon, morning);
            Assert.AreEqual(-Mathf.Sin(50f * Mathf.Deg2Rad), noon, 1e-3f);
        }
    }
}

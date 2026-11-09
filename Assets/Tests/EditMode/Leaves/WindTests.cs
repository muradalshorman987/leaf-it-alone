using FallenYard.Core;
using FallenYard.Leaves;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Leaves
{
    public class WindTests
    {
        LeafField _field;
        WindSystem _wind;

        [SetUp]
        public void SetUp() => GameEvents.ClearAllSubscribers();

        [TearDown]
        public void TearDown()
        {
            TestUtil.Destroy(_wind);
            TestUtil.DestroyLeafField(_field);
            GameEvents.ClearAllSubscribers();
        }

        static GustSettings QuickGusts() => new GustSettings
        {
            firstGustDelay = 1f,
            gustInterval = new Vector2(2f, 2f),
            gustDuration = new Vector2(1f, 1f),
            gustStrength = new Vector2(5f, 5f),
            breezeStrength = 0.5f,
        };

        [Test]
        public void Envelope_RisesHoldsAndFalls()
        {
            Assert.AreEqual(0f, GustScheduler.Envelope(0f), 1e-6f);
            Assert.AreEqual(1f, GustScheduler.Envelope(0.4f), 1e-6f);
            Assert.AreEqual(0f, GustScheduler.Envelope(1f), 1e-6f);
            Assert.Greater(GustScheduler.Envelope(0.1f), 0f);
            Assert.Less(GustScheduler.Envelope(0.1f), 1f);
        }

        [Test]
        public void DirectionFromDegrees_ZeroIsPlusZ_NinetyIsPlusX()
        {
            Vector2 north = GustScheduler.DirectionFromDegrees(0f);
            Vector2 east = GustScheduler.DirectionFromDegrees(90f);
            Assert.AreEqual(0f, north.x, 1e-5f);
            Assert.AreEqual(1f, north.y, 1e-5f);
            Assert.AreEqual(1f, east.x, 1e-5f);
            Assert.AreEqual(0f, east.y, 1e-5f);
        }

        [Test]
        public void Scheduler_StartsGustsOnScheduleAndEndsThem()
        {
            var s = new GustScheduler(QuickGusts(), 5);
            Assert.IsFalse(s.Tick(0.5f));
            Assert.IsFalse(s.GustActive);
            Assert.IsTrue(s.Tick(0.6f), "first gust at 1 s");
            Assert.IsTrue(s.GustActive);
            Assert.AreEqual(5f, s.GustPeakStrength, 1e-5f);
            Assert.AreEqual(1f, s.GustDirection.magnitude, 1e-4f);

            s.Tick(1.1f); // past the 1 s duration
            Assert.IsFalse(s.GustActive);
            Assert.Greater(s.NextGustTime, s.Time);

            bool started = false;
            for (int i = 0; i < 30 && !started; i++) started = s.Tick(0.1f);
            Assert.IsTrue(started, "second gust after the 2 s interval");
            Assert.AreEqual(2, s.GustsStarted);
        }

        [Test]
        public void Scheduler_GustStrengthPeaksMidGust()
        {
            var s = new GustScheduler(QuickGusts(), 5);
            s.StartGust(Vector2.right, 6f);
            float start = s.Sample().strength;
            s.Tick(0.4f);
            WindSample mid = s.Sample();
            Assert.IsTrue(mid.gustActive);
            Assert.AreEqual(6f, mid.strength, 1e-4f);
            Assert.Greater(mid.strength, start);
            Assert.AreEqual(1f, mid.direction.x, 1e-4f);
        }

        [Test]
        public void Scheduler_BreezeStaysBelowOakThreshold()
        {
            var settings = new GustSettings { gustsEnabled = false };
            var s = new GustScheduler(settings, 11);
            float oakThreshold = LeafPhysicsSettings.For(LeafPreset.Oak).windThreshold;
            for (int i = 0; i < 2000; i++)
            {
                s.Tick(0.1f);
                Assert.Less(s.Sample().strength, oakThreshold);
                Assert.IsFalse(s.GustActive);
            }
        }

        [Test]
        public void Scheduler_SameSeed_SameTimeline()
        {
            var a = new GustScheduler(new GustSettings(), 99);
            var b = new GustScheduler(new GustSettings(), 99);
            for (int i = 0; i < 3000; i++)
            {
                Assert.AreEqual(a.Tick(0.05f), b.Tick(0.05f));
                Assert.AreEqual(a.Sample().strength, b.Sample().strength);
            }
            Assert.Greater(a.GustsStarted, 0, "150 s should include at least one gust");
        }

        [Test]
        public void WindSystem_RaisesWindGustEventAndFeedsTheField()
        {
            _field = TestUtil.CreateLeafField(new Vector2(6f, 6f), 0.5f, 300);
            _wind = new GameObject("WindSystem (test)").AddComponent<WindSystem>();
            _wind.settings = QuickGusts();

            int gusts = 0;
            Vector3 gustDir = Vector3.zero;
            float gustStrength = 0f;
            GameEvents.WindGust += (d, s) => { gusts++; gustDir = d; gustStrength = s; };

            _wind.Tick(0.5f);
            Assert.AreEqual(0, gusts);
            Assert.AreEqual(_wind.WindStrength, _field.WindStrength, 1e-5f, "breeze reaches the field");

            _wind.Tick(0.6f);
            Assert.AreEqual(1, gusts);
            Assert.AreEqual(5f, gustStrength, 1e-5f);
            Assert.AreEqual(0f, gustDir.y, 1e-6f);
            Assert.AreEqual(1f, gustDir.magnitude, 1e-4f);
            Assert.IsTrue(_wind.GustActive);
        }

        [Test]
        public void WindSystem_TriggerGust_RaisesImmediately()
        {
            _wind = new GameObject("WindSystem (test)").AddComponent<WindSystem>();
            int gusts = 0;
            GameEvents.WindGust += (d, s) => gusts++;
            _wind.TriggerGust(Vector3.left, 4f);
            Assert.AreEqual(1, gusts);
            Assert.IsTrue(_wind.GustActive);
            Assert.AreEqual(-1f, _wind.Scheduler.GustDirection.x, 1e-4f);
        }
    }
}

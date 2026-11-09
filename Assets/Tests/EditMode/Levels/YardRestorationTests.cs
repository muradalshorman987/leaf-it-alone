using System.Collections.Generic;
using FallenYard.Core;
using FallenYard.Levels;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Levels
{
    public class YardRestorationTests
    {
        YardRestoration _r;
        GameObject _early, _late;

        [SetUp]
        public void SetUp()
        {
            GameEvents.ClearAllSubscribers();
            _r = new GameObject("Restoration (test)").AddComponent<YardRestoration>();
            _early = new GameObject("early");
            _late = new GameObject("late");
            _r.regrowth = new List<YardRestoration.RegrowthItem>
            {
                new YardRestoration.RegrowthItem { target = _early, showAt = 0.2f },
                new YardRestoration.RegrowthItem { target = _late, showAt = 0.9f },
            };
            _r.easeSpeed = 1f;
            TestUtil.Call(_r, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            TestUtil.Call(_r, "OnDisable");
            Object.DestroyImmediate(_r.gameObject);
            Object.DestroyImmediate(_early);
            Object.DestroyImmediate(_late);
            GameEvents.ClearAllSubscribers();
        }

        [Test]
        public void GrassColor_LerpsAndClamps()
        {
            var a = Color.black;
            var b = Color.white;
            Assert.AreEqual(a, YardRestoration.GrassColorAt(a, b, -1f));
            Assert.AreEqual(b, YardRestoration.GrassColorAt(a, b, 2f));
            Assert.AreEqual(0.5f, YardRestoration.GrassColorAt(a, b, 0.5f).r, 1e-4f);
        }

        [Test]
        public void SavedValue_BeforeTheFirstFrame_ShowsAtOnce()
        {
            GameEvents.RaiseYardRestorationChanged(0.5f);
            Assert.AreEqual(0.5f, _r.Shown, 1e-4f);
            Assert.IsTrue(_early.activeSelf);
            Assert.IsFalse(_late.activeSelf);
        }

        [Test]
        public void SpreadingFertilizer_EasesIn()
        {
            _r.Tick(0.1f); // first frame
            GameEvents.RaiseYardRestorationChanged(1f);
            Assert.AreEqual(1f, _r.Target);
            Assert.AreEqual(0f, _r.Shown);
            _r.Tick(0.5f);
            Assert.AreEqual(0.5f, _r.Shown, 1e-4f);
            Assert.IsTrue(_early.activeSelf);
            Assert.IsFalse(_late.activeSelf);
            _r.Tick(1f);
            Assert.AreEqual(1f, _r.Shown);
            Assert.IsTrue(_late.activeSelf);
        }

        [Test]
        public void ApplyImmediately_ZeroHidesEverything()
        {
            _r.ApplyImmediately(1f);
            Assert.IsTrue(_late.activeSelf);
            _r.ApplyImmediately(0f);
            Assert.IsFalse(_early.activeSelf);
            Assert.IsFalse(_late.activeSelf);
        }

        [Test]
        public void IsRegrown_RespectsThreshold()
        {
            var item = new YardRestoration.RegrowthItem { showAt = 0.4f };
            Assert.IsFalse(YardRestoration.IsRegrown(item, 0.39f));
            Assert.IsTrue(YardRestoration.IsRegrown(item, 0.4f));
            Assert.IsFalse(YardRestoration.IsRegrown(null, 1f));
            Assert.IsFalse(YardRestoration.IsRegrown(new YardRestoration.RegrowthItem { showAt = 0f }, 0f), "nothing grows before any fertilizer");
        }

        [Test]
        public void ShownChanged_IsRaised()
        {
            float last = -1f;
            _r.ShownChanged += v => last = v;
            _r.ApplyImmediately(0.3f);
            Assert.AreEqual(0.3f, last, 1e-4f);
        }
    }
}

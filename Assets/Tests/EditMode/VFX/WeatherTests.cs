using System.Collections.Generic;
using FallenYard.Core;
using FallenYard.Leaves;
using FallenYard.VFX;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.VFX
{
    public class WeatherTests
    {
        WindSystem _wind;
        WeatherSystem _weather;

        [SetUp]
        public void SetUp() => GameEvents.ClearAllSubscribers();

        [TearDown]
        public void TearDown()
        {
            if (_weather != null)
            {
                TestUtil.Call(_weather, "OnDisable");
                TestUtil.Call(_weather, "OnDestroy");
            }
            TestUtil.Destroy(_weather);
            TestUtil.Destroy(_wind);
            GameEvents.ClearAllSubscribers();
        }

        static WeatherSettings Quick() => new WeatherSettings
        {
            spellDuration = new Vector2(10f, 10f),
            transitionSeconds = 4f,
            gustFlashSeconds = 2f,
            gustFlashStrength = 5f,
        };

        [Test]
        public void Model_SameSeed_GivesSameWeatherSequence()
        {
            var a = new WeatherModel(Quick(), 9);
            var b = new WeatherModel(Quick(), 9);
            var seqA = new List<WeatherKind>();
            var seqB = new List<WeatherKind>();
            for (int i = 0; i < 300; i++)
            {
                if (a.Tick(0.5f)) seqA.Add(a.Current);
                if (b.Tick(0.5f)) seqB.Add(b.Current);
            }
            Assert.Greater(seqA.Count, 5);
            CollectionAssert.AreEqual(seqA, seqB);
        }

        [Test]
        public void Model_NextWeatherIsAlwaysDifferent()
        {
            var m = new WeatherModel(Quick(), 3);
            for (int i = 0; i < 400; i++)
            {
                var before = m.Current;
                if (m.Tick(0.5f)) Assert.AreNotEqual(before, m.Current);
            }
        }

        [Test]
        public void Model_StaysPutWhenCyclingIsOff()
        {
            var s = Quick();
            s.cycleWeather = false;
            s.startWeather = WeatherKind.Overcast;
            var m = new WeatherModel(s, 1);
            for (int i = 0; i < 100; i++) Assert.IsFalse(m.Tick(1f));
            Assert.AreEqual(WeatherKind.Overcast, m.Current);
        }

        [Test]
        public void Model_BlendsSmoothlyIntoNewWeather()
        {
            var s = Quick();
            s.cycleWeather = false;
            var m = new WeatherModel(s, 1);
            float clearCloud = s.Profile(WeatherKind.Clear).cloudCover;
            float overcastCloud = s.Profile(WeatherKind.Overcast).cloudCover;

            m.SetWeather(WeatherKind.Overcast, false);
            Assert.AreEqual(clearCloud, m.Sample().cloudCover, 1e-4f, "starts from the old weather");
            m.Tick(2f);
            float mid = m.Sample().cloudCover;
            Assert.Greater(mid, clearCloud);
            Assert.Less(mid, overcastCloud);
            m.Tick(2f);
            Assert.AreEqual(overcastCloud, m.Sample().cloudCover, 1e-4f);
        }

        [Test]
        public void Model_GustFlashSpikesAndFades()
        {
            var m = new WeatherModel(Quick(), 1);
            m.OnGust(10f);
            Assert.AreEqual(1f, m.Sample().gustFlash, 1e-4f, "strong gust = full flash");
            m.Tick(1f);
            Assert.AreEqual(0.5f, m.Sample().gustFlash, 1e-4f);
            m.Tick(5f);
            Assert.AreEqual(0f, m.Sample().gustFlash, 1e-4f);

            m.OnGust(2.5f);
            Assert.AreEqual(0.5f, m.Sample().gustFlash, 1e-4f, "flash scales with gust strength");
        }

        [Test]
        public void Sample_Lerp_HitsEndpoints()
        {
            var a = WeatherSample.From(WeatherProfile.Default(WeatherKind.Clear));
            var b = WeatherSample.From(WeatherProfile.Default(WeatherKind.Blustery));
            Assert.AreEqual(a.windMultiplier, WeatherSample.Lerp(a, b, 0f).windMultiplier, 1e-5f);
            Assert.AreEqual(b.windMultiplier, WeatherSample.Lerp(a, b, 1f).windMultiplier, 1e-5f);
            Assert.AreEqual(b.windMultiplier, WeatherSample.Lerp(a, b, 7f).windMultiplier, 1e-5f, "clamped");
        }

        [Test]
        public void Id_IsLowercaseName()
        {
            Assert.AreEqual("blustery", WeatherModel.Id(WeatherKind.Blustery));
        }

        void CreateSystems(WeatherKind start)
        {
            _wind = new GameObject("Wind (test)").AddComponent<WindSystem>();
            _wind.settings = new GustSettings { breezeStrength = 1f, gustInterval = new Vector2(30f, 60f), gustStrength = new Vector2(3f, 5f) };
            _weather = new GameObject("Weather (test)").AddComponent<WeatherSystem>();
            _weather.wind = _wind;
            _weather.settings = new WeatherSettings { cycleWeather = false, startWeather = start, transitionSeconds = 0f };
            TestUtil.Call(_weather, "Awake");
            TestUtil.Call(_weather, "OnEnable");
        }

        [Test]
        public void System_ScalesWindByWeather_AndRestoresOnDisable()
        {
            CreateSystems(WeatherKind.Clear);
            _weather.SetWeather(WeatherKind.Blustery, true);
            var p = _weather.settings.Profile(WeatherKind.Blustery);
            Assert.AreEqual(1f * p.windMultiplier, _wind.settings.breezeStrength, 1e-4f);
            Assert.AreEqual(30f * p.gustIntervalMultiplier, _wind.settings.gustInterval.x, 1e-3f);
            Assert.AreEqual(5f * p.gustStrengthMultiplier, _wind.settings.gustStrength.y, 1e-3f);

            TestUtil.Call(_weather, "OnDisable");
            Assert.AreEqual(1f, _wind.settings.breezeStrength, 1e-5f);
            Assert.AreEqual(new Vector2(30f, 60f), _wind.settings.gustInterval);
            Assert.AreEqual(new Vector2(3f, 5f), _wind.settings.gustStrength);
            TestUtil.Call(_weather, "OnEnable"); // so TearDown's OnDisable is balanced
        }

        [Test]
        public void System_LooksOnly_LeavesWindAlone()
        {
            CreateSystems(WeatherKind.Clear);
            _weather.affectWind = false;
            _weather.SetWeather(WeatherKind.Blustery, true);
            Assert.AreEqual(1f, _wind.settings.breezeStrength, 1e-5f);
        }

        [Test]
        public void System_RaisesWeatherChanged()
        {
            CreateSystems(WeatherKind.Clear);
            string got = null;
            GameEvents.WeatherChanged += id => got = id;
            _weather.SetWeather(WeatherKind.Overcast);
            Assert.AreEqual("overcast", got);
        }

        [Test]
        public void System_WindGustEvent_GivesGustFlash()
        {
            CreateSystems(WeatherKind.Clear);
            Assert.AreEqual(0f, _weather.Current.gustFlash, 1e-5f);
            GameEvents.RaiseWindGust(Vector3.forward, 6f);
            _weather.Tick(0f);
            Assert.Greater(_weather.Current.gustFlash, 0.9f);
        }

        [Test]
        public void System_RealGustFromWindSystem_ReachesWeather()
        {
            CreateSystems(WeatherKind.Clear);
            _wind.TriggerGust(Vector3.right, 6f);
            _weather.Tick(0f);
            Assert.Greater(_weather.Current.gustFlash, 0.9f);
        }
    }
}

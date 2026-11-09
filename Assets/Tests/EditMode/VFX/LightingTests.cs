using FallenYard.VFX;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace FallenYard.Tests.VFX
{
    public class LightingTests
    {
        LightingPreset _preset;
        LightingRig _rig;
        Light _sun;

        // RenderSettings belong to the open scene; put them back after each test.
        AmbientMode _ambientMode;
        Color _sky, _equator, _ground, _fogColor;
        bool _fog;
        FogMode _fogMode;
        float _fogDensity;

        [SetUp]
        public void SetUp()
        {
            _ambientMode = RenderSettings.ambientMode;
            _sky = RenderSettings.ambientSkyColor;
            _equator = RenderSettings.ambientEquatorColor;
            _ground = RenderSettings.ambientGroundColor;
            _fog = RenderSettings.fog;
            _fogMode = RenderSettings.fogMode;
            _fogColor = RenderSettings.fogColor;
            _fogDensity = RenderSettings.fogDensity;
        }

        [TearDown]
        public void TearDown()
        {
            if (_rig != null) TestUtil.Call(_rig, "OnDestroy");
            TestUtil.Destroy(_rig);
            TestUtil.Destroy(_sun);
            if (_preset != null) Object.DestroyImmediate(_preset);
            RenderSettings.ambientMode = _ambientMode;
            RenderSettings.ambientSkyColor = _sky;
            RenderSettings.ambientEquatorColor = _equator;
            RenderSettings.ambientGroundColor = _ground;
            RenderSettings.fog = _fog;
            RenderSettings.fogMode = _fogMode;
            RenderSettings.fogColor = _fogColor;
            RenderSettings.fogDensity = _fogDensity;
        }

        static void AssertColor(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-3f);
            Assert.AreEqual(expected.g, actual.g, 1e-3f);
            Assert.AreEqual(expected.b, actual.b, 1e-3f);
        }

        static float Luma(Color c) => c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
        static float Chroma(Color c) => Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - Mathf.Min(c.r, Mathf.Min(c.g, c.b));

        [Test]
        public void DefaultPreset_IsConfigured()
        {
            _preset = LightingPreset.CreateDefault();
            Assert.IsTrue(_preset.IsConfigured);
        }

        [Test]
        public void EmptyPreset_FillsInDefaultsWhenEvaluated()
        {
            _preset = ScriptableObject.CreateInstance<LightingPreset>();
            _preset.sunIntensity = new AnimationCurve();
            var s = _preset.Evaluate(12f, WeatherSample.Calm);
            Assert.Greater(s.sunIntensity, 0.5f);
        }

        [Test]
        public void Noon_IsBrighterAndWarmerThanMidnight()
        {
            _preset = LightingPreset.CreateDefault();
            var noon = _preset.Evaluate(13f, WeatherSample.Calm);
            var night = _preset.Evaluate(0f, WeatherSample.Calm);
            Assert.Greater(noon.sunIntensity, night.sunIntensity * 3f);
            Assert.Greater(Luma(noon.skyAmbient), Luma(night.skyAmbient));
            Assert.Greater(night.sunColor.b, night.sunColor.r, "moonlight is blue");
            Assert.Greater(noon.sunColor.r, noon.sunColor.b, "sunlight is warm");
        }

        [Test]
        public void Dusk_IsOrange()
        {
            _preset = LightingPreset.CreateDefault();
            var dusk = _preset.Evaluate(18.3f, WeatherSample.Calm);
            Assert.Greater(dusk.sunColor.r, dusk.sunColor.g + 0.3f);
        }

        [Test]
        public void Clouds_DimGreyAndSoftenTheSun_AndThickenFog()
        {
            _preset = LightingPreset.CreateDefault();
            var clear = _preset.Evaluate(14f, WeatherSample.Calm);
            var cloudy = WeatherSample.Calm;
            cloudy.cloudCover = 1f;
            cloudy.fogMultiplier = 2f;
            var overcast = _preset.Evaluate(14f, cloudy);
            Assert.Less(overcast.sunIntensity, clear.sunIntensity * 0.6f);
            Assert.Less(Chroma(overcast.sunColor), Chroma(clear.sunColor));
            Assert.Less(overcast.shadowStrength, clear.shadowStrength);
            Assert.AreEqual(clear.fogDensity * 2f, overcast.fogDensity, 1e-5f);
        }

        [Test]
        public void GustFlash_DimsTheSunBriefly()
        {
            _preset = LightingPreset.CreateDefault();
            var calm = _preset.Evaluate(14f, WeatherSample.Calm);
            var gust = WeatherSample.Calm;
            gust.gustFlash = 1f;
            Assert.Less(_preset.Evaluate(14f, gust).sunIntensity, calm.sunIntensity);
        }

        [Test]
        public void Desaturate_FullAmountIsGrey()
        {
            Color g = LightingPreset.Desaturate(new Color(1f, 0.5f, 0f), 1f);
            Assert.AreEqual(g.r, g.g, 1e-5f);
            Assert.AreEqual(g.g, g.b, 1e-5f);
        }

        void CreateRig(float hour)
        {
            _sun = new GameObject("Sun (test)").AddComponent<Light>();
            _sun.type = LightType.Directional;
            _rig = new GameObject("Rig (test)").AddComponent<LightingRig>();
            _rig.sun = _sun;
            _rig.day = new DayCycleSettings { startHour = hour, dayLengthMinutes = 24f };
            TestUtil.Call(_rig, "Awake");
        }

        [Test]
        public void Rig_DrivesSunAmbientAndFog()
        {
            CreateRig(13f);
            var expected = _rig.Current;
            Assert.AreEqual(expected.sunIntensity, _sun.intensity, 1e-4f);
            Assert.AreEqual(AmbientMode.Trilight, RenderSettings.ambientMode);
            AssertColor(expected.skyAmbient, RenderSettings.ambientSkyColor);
            Assert.AreEqual(expected.fogDensity, RenderSettings.fogDensity, 1e-6f);
            Assert.Less((_sun.transform.rotation * Vector3.forward).y, 0f);
        }

        [Test]
        public void Rig_TicksTheClock()
        {
            CreateRig(12f);
            _rig.Tick(60f);
            Assert.AreEqual(13f, _rig.Hour, 1e-3f);
            _rig.SetHour(0f);
            Assert.Less(_rig.Daylight01, 0.01f);
            Assert.Less(_sun.intensity, 0.3f);
        }

        [Test]
        public void Rig_CanLeaveAmbientAndFogAlone()
        {
            CreateRig(12f);
            RenderSettings.fogDensity = 0.123f;
            _rig.controlFog = false;
            _rig.controlAmbient = false;
            _rig.SetHour(3f);
            Assert.AreEqual(0.123f, RenderSettings.fogDensity, 1e-6f);
        }
    }
}

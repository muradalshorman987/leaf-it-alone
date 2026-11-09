using FallenYard.VFX;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.VFX
{
    public class ColorGradeTests
    {
        YardGradeTable _table;

        [TearDown]
        public void TearDown()
        {
            if (_table != null) Object.DestroyImmediate(_table);
        }

        [Test]
        public void Lerp_HitsEndpointsAndClamps()
        {
            var a = ColorGrade.DefaultMessy;
            var b = ColorGrade.DefaultRestored;
            Assert.AreEqual(a.saturation, ColorGrade.Lerp(a, b, 0f).saturation, 1e-5f);
            Assert.AreEqual(b.saturation, ColorGrade.Lerp(a, b, 1f).saturation, 1e-5f);
            Assert.AreEqual(b.saturation, ColorGrade.Lerp(a, b, 3f).saturation, 1e-5f);
            Assert.AreEqual((a.bloom + b.bloom) / 2f, ColorGrade.Lerp(a, b, 0.5f).bloom, 1e-5f);
        }

        [Test]
        public void RestoredYard_IsRicherAndWarmerThanMessy()
        {
            Assert.Greater(ColorGrade.DefaultRestored.saturation, ColorGrade.DefaultMessy.saturation);
            Assert.Greater(ColorGrade.DefaultRestored.temperature, ColorGrade.DefaultMessy.temperature);
            Assert.Less(ColorGrade.DefaultRestored.vignette, ColorGrade.DefaultMessy.vignette);
        }

        [Test]
        public void Compose_CalmMiddayChangesNothing()
        {
            var g = ColorGrade.DefaultMessy;
            var c = ColorGrade.Compose(g, WeatherSample.Calm, 1f, 0f);
            Assert.AreEqual(g.saturation, c.saturation, 1e-5f);
            Assert.AreEqual(g.temperature, c.temperature, 1e-5f);
            Assert.AreEqual(g.postExposure, c.postExposure, 1e-5f);
        }

        [Test]
        public void Compose_CloudsMute_NightCools_CelebrationGlows()
        {
            var g = ColorGrade.DefaultMessy;
            var cloudy = WeatherSample.Calm;
            cloudy.cloudCover = 1f;
            Assert.Less(ColorGrade.Compose(g, cloudy, 1f, 0f).saturation, g.saturation);
            Assert.Less(ColorGrade.Compose(g, WeatherSample.Calm, 0f, 0f).temperature, g.temperature);
            var party = ColorGrade.Compose(g, WeatherSample.Calm, 1f, 1f);
            Assert.Greater(party.bloom, g.bloom);
            Assert.Greater(party.postExposure, g.postExposure);
        }

        [Test]
        public void Clamp_KeepsValuesInUrpRanges()
        {
            var g = new ColorGrade { saturation = 500f, contrast = -500f, vignette = 2f, bloom = -1f };
            var c = ColorGrade.Clamp(g);
            Assert.AreEqual(100f, c.saturation);
            Assert.AreEqual(-100f, c.contrast);
            Assert.AreEqual(1f, c.vignette);
            Assert.AreEqual(0f, c.bloom);
        }

        [Test]
        public void Table_UsesYardEntry_OrFallsBackToDefaults()
        {
            _table = ScriptableObject.CreateInstance<YardGradeTable>();
            var special = new YardGrade { yardId = "yard_02", messy = ColorGrade.Neutral, restored = ColorGrade.Neutral };
            special.restored.saturation = 40f;
            _table.yards.Add(special);

            Assert.AreEqual(40f, _table.Evaluate("yard_02", 1f).saturation, 1e-5f);
            Assert.AreEqual(20f, _table.Evaluate("yard_02", 0.5f).saturation, 1e-5f);
            Assert.AreEqual(_table.defaultRestored.saturation, _table.Evaluate("unknown", 1f).saturation, 1e-5f);
            Assert.AreEqual(_table.defaultMessy.saturation, _table.Evaluate(null, 0f).saturation, 1e-5f);
        }

        [Test]
        public void Transition_MovesSmoothlyTowardsBlendOfCleanAndRestoration()
        {
            var t = new RestorationTransition { cleanWeight = 0.4f, smoothingSeconds = 1f };
            t.SetClean(1f);
            t.SetRestoration(0.5f);
            Assert.AreEqual(0.7f, t.Target, 1e-5f);

            t.Tick(1f);
            Assert.AreEqual(0.7f * (1f - Mathf.Exp(-1f)), t.Value, 1e-4f);
            for (int i = 0; i < 50; i++) t.Tick(0.5f);
            Assert.AreEqual(0.7f, t.Value, 1e-3f);
        }

        [Test]
        public void Transition_SnapJumpsAndClampsInputs()
        {
            var t = new RestorationTransition();
            t.SetClean(5f);
            t.SetRestoration(-1f);
            t.Snap();
            Assert.AreEqual(t.cleanWeight, t.Value, 1e-5f);
        }

        [Test]
        public void Transition_CelebrationFades()
        {
            var t = new RestorationTransition { celebrateSeconds = 2f };
            t.StartCelebration();
            Assert.AreEqual(1f, t.Celebrate);
            t.Tick(1f);
            Assert.AreEqual(0.5f, t.Celebrate, 1e-5f);
            t.Tick(5f);
            Assert.AreEqual(0f, t.Celebrate);
        }
    }
}

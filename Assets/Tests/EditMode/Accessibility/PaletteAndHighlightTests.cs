using System.Collections.Generic;
using FallenYard.Accessibility;
using FallenYard.Leaves;
using FallenYard.Settings;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Accessibility
{
    public class PaletteAndHighlightTests
    {
        static readonly (PaletteRole, PaletteRole)[] Pairs =
        {
            (PaletteRole.Pile, PaletteRole.PileReady),
            (PaletteRole.Progress, PaletteRole.ProgressDone),
            (PaletteRole.Positive, PaletteRole.Warning),
        };

        [TestCase(ColorBlindMode.Deuteranopia)]
        [TestCase(ColorBlindMode.Protanopia)]
        [TestCase(ColorBlindMode.Tritanopia)]
        public void ColourBlindPalettes_KeepPairsApart_ForThatVision(ColorBlindMode mode)
        {
            foreach (var (a, b) in Pairs)
            {
                float d = AccessibilityPalette.SimulatedDistance(AccessibilityPalette.Get(mode, a), AccessibilityPalette.Get(mode, b), mode);
                Assert.Greater(d, 40f, $"{mode}: {a} vs {b} too similar ({d:0.0})");
            }
        }

        [TestCase(ColorBlindMode.Deuteranopia)]
        [TestCase(ColorBlindMode.Protanopia)]
        public void ColourBlindPalette_BeatsTheDefault_ForPiles(ColorBlindMode mode)
        {
            float tuned = AccessibilityPalette.SimulatedDistance(
                AccessibilityPalette.Get(mode, PaletteRole.Pile), AccessibilityPalette.Get(mode, PaletteRole.PileReady), mode);
            float baseline = AccessibilityPalette.SimulatedDistance(
                AccessibilityPalette.Get(ColorBlindMode.Off, PaletteRole.Pile), AccessibilityPalette.Get(ColorBlindMode.Off, PaletteRole.PileReady), mode);
            Assert.Greater(tuned, baseline * 2f);
        }

        [Test]
        public void EveryModeAndRole_HasAnOpaqueColour()
        {
            foreach (ColorBlindMode mode in System.Enum.GetValues(typeof(ColorBlindMode)))
            foreach (PaletteRole role in System.Enum.GetValues(typeof(PaletteRole)))
                Assert.AreEqual(1f, AccessibilityPalette.Get(mode, role).a, $"{mode}/{role}");
        }

        [Test]
        public void UssClass_MatchesStylesheetNames()
        {
            Assert.IsNull(AccessibilityPalette.UssClass(ColorBlindMode.Off));
            Assert.AreEqual("fy-palette--deuteranopia", AccessibilityPalette.UssClass(ColorBlindMode.Deuteranopia));
            Assert.AreEqual("fy-palette--highcontrast", AccessibilityPalette.UssClass(ColorBlindMode.HighContrast));
        }

        [Test]
        public void Simulate_OffIsIdentity()
        {
            var c = new Color(0.2f, 0.5f, 0.7f, 1f);
            Assert.AreEqual(c, AccessibilityPalette.Simulate(c, ColorBlindMode.Off));
        }

        static LeafPile Pile(float x, float z, float radius = 0.8f) =>
            new LeafPile { center = new Vector3(x, 0f, z), radius = radius, leafCount = 100, cellCount = 4 };

        [Test]
        public void FindReadyPile_PicksThePileUnderTheCrosshair()
        {
            var piles = new List<LeafPile> { Pile(5f, 0f), Pile(0f, 2f), Pile(-4f, 0f) };
            var eye = new Vector3(0f, 1.6f, 0f);
            var lookDown = (new Vector3(0f, 0f, 2f) - eye).normalized;
            Assert.AreEqual(1, PileHighlighter.FindReadyPile(piles, eye, lookDown, 3.5f));
        }

        [Test]
        public void FindReadyPile_NoneWhenLookingUpOrTooFar()
        {
            var piles = new List<LeafPile> { Pile(0f, 2f) };
            var eye = new Vector3(0f, 1.6f, 0f);
            Assert.AreEqual(-1, PileHighlighter.FindReadyPile(piles, eye, Vector3.forward, 3.5f), "level gaze never hits the ground");
            var far = new List<LeafPile> { Pile(0f, 10f) };
            var atFar = (new Vector3(0f, 0f, 10f) - eye).normalized;
            Assert.AreEqual(-1, PileHighlighter.FindReadyPile(far, eye, atFar, 3.5f));
            Assert.AreEqual(-1, PileHighlighter.FindReadyPile(null, eye, atFar, 3.5f));
        }

        [Test]
        public void BuildRing_CirclesThePileAtGroundHeight()
        {
            var points = new List<Vector3>();
            PileHighlighter.BuildRing(new Vector3(1f, 0f, 1f), 2f, 16, points, p => 0.5f, 0.05f);
            Assert.AreEqual(16, points.Count);
            foreach (var p in points)
            {
                Assert.AreEqual(0.55f, p.y, 1e-4f);
                Assert.AreEqual(2f, Vector2.Distance(new Vector2(p.x, p.z), new Vector2(1f, 1f)), 1e-3f);
            }
        }

        [Test]
        public void PulseScale_StaysInRange()
        {
            for (float t = 0f; t < 3f; t += 0.07f)
            {
                float s = PileHighlighter.PulseScale(t, 1.5f);
                Assert.GreaterOrEqual(s, 1f);
                Assert.LessOrEqual(s, 1.35f + 1e-4f);
            }
        }

        [Test]
        public void Highlighter_FollowsSettings()
        {
            var go = new GameObject("Highlighter (test)");
            try
            {
                var h = go.AddComponent<PileHighlighter>();
                h.ApplySettings(new GameSettings { colorBlindMode = ColorBlindMode.Tritanopia });
                Assert.AreEqual(ColorBlindMode.Tritanopia, h.Mode);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}

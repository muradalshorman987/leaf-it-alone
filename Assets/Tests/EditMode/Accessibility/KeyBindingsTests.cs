using FallenYard.Core;
using FallenYard.Settings;
using FallenYard.UI;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Accessibility
{
    public class KeyBindingsTests
    {
        [SetUp]
        public void SetUp() => KeyBindings.ResetCurrent();

        [TearDown]
        public void TearDown() => KeyBindings.ResetCurrent();

        [Test]
        public void Defaults_MatchTheOriginalHardcodedKeys()
        {
            var b = new KeyBindings();
            Assert.AreEqual("W", b.Get(GameAction.MoveForward));
            Assert.AreEqual("LeftShift", b.Get(GameAction.Sprint));
            Assert.AreEqual("Mouse0", b.Get(GameAction.UseTool));
            Assert.AreEqual("E", b.Get(GameAction.Interact));
            Assert.AreEqual("Tab", b.Get(GameAction.Shop));
            Assert.AreEqual("Digit4", b.Get(GameAction.Tool4));
            Assert.IsTrue(b.AllDefault);
            Assert.AreEqual("", b.ToJson(), "defaults store nothing");
        }

        [Test]
        public void EveryDefault_IsAValidControl_AndUnique()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var a in KeyBindings.AllActions)
            {
                string c = KeyBindings.DefaultFor(a);
                Assert.IsTrue(KeyBindings.IsValidControl(c), $"{a} -> {c}");
                Assert.IsTrue(seen.Add(c), $"{c} used twice");
            }
        }

        [Test]
        public void Controls_HaveLegacyKeyCodes_AndExcludeReservedKeys()
        {
            foreach (var c in KeyBindings.Controls)
            {
                Assert.AreNotEqual(KeyCode.None, c.LegacyKey, c.Name);
                Assert.IsFalse(string.IsNullOrEmpty(c.DisplayName), c.Name);
            }
            Assert.IsFalse(KeyBindings.IsValidControl("Escape"), "Escape always pauses / cancels");
            Assert.IsFalse(KeyBindings.IsValidControl("F3"), "F3 toggles the debug overlay");
            Assert.IsTrue(KeyBindings.TryGetLegacyKeyCode("Digit1", out var k1));
            Assert.AreEqual(KeyCode.Alpha1, k1);
            Assert.IsTrue(KeyBindings.TryGetLegacyKeyCode("Enter", out var enter));
            Assert.AreEqual(KeyCode.Return, enter);
            Assert.IsTrue(KeyBindings.TryGetLegacyKeyCode("F12", out var f12));
            Assert.AreEqual(KeyCode.F12, f12);
        }

        [Test]
        public void Set_ToAFreeKey_Rebinds()
        {
            var b = new KeyBindings();
            var swapped = b.Set(GameAction.Interact, "F", out bool changed);
            Assert.IsTrue(changed);
            Assert.IsNull(swapped);
            Assert.AreEqual("F", b.Get(GameAction.Interact));
            Assert.AreEqual("F", b.Label(GameAction.Interact));
            Assert.IsFalse(b.IsDefault(GameAction.Interact));
        }

        [Test]
        public void Set_ToAKeyInUse_SwapsSoNothingIsUnbound()
        {
            var b = new KeyBindings();
            var swapped = b.Set(GameAction.Interact, "Tab");
            Assert.AreEqual(GameAction.Shop, swapped);
            Assert.AreEqual("Tab", b.Get(GameAction.Interact));
            Assert.AreEqual("E", b.Get(GameAction.Shop));
        }

        [Test]
        public void Set_InvalidControl_IsIgnored()
        {
            var b = new KeyBindings();
            b.Set(GameAction.Jump, "Escape", out bool changed);
            Assert.IsFalse(changed);
            b.Set(GameAction.Jump, null, out changed);
            Assert.IsFalse(changed);
            Assert.AreEqual("Space", b.Get(GameAction.Jump));
        }

        [Test]
        public void Json_RoundTrip_KeepsOnlyOverrides()
        {
            var b = new KeyBindings();
            b.Set(GameAction.Sprint, "LeftCtrl");
            b.Set(GameAction.UseTool, "Mouse1");
            string json = b.ToJson();
            StringAssert.Contains("LeftCtrl", json);
            StringAssert.DoesNotContain("MoveForward", json);

            var loaded = KeyBindings.FromJson(json);
            Assert.AreEqual("LeftCtrl", loaded.Get(GameAction.Sprint));
            Assert.AreEqual("Mouse1", loaded.Get(GameAction.UseTool));
            Assert.AreEqual("W", loaded.Get(GameAction.MoveForward));
        }

        [Test]
        public void FromJson_SkipsGarbageAndUnknownEntries()
        {
            Assert.IsTrue(KeyBindings.FromJson("not json at all").AllDefault);
            Assert.IsTrue(KeyBindings.FromJson("{\"bindings\":[{\"action\":\"Fly\",\"control\":\"Q\"},{\"action\":\"Jump\",\"control\":\"Escape\"}]}").AllDefault);
            Assert.IsTrue(KeyBindings.FromJson(null).AllDefault);
        }

        [Test]
        public void FromJson_DuplicateControls_StillLeaveEveryActionBound()
        {
            var b = KeyBindings.FromJson("{\"bindings\":[{\"action\":\"Jump\",\"control\":\"E\"},{\"action\":\"Sprint\",\"control\":\"E\"}]}");
            var used = new System.Collections.Generic.HashSet<string>();
            foreach (var a in KeyBindings.AllActions) Assert.IsTrue(used.Add(b.Get(a)), $"{b.Get(a)} bound twice");
            Assert.AreEqual("E", b.Get(GameAction.Sprint));
        }

        [Test]
        public void Current_RaisesChanged_OnlyWhenSomethingChanges()
        {
            int calls = 0;
            KeyBindings.Changed += () => calls++;
            KeyBindings.Current.Set(GameAction.Jump, "J");
            Assert.AreEqual(1, calls);
            KeyBindings.Current.Set(GameAction.Jump, "J");
            Assert.AreEqual(1, calls, "same key again is a no-op");
            new KeyBindings().Set(GameAction.Jump, "K");
            Assert.AreEqual(1, calls, "other instances are silent");
            KeyBindings.Current.ResetToDefaults();
            Assert.AreEqual(2, calls);
        }

        [Test]
        public void ApplyOverrides_LoadsSettingsJsonIntoCurrent()
        {
            var edited = new KeyBindings();
            edited.Set(GameAction.Interact, "F");
            KeyBindings.ApplyOverrides(edited.ToJson());
            Assert.AreEqual("F", GameInput.Label(GameAction.Interact));

            KeyBindings.ApplyOverrides("");
            Assert.AreEqual("E", GameInput.Label(GameAction.Interact), "empty = defaults");
        }

        [Test]
        public void SettingsManagerApply_LoadsBindings()
        {
            string dir = TestUtil.UseTempSaveFolder();
            var go = new GameObject("Settings (test)");
            try
            {
                var sm = go.AddComponent<SettingsManager>();
                sm.applyToEngine = false;
                var edited = new KeyBindings();
                edited.Set(GameAction.Jump, "C");
                sm.Apply(new GameSettings { bindingOverridesJson = edited.ToJson() }, save: false);
                Assert.AreEqual("C", KeyBindings.Current.Get(GameAction.Jump));
            }
            finally
            {
                Object.DestroyImmediate(go);
                SettingsManager.ClearListeners();
                TestUtil.ReleaseTempSaveFolder(dir);
            }
        }

        [Test]
        public void KeyHints_DefaultBindings_MatchTheHudConstant()
        {
            Assert.AreEqual(HudView.HintText, HudFormat.KeyHints(new KeyBindings()));
        }

        [Test]
        public void KeyHints_FollowRebinds()
        {
            var b = new KeyBindings();
            b.Set(GameAction.Interact, "F");
            b.Set(GameAction.Tool1, "Z");
            string hints = HudFormat.KeyHints(b);
            StringAssert.Contains("F bag", hints);
            StringAssert.StartsWith("Z/2/3/4 tools", hints);
        }
    }
}

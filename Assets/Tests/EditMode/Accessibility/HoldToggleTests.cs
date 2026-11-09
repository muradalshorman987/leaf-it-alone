using FallenYard.Core;
using FallenYard.Player;
using FallenYard.Settings;
using FallenYard.Tools;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Accessibility
{
    public class HoldToggleTests
    {
        [Test]
        public void HoldMode_FollowsTheButton()
        {
            var t = new HoldToggle();
            Assert.IsTrue(t.Update(true, true));
            Assert.IsTrue(t.Update(true, false));
            Assert.IsFalse(t.Update(false, false));
        }

        [Test]
        public void ToggleMode_FlipsOnEachPress()
        {
            var t = new HoldToggle(toggleMode: true);
            Assert.IsTrue(t.Update(true, true), "first press switches on");
            Assert.IsTrue(t.Update(false, false), "stays on after release");
            Assert.IsFalse(t.Update(true, true), "second press switches off");
            Assert.IsFalse(t.Update(false, false));
        }

        [Test]
        public void Reset_SwitchesOff()
        {
            var t = new HoldToggle(true);
            t.Update(true, true);
            t.Reset();
            Assert.IsFalse(t.Active);
        }

        [Test]
        public void ToolController_ToggleToolUse_LatchesAndResetsOnSettingsChange()
        {
            var go = new GameObject("Tools (test)");
            try
            {
                var tc = go.AddComponent<ToolController>();
                tc.ApplySettings(new GameSettings { toggleToolUse = true });
                Assert.IsTrue(tc.UpdateUse(true, true));
                Assert.IsTrue(tc.UpdateUse(false, false), "toggle keeps raking with the button up");
                Assert.IsFalse(tc.UpdateUse(true, true));

                tc.UpdateUse(true, true);
                tc.ApplySettings(new GameSettings { toggleToolUse = false });
                Assert.IsFalse(tc.UseActive, "turning the option off releases the tool");
                Assert.IsFalse(tc.UpdateUse(false, false));
                Assert.IsTrue(tc.UpdateUse(true, false), "hold mode again");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Player_ToggleSprint_LatchesUntilStopped()
        {
            var go = new GameObject("Player (test)");
            try
            {
                var fpc = go.AddComponent<FirstPersonController>();
                fpc.ApplySettings(new GameSettings { toggleSprint = true });
                Assert.IsTrue(fpc.toggleSprint);

                var forward = new Vector2(0f, 1f);
                Assert.IsTrue(fpc.UpdateSprint(true, true, forward));
                Assert.IsTrue(fpc.UpdateSprint(false, false, forward), "keeps sprinting after release");
                Assert.IsFalse(fpc.UpdateSprint(false, false, Vector2.zero), "stopping ends the sprint");
                Assert.IsFalse(fpc.UpdateSprint(false, false, forward));

                fpc.ApplySettings(new GameSettings { toggleSprint = false });
                Assert.IsTrue(fpc.UpdateSprint(true, false, forward));
                Assert.IsFalse(fpc.UpdateSprint(false, false, forward), "hold mode");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}

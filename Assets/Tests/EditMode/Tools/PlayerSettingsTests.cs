using FallenYard.Player;
using FallenYard.Settings;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Tools
{
    public class PlayerSettingsTests
    {
        [Test]
        public void ApplySettings_CopiesLookFovAndHeadBob()
        {
            var go = new GameObject("Player (test)");
            try
            {
                var fpc = go.AddComponent<FirstPersonController>();
                var s = new GameSettings { mouseSensitivity = 3.5f, invertY = true, fieldOfView = 90f, headBob = false };
                fpc.ApplySettings(s);

                Assert.AreEqual(3.5f, fpc.lookSensitivity, 1e-4f);
                Assert.IsTrue(fpc.invertY);
                Assert.AreEqual(90f, fpc.baseFov, 1e-4f);
                Assert.IsFalse(fpc.headBobEnabled);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}

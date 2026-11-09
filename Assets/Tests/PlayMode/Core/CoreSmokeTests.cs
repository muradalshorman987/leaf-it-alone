using System.Collections;
using System.IO;
using FallenYard.Core;
using FallenYard.Leaves;
using FallenYard.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FallenYard.Tests.Core
{
    /// <summary>Runs the real Unity loop (Awake/Start/Update). Any logged error fails the test.</summary>
    public class CoreSmokeTests
    {
        string _saveDir;

        [SetUp]
        public void SetUp()
        {
            GameEvents.ClearAllSubscribers();
            _saveDir = Path.Combine(Path.GetTempPath(), "FallenYardPlayTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);
            SaveSystem.DirectoryOverride = _saveDir;
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.DirectoryOverride = null;
            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
            GameEvents.ClearAllSubscribers();
        }

        [UnityTest]
        public IEnumerator GameManager_AddCoins_RaisesGameEvent()
        {
            var go = new GameObject("GameManager (play test)");
            var gm = go.AddComponent<GameManager>();
            int reported = -1;
            GameEvents.CoinsChanged += c => reported = c;
            yield return null;

            Assert.AreSame(gm, GameManager.Instance);
            gm.AddCoins(15);
            Assert.AreEqual(15, reported);

            Object.Destroy(go);
            yield return null;
            Assert.IsNull(GameManager.Instance);
        }

        [UnityTest]
        public IEnumerator LeafField_SimulatesFramesWithoutErrors()
        {
            var go = new GameObject("LeafField (play test)");
            go.SetActive(false);
            var field = go.AddComponent<LeafField>();
            field.leafCount = 2000;
            field.yardSize = new Vector2(10f, 10f);
            go.SetActive(true); // Awake + Start -> Spawn

            for (int i = 0; i < 10; i++) yield return null;

            Assert.AreSame(field, LeafField.Instance);
            Assert.AreEqual(2000, field.TotalLeaves);
            Assert.AreEqual(0, field.CollectedLeaves);

            var mesh = field.leafMesh;
            Object.Destroy(go);
            if (mesh != null) Object.Destroy(mesh);
            yield return null;
        }
    }
}

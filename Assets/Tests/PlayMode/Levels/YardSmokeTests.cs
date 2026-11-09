using System.Collections;
using System.IO;
using FallenYard.Core;
using FallenYard.Leaves;
using FallenYard.Levels;
using FallenYard.Progression;
using FallenYard.Save;
using FallenYard.Tools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FallenYard.Tests.Levels
{
    /// <summary>
    /// Builds each Hollow Creek yard at runtime from its layout (greybox, leaves, wind, goal)
    /// and runs it for a few frames. Any logged error fails the test.
    /// </summary>
    public class YardSmokeTests
    {
        static readonly string[] Yards = YardLayouts.Order;
        string _saveDir;

        [SetUp]
        public void SetUp()
        {
            GameEvents.ClearAllSubscribers();
            _saveDir = Path.Combine(Path.GetTempPath(), "FallenYardYardTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);
            SaveSystem.DirectoryOverride = _saveDir;
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.DirectoryOverride = null;
            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
            GameEvents.ClearAllSubscribers();
            GameState.SkipTitleOnNextLoad = false;
        }

        [UnityTest]
        public IEnumerator Yard_LoadsAndRunsWithoutErrors([ValueSource(nameof(Yards))] string yardId)
        {
            var def = YardLayouts.Create(yardId);
            string loaded = null;
            GameEvents.YardLoaded += id => loaded = id;

            var fieldGo = new GameObject("LeafField");
            var field = fieldGo.AddComponent<LeafField>();
            var managers = new GameObject("Managers");
            managers.AddComponent<GameManager>().autosaveInterval = 0f;
            var progress = managers.AddComponent<YardProgress>();
            managers.AddComponent<WindSystem>();
            managers.AddComponent<LevelFlow>();

            var loaderGo = new GameObject("YardLoader");
            loaderGo.SetActive(false);
            var loader = loaderGo.AddComponent<YardLoader>();
            loader.definition = def;
            loader.buildGreyboxAtRuntime = true;
            loaderGo.SetActive(true); // Awake -> Apply

            for (int i = 0; i < 10; i++) yield return null;

            Assert.AreEqual(yardId, loaded);
            Assert.AreEqual(def.leafCount, field.TotalLeaves);
            Assert.AreEqual(yardId, progress.yardId);
            Assert.IsNotNull(loader.Greybox);
            Assert.IsNotNull(CompostBin.Instance);
            Assert.AreEqual(yardId, LevelFlow.Instance.currentYardId);

            // Restoration reaches the yard: flowers grow when fertilizer is spread.
            var restoration = loader.Greybox.root.GetComponent<YardRestoration>();
            GameEvents.RaiseYardRestorationChanged(1f);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(1f, restoration.Target);
            Assert.Greater(restoration.Shown, 0f);

            Object.Destroy(loaderGo);
            Object.Destroy(managers);
            Object.Destroy(fieldGo);
            Object.Destroy(def);
            yield return null;
        }
    }
}

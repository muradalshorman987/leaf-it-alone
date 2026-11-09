using FallenYard.Save;
using FallenYard.Tutorial;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Tutorial
{
    public class TutorialSaveTests
    {
        [Test]
        public void NewSave_StartsTheTutorial()
        {
            var d = new SaveData();
            Assert.AreEqual(4, SaveData.CurrentVersion);
            Assert.IsNotNull(d.tutorial);
            Assert.AreEqual((int)TutorialStep.Move, d.tutorial.step);
        }

        [Test]
        public void V3Save_FreshPlayer_GetsTheTutorial()
        {
            var d = JsonUtility.FromJson<SaveData>("{\"version\":3,\"coins\":0}");
            d.Migrate();
            Assert.AreEqual(SaveData.CurrentVersion, d.version);
            Assert.AreEqual((int)TutorialStep.Move, d.tutorial.step);
        }

        [Test]
        public void V3Save_ExistingPlayer_SkipsTheTutorial()
        {
            var d = JsonUtility.FromJson<SaveData>("{\"version\":3,\"coins\":120,\"completedYards\":[\"yard_01\"]}");
            d.Migrate();
            Assert.AreEqual((int)TutorialStep.Done, d.tutorial.step);
            Assert.IsFalse(d.tutorial.skipped);
        }

        [Test]
        public void V1Save_MigratesAllTheWay()
        {
            var d = JsonUtility.FromJson<SaveData>("{\"version\":1,\"coins\":5}");
            d.Migrate();
            Assert.AreEqual(SaveData.CurrentVersion, d.version);
            Assert.AreEqual((int)TutorialStep.Done, d.tutorial.step);
            Assert.IsTrue(SaveMigrator.HasCompleteChain());
        }

        [Test]
        public void Normalize_RepairsBrokenTutorialData()
        {
            var d = new SaveData { tutorial = null };
            d.Migrate();
            Assert.IsNotNull(d.tutorial);
            Assert.IsNotNull(d.tutorial.hintsShown);

            var e = new SaveData();
            e.tutorial.step = -4;
            e.tutorial.hintsShown = null;
            e.Migrate();
            Assert.AreEqual(0, e.tutorial.step);
            Assert.IsNotNull(e.tutorial.hintsShown);
        }

        [Test]
        public void TutorialState_SurvivesJson()
        {
            var d = new SaveData();
            d.tutorial.step = 3;
            d.tutorial.hintsShown.Add("wind");
            var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(d)).Migrate();
            Assert.AreEqual(3, back.tutorial.step);
            Assert.AreEqual(1, back.tutorial.TimesShown("wind"));
        }
    }
}

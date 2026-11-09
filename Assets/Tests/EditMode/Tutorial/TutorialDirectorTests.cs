using FallenYard.Core;
using FallenYard.Settings;
using FallenYard.Tutorial;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Tutorial
{
    public class TutorialDirectorTests
    {
        string _dir;
        GameManager _gm;
        TutorialDirector _td;

        [SetUp]
        public void SetUp()
        {
            GameEvents.ClearAllSubscribers();
            KeyBindings.ResetCurrent();
            _dir = TestUtil.UseTempSaveFolder();
            _gm = TestUtil.CreateGameManager();
            _td = new GameObject("Tutorial (test)").AddComponent<TutorialDirector>();
            TestUtil.Call(_td, "Awake");
            TestUtil.Call(_td, "OnEnable");
            TestUtil.Call(_td, "Start");
        }

        [TearDown]
        public void TearDown()
        {
            TestUtil.Call(_td, "OnDisable");
            TestUtil.Call(_td, "OnDestroy");
            TestUtil.Destroy(_td);
            TestUtil.DestroySingleton(_gm);
            TestUtil.ReleaseTempSaveFolder(_dir);
            GameEvents.ClearAllSubscribers();
            KeyBindings.ResetCurrent();
            SettingsManager.ClearListeners();
        }

        [Test]
        public void NoUIManager_MeansPlaying_AndShowsTheFirstStep()
        {
            Assert.AreEqual(TutorialStep.Move, _td.Model.Step);
            Assert.IsTrue(_td.HasCard);
            Assert.AreEqual("Tutorial 1/6", _td.CardTitle);
            StringAssert.Contains("WASD", _td.CardText);
        }

        [Test]
        public void CardHides_InMenus_AndComesBack()
        {
            int changes = 0;
            _td.CardChanged += () => changes++;
            GameEvents.RaiseStateChanged(GamePhase.Paused);
            Assert.IsFalse(_td.HasCard);
            GameEvents.RaiseStateChanged(GamePhase.Playing);
            Assert.IsTrue(_td.HasCard);
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void GameEvents_DriveTheSteps_AndProgressIsSaved()
        {
            GameEvents.RaisePileBagged(60, 30);
            Assert.AreEqual(TutorialStep.Coins, _td.Model.Step);
            Assert.AreEqual((int)TutorialStep.Coins, _gm.Data.tutorial.step, "saved on each step");

            GameEvents.RaiseYardCompleted("yard_01");
            Assert.IsFalse(_td.Model.IsActive);
            Assert.AreEqual((int)TutorialStep.Done, _gm.Data.tutorial.step);
        }

        [Test]
        public void RebindingKeys_UpdatesTheCard()
        {
            KeyBindings.Current.Set(GameAction.MoveForward, "I");
            StringAssert.Contains("IASD", _td.CardText);
        }

        [Test]
        public void TurningHintsOff_HidesTheCard()
        {
            _td.ApplySettings(new GameSettings { tutorialHints = false });
            Assert.IsFalse(_td.HasCard);
            _td.ApplySettings(new GameSettings { tutorialHints = true });
            Assert.IsTrue(_td.HasCard);
        }

        [Test]
        public void Hints_ShowAfterTheTutorial_AndCountInTheSave()
        {
            _td.SkipTutorial();
            Assert.IsFalse(_td.HasCard);
            _td.ShowHint("wind");
            Assert.IsTrue(_td.CardIsHint);
            Assert.AreEqual("Tip", _td.CardTitle);
            Assert.AreEqual(1, _gm.Data.tutorial.TimesShown("wind"));

            _td.Step(_td.hintSeconds + 0.1f, 0.016f);
            Assert.IsFalse(_td.HasCard, "hints expire");
        }

        [Test]
        public void StrongGust_TriggersTheWindHint_Once()
        {
            _td.SkipTutorial();
            GameEvents.RaiseWindGust(Vector3.right, 1f);
            _td.Step(0.016f, 0.016f);
            Assert.AreEqual("Tip", _td.CardTitle);
            StringAssert.Contains("Gusts", _td.CardText);

            _td.Step(_td.hintSeconds + 1f, 0.016f);
            GameEvents.RaiseWindGust(Vector3.right, 1f);
            _td.Step(0.016f, 0.016f);
            Assert.IsFalse(_td.HasCard, "wind hint is once per save");
        }

        [Test]
        public void Replay_RestartsFromTheBeginning()
        {
            _td.SkipTutorial();
            _td.ReplayTutorial();
            Assert.AreEqual(TutorialStep.Move, _td.Model.Step);
            Assert.IsTrue(_td.HasCard);
        }

        [Test]
        public void CaptureAndRestore_UseTheSave()
        {
            var data = new FallenYard.Save.SaveData();
            data.tutorial.step = (int)TutorialStep.Shop;
            _td.RestoreState(data);
            Assert.AreEqual(TutorialStep.Shop, _td.Model.Step);

            var other = new FallenYard.Save.SaveData();
            _td.CaptureState(other);
            Assert.AreEqual((int)TutorialStep.Shop, other.tutorial.step);
        }
    }
}

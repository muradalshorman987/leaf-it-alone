using FallenYard.Core;
using FallenYard.Save;
using FallenYard.Tutorial;
using NUnit.Framework;

namespace FallenYard.Tests.Tutorial
{
    public class TutorialModelTests
    {
        [Test]
        public void DoneIndex_MatchesTheSaveMigratorConstant()
        {
            Assert.AreEqual(SaveMigrator.TutorialStepDone, (int)TutorialStep.Done);
        }

        [Test]
        public void FullRun_AdvancesOnTheThingEachStepTeaches()
        {
            var m = new TutorialModel();
            Assert.AreEqual(TutorialStep.Move, m.Step);

            m.NotifyMoved(1f);
            Assert.AreEqual(TutorialStep.Move, m.Step, "needs 1.5 s of walking");
            m.NotifyMoved(0.6f);
            Assert.AreEqual(TutorialStep.Rake, m.Step);

            m.NotifyPhase(GamePhase.Shop);
            Assert.AreEqual(TutorialStep.Rake, m.Step, "opening the shop early doesn't skip raking");

            m.NotifyPileFormed();
            Assert.AreEqual(TutorialStep.Bag, m.Step);

            m.NotifyPileBagged();
            Assert.AreEqual(TutorialStep.Coins, m.Step);
            m.Tick(m.coinsStepSeconds + 0.1f);
            Assert.AreEqual(TutorialStep.Shop, m.Step);

            m.NotifyPhase(GamePhase.Shop);
            Assert.AreEqual(TutorialStep.Finish, m.Step);

            m.NotifyYardCompleted();
            Assert.AreEqual(TutorialStep.Done, m.Step);
            Assert.IsFalse(m.IsActive);
            Assert.AreEqual("", m.Prompt(new KeyBindings()));
        }

        [Test]
        public void RakingBeforeWalking_StillCountsAsAPile()
        {
            var m = new TutorialModel();
            m.NotifyPileFormed();
            Assert.AreEqual(TutorialStep.Bag, m.Step);
        }

        [Test]
        public void CoinsStep_EndsEarlyOnTheNextBag()
        {
            var m = new TutorialModel(TutorialStep.Coins);
            m.NotifyPileBagged();
            Assert.AreEqual(TutorialStep.Shop, m.Step);
        }

        [Test]
        public void YardCompletedEarly_FinishesTheTutorial()
        {
            var m = new TutorialModel(TutorialStep.Rake);
            m.NotifyYardCompleted();
            Assert.AreEqual(TutorialStep.Done, m.Step);
        }

        [Test]
        public void Skip_AndRestart()
        {
            var m = new TutorialModel(TutorialStep.Bag);
            int changes = 0;
            m.StepChanged += _ => changes++;
            m.Skip();
            Assert.IsTrue(m.Skipped);
            Assert.IsFalse(m.IsActive);
            m.Restart();
            Assert.IsFalse(m.Skipped);
            Assert.AreEqual(TutorialStep.Move, m.Step);
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void Prompts_UseThePlayersKeys()
        {
            var keys = new KeyBindings();
            var m = new TutorialModel();
            StringAssert.Contains("WASD", m.Prompt(keys));

            keys.Set(GameAction.MoveForward, "UpArrow");
            StringAssert.Contains("Up / A / S / D", m.Prompt(keys));

            m = new TutorialModel(TutorialStep.Rake);
            StringAssert.Contains("Hold Left Mouse", m.Prompt(keys));
            StringAssert.Contains("Press Left Mouse", m.Prompt(keys, toggleToolUse: true));

            keys.Set(GameAction.Interact, "F");
            m = new TutorialModel(TutorialStep.Bag);
            StringAssert.Contains("press F", m.Prompt(keys));

            m = new TutorialModel(TutorialStep.Shop);
            StringAssert.Contains("Press Tab", m.Prompt(keys));
        }

        [Test]
        public void EveryActiveStep_HasPromptAndProgress()
        {
            for (var s = TutorialStep.Move; s < TutorialStep.Done; s++)
            {
                var m = new TutorialModel(s);
                Assert.IsNotEmpty(m.Prompt(null), s.ToString());
                StringAssert.StartsWith("Tutorial ", m.Progress);
            }
            Assert.AreEqual("Tutorial 1/6", new TutorialModel().Progress);
        }

        [Test]
        public void StoreAndLoad_RoundTrip()
        {
            var save = new TutorialSave();
            var m = new TutorialModel(TutorialStep.Shop);
            m.Store(save);
            Assert.AreEqual((int)TutorialStep.Shop, save.step);

            var n = new TutorialModel();
            n.Load(save);
            Assert.AreEqual(TutorialStep.Shop, n.Step);

            n.Load(new TutorialSave { step = 99, skipped = true });
            Assert.AreEqual(TutorialStep.Done, n.Step, "out-of-range clamps to done");
            Assert.IsTrue(n.Skipped);
        }
    }
}

using FallenYard.Core;
using FallenYard.Save;
using FallenYard.Tutorial;
using NUnit.Framework;

namespace FallenYard.Tests.Tutorial
{
    public class HintRulesTests
    {
        static HintContext Quiet() => new HintContext
        {
            Phase = GamePhase.Playing,
            TutorialActive = false,
            Coins = 0,
            CheapestPurchase = -1,
            PileCount = 0,
            SecondsSinceBag = 0f,
            SecondsSinceLastHint = 999f,
        };

        [Test]
        public void NothingToSay_ReturnsNull()
        {
            Assert.IsNull(HintRules.Choose(Quiet(), new TutorialSave()));
        }

        [Test]
        public void NoHints_DuringTutorialOrMenus()
        {
            var c = Quiet();
            c.ToolJustUnlocked = true;
            c.TutorialActive = true;
            Assert.IsNull(HintRules.Choose(c, new TutorialSave()));
            c.TutorialActive = false;
            c.Phase = GamePhase.Shop;
            Assert.IsNull(HintRules.Choose(c, new TutorialSave()));
        }

        [Test]
        public void AffordUpgrade_WhenCoinsCoverTheCheapestItem()
        {
            var c = Quiet();
            c.Coins = 49;
            c.CheapestPurchase = 50;
            Assert.IsNull(HintRules.Choose(c, new TutorialSave()));
            c.Coins = 50;
            Assert.AreEqual(HintRules.AffordUpgrade, HintRules.Choose(c, new TutorialSave()));
        }

        [Test]
        public void IdlePiles_AfterAWhileWithoutBagging()
        {
            var c = Quiet();
            c.PileCount = 2;
            c.SecondsSinceBag = HintRules.IdleSeconds - 1f;
            Assert.IsNull(HintRules.Choose(c, new TutorialSave()));
            c.SecondsSinceBag = HintRules.IdleSeconds;
            Assert.AreEqual(HintRules.IdlePiles, HintRules.Choose(c, new TutorialSave()));
        }

        [Test]
        public void EventHints_IgnoreTheGap_OthersWait()
        {
            var c = Quiet();
            c.SecondsSinceLastHint = 1f;
            c.Coins = 100;
            c.CheapestPurchase = 10;
            Assert.IsNull(HintRules.Choose(c, new TutorialSave()), "too soon after the last hint");
            c.GustJustHappened = true;
            Assert.AreEqual(HintRules.Wind, HintRules.Choose(c, new TutorialSave()));
            c.ToolJustUnlocked = true;
            Assert.AreEqual(HintRules.NewTool, HintRules.Choose(c, new TutorialSave()), "new tool beats wind");
        }

        [Test]
        public void HintsStopAfterTheirLimit()
        {
            var save = new TutorialSave();
            var c = Quiet();
            c.GustJustHappened = true;
            Assert.AreEqual(HintRules.Wind, HintRules.Choose(c, save));
            HintRules.MarkShown(HintRules.Wind, save);
            Assert.IsNull(HintRules.Choose(c, save), "wind hint shows once per save");
            Assert.AreEqual(1, save.TimesShown(HintRules.Wind));
        }

        [Test]
        public void Texts_UseThePlayersKeys()
        {
            var keys = new KeyBindings();
            keys.Set(GameAction.Shop, "B");
            StringAssert.Contains("Press B", HintRules.Text(HintRules.AffordUpgrade, keys));
            StringAssert.Contains("1-4", HintRules.Text(HintRules.NewTool, null));
            Assert.AreEqual("", HintRules.Text("nope", keys));
            foreach (var id in new[] { HintRules.AffordUpgrade, HintRules.IdlePiles, HintRules.Wind, HintRules.NewTool })
                Assert.IsNotEmpty(HintRules.Text(id, keys), id);
        }
    }
}

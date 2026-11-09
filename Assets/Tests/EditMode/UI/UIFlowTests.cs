using System.Collections.Generic;
using FallenYard.Core;
using FallenYard.UI;
using NUnit.Framework;

namespace FallenYard.Tests.UI
{
    public class UIFlowTests
    {
        List<GamePhase> _raised;

        [SetUp]
        public void SetUp()
        {
            GameEvents.ClearAllSubscribers();
            _raised = new List<GamePhase>();
            GameEvents.StateChanged += p => _raised.Add(p);
        }

        [TearDown]
        public void TearDown() => GameEvents.ClearAllSubscribers();

        [Test]
        public void StartGame_FromTitle_RaisesPlaying()
        {
            var flow = new UIFlow(GamePhase.TitleMenu);
            Assert.IsTrue(flow.StartGame());
            Assert.AreEqual(GamePhase.Playing, flow.Phase);
            CollectionAssert.AreEqual(new[] { GamePhase.Playing }, _raised);
        }

        [Test]
        public void StartGame_WhilePlaying_DoesNothing()
        {
            var flow = new UIFlow(GamePhase.Playing);
            Assert.IsFalse(flow.StartGame());
            Assert.IsEmpty(_raised);
        }

        [Test]
        public void SetPhase_SamePhase_DoesNotRaise()
        {
            var flow = new UIFlow(GamePhase.Playing);
            Assert.IsFalse(flow.SetPhase(GamePhase.Playing));
            Assert.IsEmpty(_raised);
        }

        [Test]
        public void TogglePause_GoesBothWays()
        {
            var flow = new UIFlow(GamePhase.Playing);
            flow.TogglePause();
            Assert.AreEqual(GamePhase.Paused, flow.Phase);
            flow.TogglePause();
            Assert.AreEqual(GamePhase.Playing, flow.Phase);
            CollectionAssert.AreEqual(new[] { GamePhase.Paused, GamePhase.Playing }, _raised);
        }

        [Test]
        public void TogglePause_ClosesShopAndYardCard()
        {
            var flow = new UIFlow(GamePhase.Shop);
            flow.TogglePause();
            Assert.AreEqual(GamePhase.Playing, flow.Phase);

            flow = new UIFlow(GamePhase.YardComplete);
            flow.TogglePause();
            Assert.AreEqual(GamePhase.Playing, flow.Phase);
        }

        [Test]
        public void TogglePause_OnTitle_DoesNothing()
        {
            var flow = new UIFlow(GamePhase.TitleMenu);
            Assert.IsFalse(flow.TogglePause());
            Assert.AreEqual(GamePhase.TitleMenu, flow.Phase);
        }

        [Test]
        public void ToggleShop_OpensFromPlayPauseAndYardCard_ClosesToPlaying()
        {
            foreach (var from in new[] { GamePhase.Playing, GamePhase.Paused, GamePhase.YardComplete })
            {
                var flow = new UIFlow(from);
                Assert.IsTrue(flow.ToggleShop(), from.ToString());
                Assert.AreEqual(GamePhase.Shop, flow.Phase);
                Assert.IsTrue(flow.ToggleShop());
                Assert.AreEqual(GamePhase.Playing, flow.Phase);
            }
        }

        [Test]
        public void ToggleShop_OnTitle_DoesNothing()
        {
            var flow = new UIFlow(GamePhase.TitleMenu);
            Assert.IsFalse(flow.ToggleShop());
            Assert.AreEqual(GamePhase.TitleMenu, flow.Phase);
        }

        [Test]
        public void Resume_DoesNotLeaveTitle()
        {
            var flow = new UIFlow(GamePhase.TitleMenu);
            Assert.IsFalse(flow.Resume());
            flow = new UIFlow(GamePhase.Paused);
            Assert.IsTrue(flow.Resume());
            Assert.AreEqual(GamePhase.Playing, flow.Phase);
        }

        [Test]
        public void ReturnToTitle_FromPause()
        {
            var flow = new UIFlow(GamePhase.Paused);
            Assert.IsTrue(flow.ReturnToTitle());
            Assert.AreEqual(GamePhase.TitleMenu, flow.Phase);
        }

        [Test]
        public void YardCompleted_OnlyInterruptsTheYard()
        {
            var flow = new UIFlow(GamePhase.Playing);
            Assert.IsTrue(flow.OnYardCompleted());
            Assert.AreEqual(GamePhase.YardComplete, flow.Phase);

            flow = new UIFlow(GamePhase.TitleMenu);
            Assert.IsFalse(flow.OnYardCompleted());
            flow = new UIFlow(GamePhase.Paused);
            Assert.IsFalse(flow.OnYardCompleted());
        }

        [Test]
        public void PhaseChanged_ReportsPreviousAndNext()
        {
            var flow = new UIFlow(GamePhase.Playing);
            GamePhase prev = GamePhase.Boot, next = GamePhase.Boot;
            flow.PhaseChanged += (p, n) => { prev = p; next = n; };
            flow.ToggleShop();
            Assert.AreEqual(GamePhase.Playing, prev);
            Assert.AreEqual(GamePhase.Shop, next);
        }

        [Test]
        public void SideEffectRules()
        {
            Assert.IsFalse(UIFlow.BlocksInput(GamePhase.Playing));
            Assert.IsTrue(UIFlow.BlocksInput(GamePhase.Shop));
            Assert.IsTrue(UIFlow.BlocksInput(GamePhase.Paused));
            Assert.IsTrue(UIFlow.FreezesTime(GamePhase.Paused));
            Assert.IsTrue(UIFlow.FreezesTime(GamePhase.TitleMenu));
            Assert.IsFalse(UIFlow.FreezesTime(GamePhase.Shop));
            Assert.IsFalse(UIFlow.FreezesTime(GamePhase.Playing));
            Assert.IsFalse(UIFlow.ShowsCursor(GamePhase.Playing));
            Assert.IsTrue(UIFlow.ShowsCursor(GamePhase.YardComplete));
        }
    }
}

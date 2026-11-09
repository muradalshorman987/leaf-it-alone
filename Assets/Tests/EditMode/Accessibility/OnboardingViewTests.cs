using FallenYard.Accessibility;
using FallenYard.Core;
using FallenYard.Settings;
using FallenYard.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace FallenYard.Tests.Accessibility
{
    public class OnboardingViewTests
    {
        float _now;
        OptionsScreen _options;
        GameSettings _lastEdit;
        int _edits;

        [SetUp]
        public void SetUp()
        {
            KeyBindings.ResetCurrent();
            _now = 10f;
            _edits = 0;
            _lastEdit = null;
            _options = new OptionsScreen { Clock = () => _now };
            _options.SettingsEdited += s =>
            {
                _edits++;
                _lastEdit = s;
            };
        }

        [TearDown]
        public void TearDown() => KeyBindings.ResetCurrent();

        [Test]
        public void Options_OpenShowsCurrentValues()
        {
            Assert.IsFalse(_options.IsOpen);
            _options.Open(new GameSettings { captions = true, invertY = true, colorBlindMode = ColorBlindMode.Protanopia }, tutorialActive: true);
            Assert.IsTrue(_options.IsOpen);
            Assert.IsTrue(_options.Body.Q<Toggle>("options-captions").value);
            Assert.IsTrue(_options.Body.Q<Toggle>("options-invert-y").value);
            Assert.IsFalse(_options.Body.Q<Toggle>("options-toggle-sprint").value);
            Assert.AreEqual("Protanopia (red-weak)", _options.Body.Q<Button>("options-palette").text);
            Assert.AreEqual("Skip tutorial", _options.TutorialButton.text);
            Assert.AreEqual("E", _options.KeyButton(GameAction.Interact).text);
            Assert.AreEqual(0, _edits, "opening doesn't apply anything");
        }

        [Test]
        public void Options_HasARowForEveryAction()
        {
            foreach (var a in KeyBindings.AllActions)
            {
                Assert.IsNotNull(_options.KeyButton(a), a.ToString());
                Assert.IsNotEmpty(OptionsScreen.ActionName(a));
            }
        }

        [Test]
        public void Rebind_ToAFreeKey_EmitsSettingsWithOverrides()
        {
            _options.Open(new GameSettings(), false);
            _options.BeginRebind(GameAction.Jump);
            Assert.AreEqual(GameAction.Jump, _options.ListeningAction);
            Assert.AreEqual("Press a key...", _options.KeyButton(GameAction.Jump).text);

            Assert.IsTrue(_options.CompleteRebind("C"));
            Assert.IsNull(_options.ListeningAction);
            Assert.AreEqual(1, _edits);
            Assert.AreEqual("C", KeyBindings.FromJson(_lastEdit.bindingOverridesJson).Get(GameAction.Jump));
            Assert.AreEqual("C", _options.KeyButton(GameAction.Jump).text);
        }

        [Test]
        public void Rebind_ToAKeyInUse_SwapsAndSaysSo()
        {
            _options.Open(new GameSettings(), false);
            _options.BeginRebind(GameAction.Interact);
            _options.CompleteRebind("Tab");
            StringAssert.Contains("Tool Shed moved to E", _options.StatusLabel.text);
            Assert.AreEqual("E", _options.KeyButton(GameAction.Shop).text);
        }

        [Test]
        public void Rebind_IgnoresTheClickRightAfterBinding()
        {
            _options.Open(new GameSettings(), false);
            _options.BeginRebind(GameAction.Jump);
            _options.CompleteRebind("Space"); // pressing Space also "clicks" the focused button
            _options.BeginRebind(GameAction.Jump);
            Assert.IsNull(_options.ListeningAction, "the same press must not start listening again");
            _now += 1f;
            _options.BeginRebind(GameAction.Jump);
            Assert.AreEqual(GameAction.Jump, _options.ListeningAction);
        }

        [Test]
        public void Back_CancelsListeningFirst_ThenCloses()
        {
            int closed = 0;
            _options.Closed += () => closed++;
            _options.Open(new GameSettings(), false);
            _options.BeginRebind(GameAction.Sprint);
            _options.HandleBack();
            Assert.IsTrue(_options.IsOpen);
            Assert.IsNull(_options.ListeningAction);
            _options.HandleBack();
            Assert.IsFalse(_options.IsOpen);
            Assert.AreEqual(1, closed);
            Assert.AreEqual(0, _edits);
        }

        [Test]
        public void CompleteRebind_WhenNotListening_DoesNothing()
        {
            _options.Open(new GameSettings(), false);
            Assert.IsFalse(_options.CompleteRebind("K"));
            Assert.AreEqual(0, _edits);
        }

        [Test]
        public void CaptionView_MirrorsQueueAndSettings()
        {
            var q = new CaptionQueue();
            var view = new CaptionView();
            q.Show("a", "[A]", 2f);
            view.Bind(q);
            Assert.AreEqual(1, view.Count);
            var line = q.Show("a", "[A again]", 2f);
            Assert.AreEqual("[A again]", view.LabelFor(line).text);
            q.Tick(3f);
            Assert.AreEqual(0, view.Count);

            view.ApplySettings(new GameSettings { captionSize = CaptionSize.Large, captionBackground = false });
            Assert.IsTrue(view.Root.ClassListContains("fy-captions--large"));
            Assert.IsFalse(view.Root.ClassListContains("fy-captions--boxed"));
            view.Unbind();
            q.Show("b", "[B]", 1f);
            Assert.AreEqual(0, view.Count);
        }

        [Test]
        public void TutorialCard_HidesWhenEmpty_AndStylesHints()
        {
            var card = new TutorialCardView();
            Assert.IsFalse(card.Visible);
            card.Set("Tutorial 1/6", "Walk around", false);
            Assert.IsTrue(card.Visible);
            Assert.IsTrue(UIViewUtil.IsVisible(card.FooterLabel));
            card.Set("Tip", "Bag piles", true);
            Assert.IsTrue(card.Root.ClassListContains("fy-tutorial--hint"));
            Assert.IsFalse(UIViewUtil.IsVisible(card.FooterLabel));
            card.Set("", "", false);
            Assert.IsFalse(card.Visible);
            Assert.AreEqual(PickingMode.Ignore, card.Root.pickingMode, "the card must not block clicks");
        }

        [Test]
        public void MenuScreen_InsertButton_KeepsOrder()
        {
            var menu = new MenuScreen("pause", "Paused");
            menu.AddButton("a", "Resume", () => { });
            menu.AddButton("c", "Quit", () => { });
            menu.InsertButton(1, "b", "Options", () => { });
            menu.InsertButton(99, "d", "Last", () => { });
            Assert.AreEqual("Options", menu.Buttons[1].text);
            Assert.AreEqual(menu.Buttons[1], menu.ButtonColumn[1]);
            Assert.AreEqual("Last", menu.Buttons[3].text);
        }

        class FakeModal : IUIModal
        {
            public bool IsOpen { get; set; }
            public int Backs;
            public void HandleBack() => Backs++;
        }

        [Test]
        public void UIManager_TopModal_IsTheLatestOpenOne()
        {
            var go = new GameObject("UI (test)");
            try
            {
                var ui = go.AddComponent<UIManager>();
                var a = new FakeModal { IsOpen = true };
                var b = new FakeModal { IsOpen = false };
                ui.RegisterModal(a);
                ui.RegisterModal(b);
                ui.RegisterModal(a);
                Assert.AreSame(a, ui.TopModal());
                b.IsOpen = true;
                Assert.AreSame(b, ui.TopModal());
                ui.UnregisterModal(b);
                a.IsOpen = false;
                Assert.IsNull(ui.TopModal());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Bootstrap_AddsTheComponentsOnce()
        {
            var go = new GameObject("UI (test)");
            try
            {
                var ui = go.AddComponent<UIManager>();
                Assert.IsTrue(OnboardingBootstrap.Install(ui));
                Assert.IsTrue(OnboardingBootstrap.Install(ui));
                Assert.AreEqual(1, go.GetComponents<OnboardingUI>().Length);
                Assert.AreEqual(1, go.GetComponents<FallenYard.Tutorial.TutorialDirector>().Length);
                Assert.AreEqual(1, go.GetComponents<PileHighlighter>().Length);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}

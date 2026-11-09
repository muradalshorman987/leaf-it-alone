using FallenYard.Core;
using FallenYard.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace FallenYard.Tests.UI
{
    public class HudViewTests
    {
        HudView _hud;

        [SetUp]
        public void SetUp()
        {
            GameEvents.ClearAllSubscribers();
            _hud = new HudView();
        }

        [TearDown]
        public void TearDown()
        {
            _hud.Unbind();
            GameEvents.ClearAllSubscribers();
        }

        [Test]
        public void Builds_NamedElements()
        {
            Assert.IsNotNull(_hud.Root.Q<Label>("hud-coins"));
            Assert.IsNotNull(_hud.Root.Q<Label>("hud-clean"));
            Assert.IsNotNull(_hud.Root.Q("hud-clean-fill"));
            Assert.IsNotNull(_hud.Root.Q("hud-crosshair"));
            Assert.AreEqual(PickingMode.Ignore, _hud.Root.pickingMode, "HUD must not swallow clicks");
        }

        [Test]
        public void Bound_FollowsCoinAndCleanEvents()
        {
            _hud.Bind();
            GameEvents.RaiseCoinsChanged(1500);
            GameEvents.RaiseYardCleanChanged(0.426f);

            Assert.AreEqual("1,500", _hud.CoinsLabel.text);
            Assert.AreEqual("42%", _hud.CleanLabel.text);
            Assert.AreEqual(42.6f, _hud.CleanFill.style.width.value.value, 0.01f);
            Assert.AreEqual(LengthUnit.Percent, _hud.CleanFill.style.width.value.unit);
        }

        [Test]
        public void Bound_ToolEquipped_UsesLookup()
        {
            _hud.Bind(id => id == "blower" ? "Leaf Blower" : null);
            GameEvents.RaiseToolEquipped("blower");
            Assert.AreEqual("Leaf Blower", _hud.ToolLabel.text);
            GameEvents.RaiseToolEquipped("rake");
            Assert.AreEqual("Rake", _hud.ToolLabel.text);
        }

        [Test]
        public void Unbind_StopsListening()
        {
            _hud.Bind();
            _hud.Unbind();
            GameEvents.RaiseCoinsChanged(99);
            Assert.AreEqual("0", _hud.CoinsLabel.text);
        }

        [Test]
        public void Bind_Twice_SubscribesOnce()
        {
            int before = 0;
            _hud.Bind();
            _hud.Bind();
            _hud.Unbind();
            GameEvents.CoinsChanged += v => before = v;
            GameEvents.RaiseCoinsChanged(7);
            Assert.AreEqual("0", _hud.CoinsLabel.text, "a second Bind must not leave a dangling subscription");
            Assert.AreEqual(7, before);
        }

        [Test]
        public void FillTurnsGreenAtGoal()
        {
            _hud.SetGoal(0.9f);
            _hud.SetClean(0.5f);
            Assert.IsFalse(_hud.CleanFill.ClassListContains("fy-bar__fill--done"));
            _hud.SetClean(0.92f);
            Assert.IsTrue(_hud.CleanFill.ClassListContains("fy-bar__fill--done"));
            Assert.AreEqual(90f, _hud.GoalMarker.style.left.value.value, 0.01f);
        }

        [Test]
        public void Prompt_HiddenWhenEmpty()
        {
            _hud.SetPrompt("[E] Bag pile (40 leaves)");
            Assert.IsTrue(UIViewUtil.IsVisible(_hud.PromptLabel));
            Assert.AreEqual("[E] Bag pile (40 leaves)", _hud.PromptLabel.text);
            _hud.SetPrompt("");
            Assert.IsFalse(UIViewUtil.IsVisible(_hud.PromptLabel));
        }
    }
}

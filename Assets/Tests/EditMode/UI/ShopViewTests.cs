using FallenYard.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace FallenYard.Tests.UI
{
    public class ShopViewTests
    {
        static ShopRow Row(string id, ShopAction action, bool canAfford) => new ShopRow
        {
            ToolId = id,
            Title = id,
            LevelText = "Lv 0 / 1",
            EffectText = "+10% reach",
            ButtonText = action.ToString(),
            Action = action,
            Cost = action == ShopAction.Maxed ? -1 : 10,
            CanAfford = canAfford,
        };

        [Test]
        public void StartsHidden()
        {
            var view = new ShopView();
            Assert.IsFalse(view.Visible);
            view.Show();
            Assert.IsTrue(view.Visible);
        }

        [Test]
        public void Refresh_BuildsOneRowPerTool_WithButtonState()
        {
            var view = new ShopView();
            view.Refresh(new[]
            {
                Row("rake", ShopAction.Upgrade, true),
                Row("blower", ShopAction.Unlock, false),
                Row("bag", ShopAction.Maxed, false),
            }, 1234);

            Assert.AreEqual("1,234", view.CoinsLabel.text);
            Assert.AreEqual(3, view.BuyButtons.Count);
            Assert.IsTrue(view.BuyButtons["rake"].enabledSelf);
            Assert.IsFalse(view.BuyButtons["blower"].enabledSelf);
            Assert.IsTrue(view.BuyButtons["blower"].ClassListContains("fy-button--short"));
            Assert.IsFalse(view.BuyButtons["bag"].enabledSelf);
            Assert.IsTrue(view.BuyButtons["bag"].ClassListContains("fy-button--maxed"));
            Assert.IsFalse(UIViewUtil.IsVisible(view.EmptyLabel));
            Assert.IsNotNull(view.Root.Q("shop-row-rake"));
        }

        [Test]
        public void Refresh_ReplacesOldRows()
        {
            var view = new ShopView();
            view.Refresh(new[] { Row("rake", ShopAction.Upgrade, true), Row("bag", ShopAction.Upgrade, true) }, 0);
            view.Refresh(new[] { Row("rake", ShopAction.Maxed, false) }, 0);
            Assert.AreEqual(1, view.BuyButtons.Count);
            Assert.IsNull(view.Root.Q("shop-row-bag"));
        }

        [Test]
        public void Refresh_Empty_ShowsEmptyLabel()
        {
            var view = new ShopView();
            view.Refresh(new ShopRow[0], 0);
            Assert.IsTrue(UIViewUtil.IsVisible(view.EmptyLabel));
        }
    }
}

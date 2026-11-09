using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace FallenYard.UI
{
    /// <summary>
    /// The upgrade shop ("Tool Shed"): coin total, one row per tool with level,
    /// next-level effect and a buy button. Purely a view: it raises
    /// <see cref="PurchaseRequested"/> and the owner calls <see cref="ShopModel.TryBuy"/>.
    /// </summary>
    public class ShopView
    {
        public VisualElement Root { get; }
        public Label CoinsLabel { get; }
        public ScrollView RowList { get; }
        public Label EmptyLabel { get; }
        public Button CloseButton { get; }

        /// <summary>Row id (tool id) to its buy button, rebuilt on every Refresh.</summary>
        public IReadOnlyDictionary<string, Button> BuyButtons => _buyButtons;
        readonly Dictionary<string, Button> _buyButtons = new Dictionary<string, Button>();

        public event Action<ShopRow> PurchaseRequested;
        public event Action CloseRequested;

        public ShopView()
        {
            Root = new VisualElement().With("shop", "fy-overlay");

            var card = new VisualElement().With("shop-card", "fy-panel", "fy-shop");
            Root.Add(card);

            var header = new VisualElement().With("shop-header", "fy-shop__header");
            header.Add(UIViewUtil.MakeLabel("shop-title", "Tool Shed", "fy-menu__title"));
            var wallet = new VisualElement().With("shop-wallet", "fy-hud__row");
            wallet.Add(new VisualElement().With("shop-coin-icon", "fy-coin-icon"));
            CoinsLabel = UIViewUtil.MakeLabel("shop-coins", "0", "fy-hud__coins");
            wallet.Add(CoinsLabel);
            header.Add(wallet);
            card.Add(header);

            RowList = new ScrollView(ScrollViewMode.Vertical).With("shop-list", "fy-shop__list");
            card.Add(RowList);

            EmptyLabel = UIViewUtil.MakeLabel("shop-empty", "Nothing for sale yet.", "fy-menu__subtitle");
            card.Add(EmptyLabel);

            CloseButton = new Button(() => CloseRequested?.Invoke()) { text = "Back to the yard  (Tab)" }
                .With("shop-close", "fy-button");
            card.Add(CloseButton);

            Hide();
        }

        public void Refresh(IReadOnlyList<ShopRow> rows, int coins)
        {
            CoinsLabel.text = HudFormat.Coins(coins);
            RowList.Clear();
            _buyButtons.Clear();

            int count = rows != null ? rows.Count : 0;
            UIViewUtil.SetVisible(EmptyLabel, count == 0);
            for (int i = 0; i < count; i++) RowList.Add(BuildRow(rows[i]));
        }

        VisualElement BuildRow(ShopRow row)
        {
            var element = new VisualElement().With("shop-row-" + row.ToolId, "fy-shop__row");

            var info = new VisualElement().With(null, "fy-shop__info");
            var titleLine = new VisualElement().With(null, "fy-hud__row");
            titleLine.Add(UIViewUtil.MakeLabel(null, row.Title, "fy-shop__name"));
            titleLine.Add(UIViewUtil.MakeLabel(null, row.LevelText, "fy-shop__level"));
            info.Add(titleLine);
            if (!string.IsNullOrEmpty(row.EffectText))
                info.Add(UIViewUtil.MakeLabel(null, row.EffectText, "fy-shop__effect"));
            element.Add(info);

            var captured = row;
            var buy = new Button(() => PurchaseRequested?.Invoke(captured)) { text = row.ButtonText }
                .With("shop-buy-" + row.ToolId, "fy-button", "fy-shop__buy");
            if (row.Action == ShopAction.Maxed) buy.AddToClassList("fy-button--maxed");
            else if (!row.CanAfford) buy.AddToClassList("fy-button--short");
            buy.SetEnabled(row.Interactable);
            element.Add(buy);

            _buyButtons[row.ToolId] = buy;
            return element;
        }

        public bool Visible => UIViewUtil.IsVisible(Root);

        public void Show()
        {
            UIViewUtil.SetVisible(Root, true);
            CloseButton.Focus();
        }

        public void Hide() => UIViewUtil.SetVisible(Root, false);
    }
}

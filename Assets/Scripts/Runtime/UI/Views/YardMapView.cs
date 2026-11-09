using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace FallenYard.UI
{
    /// <summary>
    /// The Hollow Creek map: one row per yard with its neighbour, status and a Go / Open / Locked
    /// button. Purely a view; the owner turns <see cref="RowChosen"/> into travel or a purchase.
    /// </summary>
    public class YardMapView
    {
        public VisualElement Root { get; }
        public Label CoinsLabel { get; }
        public ScrollView RowList { get; }
        public Button CloseButton { get; }

        /// <summary>Yard id to its button, rebuilt on every Refresh.</summary>
        public IReadOnlyDictionary<string, Button> RowButtons => _buttons;
        readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();

        public event Action<YardMapRow> RowChosen;
        public event Action CloseRequested;

        public YardMapView()
        {
            Root = new VisualElement().With("yard-map", "fy-overlay");

            var card = new VisualElement().With("yard-map-card", "fy-panel", "fy-shop");
            Root.Add(card);

            var header = new VisualElement().With("yard-map-header", "fy-shop__header");
            header.Add(UIViewUtil.MakeLabel("yard-map-title", "Hollow Creek", "fy-menu__title"));
            var wallet = new VisualElement().With("yard-map-wallet", "fy-hud__row");
            wallet.Add(new VisualElement().With("yard-map-coin-icon", "fy-coin-icon"));
            CoinsLabel = UIViewUtil.MakeLabel("yard-map-coins", "0", "fy-hud__coins");
            wallet.Add(CoinsLabel);
            header.Add(wallet);
            card.Add(header);

            RowList = new ScrollView(ScrollViewMode.Vertical).With("yard-map-list", "fy-shop__list");
            card.Add(RowList);

            CloseButton = new Button(() => CloseRequested?.Invoke()) { text = "Back" }.With("yard-map-close", "fy-button");
            card.Add(CloseButton);

            Hide();
        }

        public void Refresh(IReadOnlyList<YardMapRow> rows, int coins)
        {
            CoinsLabel.text = HudFormat.Coins(coins);
            RowList.Clear();
            _buttons.Clear();
            if (rows == null) return;
            foreach (var row in rows) RowList.Add(BuildRow(row));
        }

        VisualElement BuildRow(YardMapRow row)
        {
            var element = new VisualElement().With("yard-map-row-" + row.YardId, "fy-shop__row");

            var info = new VisualElement().With(null, "fy-shop__info");
            var titleLine = new VisualElement().With(null, "fy-hud__row");
            titleLine.Add(UIViewUtil.MakeLabel(null, row.Title, "fy-shop__name"));
            titleLine.Add(UIViewUtil.MakeLabel(null, row.StatusText, "fy-shop__level"));
            info.Add(titleLine);
            if (!string.IsNullOrEmpty(row.Neighbour))
                info.Add(UIViewUtil.MakeLabel(null, row.Neighbour, "fy-shop__effect"));
            element.Add(info);

            var captured = row;
            var button = new Button(() => RowChosen?.Invoke(captured)) { text = row.ButtonText }
                .With("yard-map-go-" + row.YardId, "fy-button", "fy-shop__buy");
            if (row.Action == YardMapAction.Play) button.AddToClassList("fy-button--primary");
            if (row.Action == YardMapAction.Locked || row.Action == YardMapAction.Current) button.AddToClassList("fy-button--maxed");
            else if (!row.CanAfford) button.AddToClassList("fy-button--short");
            button.SetEnabled(row.Interactable);
            element.Add(button);

            _buttons[row.YardId] = button;
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

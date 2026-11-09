using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace FallenYard.UI
{
    /// <summary>
    /// A full-screen overlay with a title, optional subtitle and a column of buttons.
    /// Used for the title menu, pause menu and yard-complete card.
    /// </summary>
    public class MenuScreen
    {
        public VisualElement Root { get; }
        public VisualElement Card { get; }
        public Label TitleLabel { get; }
        public Label SubtitleLabel { get; }
        public VisualElement ButtonColumn { get; }

        readonly List<Button> _buttons = new List<Button>();
        public IReadOnlyList<Button> Buttons => _buttons;

        public MenuScreen(string name, string title, string subtitle = null, string extraClass = null)
        {
            Root = new VisualElement().With(name, "fy-overlay");
            if (!string.IsNullOrEmpty(extraClass)) Root.AddToClassList(extraClass);

            Card = new VisualElement().With(name + "-card", "fy-panel", "fy-menu");
            Root.Add(Card);

            TitleLabel = UIViewUtil.MakeLabel(name + "-title", title, "fy-menu__title");
            Card.Add(TitleLabel);

            SubtitleLabel = UIViewUtil.MakeLabel(name + "-subtitle", subtitle ?? "", "fy-menu__subtitle");
            UIViewUtil.SetVisible(SubtitleLabel, !string.IsNullOrEmpty(subtitle));
            Card.Add(SubtitleLabel);

            ButtonColumn = new VisualElement().With(name + "-buttons", "fy-menu__buttons");
            Card.Add(ButtonColumn);

            Hide();
        }

        public Button AddButton(string id, string text, Action onClick, bool primary = false)
        {
            var button = new Button(onClick) { text = text }.With(id, "fy-button");
            if (primary) button.AddToClassList("fy-button--primary");
            ButtonColumn.Add(button);
            _buttons.Add(button);
            return button;
        }

        /// <summary>Like <see cref="AddButton"/> but at a position in the column (clamped).</summary>
        public Button InsertButton(int index, string id, string text, Action onClick, bool primary = false)
        {
            var button = new Button(onClick) { text = text }.With(id, "fy-button");
            if (primary) button.AddToClassList("fy-button--primary");
            index = Math.Max(0, Math.Min(index, _buttons.Count));
            ButtonColumn.Insert(index, button);
            _buttons.Insert(index, button);
            return button;
        }

        public void SetSubtitle(string text)
        {
            SubtitleLabel.text = text ?? "";
            UIViewUtil.SetVisible(SubtitleLabel, !string.IsNullOrEmpty(text));
        }

        public bool Visible => UIViewUtil.IsVisible(Root);

        public void Show()
        {
            UIViewUtil.SetVisible(Root, true);
            if (_buttons.Count > 0) _buttons[0].Focus();
        }

        public void Hide() => UIViewUtil.SetVisible(Root, false);
    }
}

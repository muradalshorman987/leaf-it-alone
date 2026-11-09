using UnityEngine.UIElements;

namespace FallenYard.UI
{
    /// <summary>The tutorial / tip card near the top of the screen.</summary>
    public class TutorialCardView
    {
        public const string FooterText = "Esc > Options to turn off or skip the tutorial";

        public VisualElement Root { get; }
        public Label TitleLabel { get; }
        public Label TextLabel { get; }
        public Label FooterLabel { get; }

        public TutorialCardView()
        {
            Root = new VisualElement().With("tutorial-card", "fy-panel", "fy-tutorial");
            Root.pickingMode = PickingMode.Ignore;

            TitleLabel = UIViewUtil.MakeLabel("tutorial-title", "", "fy-tutorial__title");
            TextLabel = UIViewUtil.MakeLabel("tutorial-text", "", "fy-tutorial__text");
            FooterLabel = UIViewUtil.MakeLabel("tutorial-footer", FooterText, "fy-tutorial__footer");
            Root.Add(TitleLabel);
            Root.Add(TextLabel);
            Root.Add(FooterLabel);
            foreach (var child in Root.Query<VisualElement>().ToList()) child.pickingMode = PickingMode.Ignore;

            Set("", "", false);
        }

        public bool Visible => UIViewUtil.IsVisible(Root);

        /// <summary>Shows the card, or hides it when <paramref name="text"/> is empty.</summary>
        public void Set(string title, string text, bool isHint)
        {
            TitleLabel.text = title ?? "";
            TextLabel.text = text ?? "";
            Root.EnableInClassList("fy-tutorial--hint", isHint);
            UIViewUtil.SetVisible(TitleLabel, !string.IsNullOrEmpty(title));
            UIViewUtil.SetVisible(FooterLabel, !isHint);
            UIViewUtil.SetVisible(Root, !string.IsNullOrEmpty(text));
        }
    }
}

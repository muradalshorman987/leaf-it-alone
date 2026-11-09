using System;
using FallenYard.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace FallenYard.UI
{
    /// <summary>
    /// In-game HUD: coins, yard clean % with a goal marker, current tool, crosshair,
    /// interaction prompt and key hints. Built in code so it needs no UXML wiring.
    /// Call <see cref="Bind"/> to follow <see cref="GameEvents"/>.
    /// </summary>
    public class HudView
    {
        public VisualElement Root { get; }
        public Label CoinsLabel { get; }
        public Label CleanLabel { get; }
        public VisualElement CleanFill { get; }
        public VisualElement GoalMarker { get; }
        public Label ToolLabel { get; }
        public Label PromptLabel { get; }
        public Label HintLabel { get; }

        public int Coins { get; private set; }
        public float CleanFraction { get; private set; }

        Func<string, string> _toolNameLookup;
        bool _bound;

        public const string HintText = "1-4 tools   E bag   Tab shop   Esc pause";

        public HudView()
        {
            Root = new VisualElement().With("hud", "fy-hud");
            Root.pickingMode = PickingMode.Ignore;

            var stats = new VisualElement().With("hud-stats", "fy-panel", "fy-hud__stats");
            Root.Add(stats);

            var coinRow = new VisualElement().With("hud-coins-row", "fy-hud__row");
            coinRow.Add(new VisualElement().With("hud-coin-icon", "fy-coin-icon"));
            CoinsLabel = UIViewUtil.MakeLabel("hud-coins", "0", "fy-hud__coins");
            coinRow.Add(CoinsLabel);
            stats.Add(coinRow);

            var cleanRow = new VisualElement().With("hud-clean-row", "fy-hud__row");
            cleanRow.Add(UIViewUtil.MakeLabel("hud-clean-caption", "Yard", "fy-hud__caption"));
            var track = new VisualElement().With("hud-clean-track", "fy-bar");
            CleanFill = new VisualElement().With("hud-clean-fill", "fy-bar__fill");
            GoalMarker = new VisualElement().With("hud-clean-goal", "fy-bar__goal");
            track.Add(CleanFill);
            track.Add(GoalMarker);
            cleanRow.Add(track);
            CleanLabel = UIViewUtil.MakeLabel("hud-clean", "0%", "fy-hud__clean");
            cleanRow.Add(CleanLabel);
            stats.Add(cleanRow);

            ToolLabel = UIViewUtil.MakeLabel("hud-tool", "", "fy-hud__tool");
            stats.Add(ToolLabel);

            var crosshair = new VisualElement().With("hud-crosshair", "fy-crosshair");
            crosshair.pickingMode = PickingMode.Ignore;
            Root.Add(crosshair);

            PromptLabel = UIViewUtil.MakeLabel("hud-prompt", "", "fy-hud__prompt");
            Root.Add(PromptLabel);

            HintLabel = UIViewUtil.MakeLabel("hud-hint", HintText, "fy-hud__hint");
            Root.Add(HintLabel);

            foreach (var child in Root.Query<VisualElement>().ToList()) child.pickingMode = PickingMode.Ignore;

            SetCoins(0);
            SetClean(0f);
            SetGoal(0.95f);
            SetPrompt("");
        }

        /// <summary>Subscribes to GameEvents. <paramref name="toolNameLookup"/> maps tool id to display name.</summary>
        public void Bind(Func<string, string> toolNameLookup = null)
        {
            _toolNameLookup = toolNameLookup;
            if (_bound) return;
            _bound = true;
            GameEvents.CoinsChanged += SetCoins;
            GameEvents.YardCleanChanged += SetClean;
            GameEvents.ToolEquipped += OnToolEquipped;
            KeyBindings.Changed += RefreshKeyHints;
            RefreshKeyHints();
        }

        public void Unbind()
        {
            if (!_bound) return;
            _bound = false;
            GameEvents.CoinsChanged -= SetCoins;
            GameEvents.YardCleanChanged -= SetClean;
            GameEvents.ToolEquipped -= OnToolEquipped;
            KeyBindings.Changed -= RefreshKeyHints;
        }

        /// <summary>Rewrites the key reminder line after the player rebinds keys.</summary>
        public void RefreshKeyHints() => HintLabel.text = HudFormat.KeyHints(KeyBindings.Current);

        void OnToolEquipped(string toolId) => SetTool(HudFormat.ToolName(toolId, _toolNameLookup));

        public void SetCoins(int coins)
        {
            Coins = Mathf.Max(0, coins);
            CoinsLabel.text = HudFormat.Coins(Coins);
        }

        public void SetClean(float fraction)
        {
            CleanFraction = Mathf.Clamp01(float.IsNaN(fraction) ? 0f : fraction);
            CleanLabel.text = HudFormat.CleanPercent(CleanFraction);
            CleanFill.style.width = Length.Percent(CleanFraction * 100f);
            CleanFill.EnableInClassList("fy-bar__fill--done", CleanFraction >= _goal);
        }

        float _goal = 0.95f;

        /// <summary>Where the "yard done" marker sits on the clean bar (YardProgress.completeAtFraction).</summary>
        public void SetGoal(float fraction)
        {
            _goal = Mathf.Clamp01(fraction);
            GoalMarker.style.left = Length.Percent(_goal * 100f);
            CleanFill.EnableInClassList("fy-bar__fill--done", CleanFraction >= _goal);
        }

        public void SetTool(string displayName)
        {
            ToolLabel.text = displayName ?? "";
            UIViewUtil.SetVisible(ToolLabel, !string.IsNullOrEmpty(displayName));
        }

        public void SetPrompt(string prompt)
        {
            if (PromptLabel.text == (prompt ?? "") && UIViewUtil.IsVisible(PromptLabel) == !string.IsNullOrEmpty(prompt)) return;
            PromptLabel.text = prompt ?? "";
            UIViewUtil.SetVisible(PromptLabel, !string.IsNullOrEmpty(prompt));
        }
    }
}

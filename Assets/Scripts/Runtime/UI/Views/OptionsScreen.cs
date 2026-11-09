using System;
using System.Collections.Generic;
using System.Globalization;
using FallenYard.Accessibility;
using FallenYard.Core;
using FallenYard.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace FallenYard.UI
{
    /// <summary>
    /// Options overlay: controls (sensitivity, invert Y, hold-to-toggle), accessibility (captions,
    /// colour-blind palette, pile highlights, head bob, tutorial) and key rebinding. Edits apply
    /// immediately: every change raises <see cref="SettingsEdited"/> with a fresh copy, which the
    /// owner passes to <c>SettingsManager.Apply</c>. Built in code, like the other views.
    /// </summary>
    public class OptionsScreen : IUIModal
    {
        public VisualElement Root { get; }
        public VisualElement Card { get; }
        public ScrollView Body { get; }
        public Label StatusLabel { get; }
        public Button BackButton { get; }
        public Button TutorialButton { get; }

        /// <summary>A copy of the edited settings; apply it.</summary>
        public event Action<GameSettings> SettingsEdited;
        public event Action ReplayTutorialRequested;
        public event Action SkipTutorialRequested;
        public event Action Closed;

        /// <summary>Clock used to ignore the click that lands right after a rebind (seconds, unscaled).</summary>
        public Func<float> Clock = () => Time.unscaledTime;

        public bool IsOpen => UIViewUtil.IsVisible(Root);

        /// <summary>The action waiting for a key press, or null.</summary>
        public GameAction? ListeningAction { get; private set; }

        public GameSettings Working => _working;
        public KeyBindings WorkingKeys => _keys;

        readonly Dictionary<GameAction, Button> _keyButtons = new Dictionary<GameAction, Button>();
        readonly List<Action> _refreshers = new List<Action>();
        GameSettings _working = new GameSettings();
        KeyBindings _keys = new KeyBindings();
        bool _tutorialActive;
        float _ignoreRebindClicksUntil = -1f;

        public OptionsScreen()
        {
            Root = new VisualElement().With("options", "fy-overlay", "fy-options-overlay");
            Card = new VisualElement().With("options-card", "fy-panel", "fy-options");
            Root.Add(Card);

            var header = new VisualElement().With("options-header", "fy-options__header");
            header.Add(UIViewUtil.MakeLabel("options-title", "Options", "fy-menu__title"));
            Card.Add(header);

            Body = new ScrollView(ScrollViewMode.Vertical).With("options-body", "fy-options__body");
            Card.Add(Body);

            BuildControls();
            BuildAccessibility();
            BuildKeys();

            StatusLabel = UIViewUtil.MakeLabel("options-status", "", "fy-options__status");
            Card.Add(StatusLabel);

            var footer = new VisualElement().With("options-footer", "fy-options__footer");
            footer.Add(new Button(ResetAll) { text = "Reset to defaults" }.With("options-reset", "fy-button"));
            BackButton = new Button(Close) { text = "Back  (Esc)" }.With("options-back", "fy-button", "fy-button--primary");
            footer.Add(BackButton);
            Card.Add(footer);

            TutorialButton = Body.Q<Button>("options-tutorial");
            UIViewUtil.SetVisible(Root, false);
        }

        // ------------------------------------------------------------ open / close

        public void Open(GameSettings current, bool tutorialActive)
        {
            _working = (current ?? new GameSettings()).Clone();
            _keys = KeyBindings.FromJson(_working.bindingOverridesJson);
            _tutorialActive = tutorialActive;
            ListeningAction = null;
            StatusLabel.text = "";
            Refresh();
            UIViewUtil.SetVisible(Root, true);
            BackButton.Focus();
        }

        public void Close()
        {
            if (!IsOpen) return;
            ListeningAction = null;
            UIViewUtil.SetVisible(Root, false);
            Closed?.Invoke();
        }

        /// <summary>Escape: stop listening for a key if we are, otherwise close.</summary>
        public void HandleBack()
        {
            if (ListeningAction.HasValue) CancelRebind();
            else Close();
        }

        /// <summary>Updates the tutorial button after the tutorial was skipped/finished elsewhere.</summary>
        public void SetTutorialActive(bool active)
        {
            _tutorialActive = active;
            Refresh();
        }

        // ------------------------------------------------------------ rebinding

        public void BeginRebind(GameAction action)
        {
            if (Clock() < _ignoreRebindClicksUntil) return;
            ListeningAction = action;
            StatusLabel.text = $"Press a key or mouse button for \"{ActionName(action)}\" (Esc to cancel)";
            Refresh();
        }

        /// <summary>Binds the listening action to <paramref name="control"/>. Returns false if nothing was listening.</summary>
        public bool CompleteRebind(string control)
        {
            if (!ListeningAction.HasValue) return false;
            var action = ListeningAction.Value;
            ListeningAction = null;
            _ignoreRebindClicksUntil = Clock() + 0.25f;

            var swapped = _keys.Set(action, control, out bool changed);
            if (!changed)
            {
                StatusLabel.text = KeyBindings.IsValidControl(control) ? "" : "That key can't be used.";
                Refresh();
                return true;
            }

            StatusLabel.text = swapped.HasValue
                ? $"{ActionName(action)} is now {KeyBindings.DisplayName(control)}. {ActionName(swapped.Value)} moved to {_keys.Label(swapped.Value)}."
                : $"{ActionName(action)} is now {KeyBindings.DisplayName(control)}.";
            _working.bindingOverridesJson = _keys.ToJson();
            Emit();
            return true;
        }

        public void CancelRebind()
        {
            ListeningAction = null;
            StatusLabel.text = "";
            Refresh();
        }

        public Button KeyButton(GameAction action) => _keyButtons.TryGetValue(action, out var b) ? b : null;

        public static string ActionName(GameAction action) => action switch
        {
            GameAction.MoveForward => "Walk forward",
            GameAction.MoveBack => "Walk back",
            GameAction.MoveLeft => "Step left",
            GameAction.MoveRight => "Step right",
            GameAction.Sprint => "Sprint",
            GameAction.Jump => "Jump",
            GameAction.UseTool => "Use tool",
            GameAction.Interact => "Bag pile / interact",
            GameAction.Shop => "Tool Shed",
            GameAction.Tool1 => "Tool slot 1",
            GameAction.Tool2 => "Tool slot 2",
            GameAction.Tool3 => "Tool slot 3",
            GameAction.Tool4 => "Tool slot 4",
            _ => action.ToString(),
        };

        // ------------------------------------------------------------ sections

        void BuildControls()
        {
            Section("options-controls", "Controls");
            AddSlider("options-sensitivity", "Mouse sensitivity", GameSettings.MinSensitivity, GameSettings.MaxSensitivity,
                s => s.mouseSensitivity, (s, v) => s.mouseSensitivity = Mathf.Round(v * 10f) / 10f);
            AddToggle("options-invert-y", "Invert mouse Y", s => s.invertY, (s, v) => s.invertY = v);
            AddToggle("options-toggle-sprint", "Sprint: press to toggle (instead of hold)", s => s.toggleSprint, (s, v) => s.toggleSprint = v);
            AddToggle("options-toggle-tool", "Use tool: press to toggle (instead of hold)", s => s.toggleToolUse, (s, v) => s.toggleToolUse = v);
        }

        void BuildAccessibility()
        {
            Section("options-accessibility", "Accessibility");
            AddToggle("options-captions", "Sound captions", s => s.captions, (s, v) => s.captions = v);
            AddCycle("options-caption-size", "Caption size", s => (int)s.captionSize, (s, v) => s.captionSize = (CaptionSize)v,
                Enum.GetValues(typeof(CaptionSize)).Length, v => ((CaptionSize)v).ToString());
            AddToggle("options-caption-box", "Caption background", s => s.captionBackground, (s, v) => s.captionBackground = v);
            AddCycle("options-palette", "Colour-blind palette", s => (int)s.colorBlindMode, (s, v) => s.colorBlindMode = (ColorBlindMode)v,
                Enum.GetValues(typeof(ColorBlindMode)).Length, v => AccessibilityPalette.DisplayName((ColorBlindMode)v));
            AddPaletteSwatches();
            AddToggle("options-pile-rings", "Highlight leaf piles", s => s.pileHighlights, (s, v) => s.pileHighlights = v);
            AddToggle("options-head-bob", "Camera head bob", s => s.headBob, (s, v) => s.headBob = v);
            AddToggle("options-hints", "Tutorial and tips", s => s.tutorialHints, (s, v) => s.tutorialHints = v);

            var tutorial = new Button(() =>
            {
                if (_tutorialActive) SkipTutorialRequested?.Invoke();
                else ReplayTutorialRequested?.Invoke();
                _tutorialActive = !_tutorialActive;
                Refresh();
            }).With("options-tutorial", "fy-button", "fy-options__wide");
            Body.Add(tutorial);
            _refreshers.Add(() => tutorial.text = _tutorialActive ? "Skip tutorial" : "Replay tutorial");
        }

        void BuildKeys()
        {
            Section("options-keys", "Keys");
            foreach (var action in KeyBindings.AllActions)
            {
                var row = Row("options-key-" + action);
                row.Add(UIViewUtil.MakeLabel(null, ActionName(action), "fy-options__label"));
                var a = action;
                var button = new Button(() => BeginRebind(a)).With("options-key-button-" + action, "fy-button", "fy-options__key");
                row.Add(button);
                _keyButtons[action] = button;
            }
            _refreshers.Add(() =>
            {
                foreach (var pair in _keyButtons)
                {
                    bool listening = ListeningAction == pair.Key;
                    pair.Value.text = listening ? "Press a key..." : _keys.Label(pair.Key);
                    pair.Value.EnableInClassList("fy-options__key--listening", listening);
                }
            });

            var reset = new Button(() =>
            {
                _keys.ResetToDefaults();
                _working.bindingOverridesJson = "";
                StatusLabel.text = "Keys reset to defaults.";
                Emit();
            }) { text = "Reset keys" }.With("options-reset-keys", "fy-button", "fy-options__wide");
            Body.Add(reset);
            Body.Add(UIViewUtil.MakeLabel("options-keys-note", "Esc always pauses and can't be rebound.", "fy-options__note"));
        }

        void AddPaletteSwatches()
        {
            var row = Row("options-palette-preview");
            row.Add(UIViewUtil.MakeLabel(null, "Pile / ready / progress / done", "fy-options__note"));
            var swatches = new VisualElement().With("options-swatches", "fy-options__swatches");
            var roles = new[] { PaletteRole.Pile, PaletteRole.PileReady, PaletteRole.Progress, PaletteRole.ProgressDone };
            var boxes = new List<VisualElement>();
            foreach (var role in roles)
            {
                var box = new VisualElement().With("options-swatch-" + role, "fy-options__swatch");
                swatches.Add(box);
                boxes.Add(box);
            }
            row.Add(swatches);
            _refreshers.Add(() =>
            {
                for (int i = 0; i < roles.Length; i++)
                    boxes[i].style.backgroundColor = AccessibilityPalette.Get(_working.colorBlindMode, roles[i]);
            });
        }

        // ------------------------------------------------------------ row builders

        void Section(string id, string title) => Body.Add(UIViewUtil.MakeLabel(id, title, "fy-options__section"));

        VisualElement Row(string id)
        {
            var row = new VisualElement().With(id, "fy-options__row");
            Body.Add(row);
            return row;
        }

        void AddToggle(string id, string label, Func<GameSettings, bool> get, Action<GameSettings, bool> set)
        {
            var toggle = new Toggle(label).With(id, "fy-options__toggle");
            toggle.RegisterValueChangedCallback(evt =>
            {
                set(_working, evt.newValue);
                Emit();
            });
            Body.Add(toggle);
            _refreshers.Add(() => toggle.SetValueWithoutNotify(get(_working)));
        }

        void AddSlider(string id, string label, float min, float max, Func<GameSettings, float> get, Action<GameSettings, float> set)
        {
            var row = Row(id + "-row");
            var slider = new Slider(label, min, max).With(id, "fy-options__slider");
            var value = UIViewUtil.MakeLabel(id + "-value", "", "fy-options__value");
            slider.RegisterValueChangedCallback(evt =>
            {
                set(_working, evt.newValue);
                value.text = get(_working).ToString("0.0", CultureInfo.InvariantCulture);
                Emit(refresh: false);
            });
            row.Add(slider);
            row.Add(value);
            _refreshers.Add(() =>
            {
                slider.SetValueWithoutNotify(get(_working));
                value.text = get(_working).ToString("0.0", CultureInfo.InvariantCulture);
            });
        }

        void AddCycle(string id, string label, Func<GameSettings, int> get, Action<GameSettings, int> set, int count, Func<int, string> name)
        {
            var row = Row(id + "-row");
            row.Add(UIViewUtil.MakeLabel(null, label, "fy-options__label"));
            var button = new Button().With(id, "fy-button", "fy-options__cycle");
            button.clicked += () =>
            {
                set(_working, (get(_working) + 1) % Math.Max(1, count));
                Emit();
            };
            row.Add(button);
            _refreshers.Add(() => button.text = name(get(_working)));
        }

        // ------------------------------------------------------------ internals

        void ResetAll()
        {
            var fresh = new GameSettings
            {
                // Graphics and audio live elsewhere; this screen only resets what it shows.
                masterVolume = _working.masterVolume,
                musicVolume = _working.musicVolume,
                sfxVolume = _working.sfxVolume,
                ambienceVolume = _working.ambienceVolume,
                muteWhenUnfocused = _working.muteWhenUnfocused,
                fieldOfView = _working.fieldOfView,
                qualityLevel = _working.qualityLevel,
                fullScreenMode = _working.fullScreenMode,
                resolutionWidth = _working.resolutionWidth,
                resolutionHeight = _working.resolutionHeight,
                vSync = _working.vSync,
                targetFrameRate = _working.targetFrameRate,
            };
            _working = fresh;
            _keys = new KeyBindings();
            ListeningAction = null;
            StatusLabel.text = "Options reset to defaults.";
            Emit();
        }

        void Emit(bool refresh = true)
        {
            if (refresh) Refresh();
            SettingsEdited?.Invoke(_working.Clone());
        }

        public void Refresh()
        {
            foreach (var r in _refreshers) r();
        }
    }
}

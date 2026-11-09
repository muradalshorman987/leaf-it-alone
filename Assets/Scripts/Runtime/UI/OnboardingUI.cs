using FallenYard.Accessibility;
using FallenYard.Core;
using FallenYard.Settings;
using FallenYard.Tutorial;
using UnityEngine;
using UnityEngine.UIElements;

namespace FallenYard.UI
{
    /// <summary>
    /// WP11's layer on top of <see cref="UIManager"/>: the options screen (with key rebinding),
    /// sound captions, the tutorial / tip card and the colour-blind palette on the UI root. It only
    /// adds views and menu buttons once the UIManager has built its screens, so the regular UI
    /// works with or without it. Zero setup: added by <see cref="OnboardingBootstrap"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)] // after UIManager (-100)
    public class OnboardingUI : MonoBehaviour
    {
        public OptionsScreen Options { get; private set; }
        public CaptionView Captions { get; private set; }
        public CaptionDirector CaptionDirector { get; private set; }
        public TutorialCardView TutorialCard { get; private set; }

        UIManager _ui;
        TutorialDirector _tutorial;
        bool _attached;
        bool _skipInputFrame;
        GameSettings _fallbackSettings = new GameSettings();

        GameSettings CurrentSettings =>
            SettingsManager.Instance != null && SettingsManager.Instance.Current != null ? SettingsManager.Instance.Current : _fallbackSettings;

        void Awake()
        {
            CaptionDirector = new CaptionDirector { ListenerForward = ListenerForward };
        }

        void OnEnable()
        {
            CaptionDirector.Bind();
            SettingsManager.Changed += ApplySettings;
            GameEvents.StateChanged += OnPhaseChanged;
            TryAttach();
        }

        void OnDisable()
        {
            CaptionDirector.Unbind();
            SettingsManager.Changed -= ApplySettings;
            GameEvents.StateChanged -= OnPhaseChanged;
            if (_ui != null)
            {
                _ui.Built -= Attach;
                if (Options != null) _ui.UnregisterModal(Options);
            }
            if (_tutorial != null) _tutorial.CardChanged -= RefreshCard;
        }

        void Start() => TryAttach();

        void TryAttach()
        {
            if (_attached) return;
            if (_ui == null) _ui = GetComponent<UIManager>();
            if (_ui == null) _ui = UIManager.Instance;
            if (_ui == null) return;
            if (_ui.IsBuilt) Attach();
            else
            {
                _ui.Built -= Attach;
                _ui.Built += Attach;
            }
        }

        /// <summary>Adds the views to the UIManager's root. Public for tests.</summary>
        public void Attach()
        {
            if (_attached || _ui == null || _ui.Root == null) return;
            _attached = true;
            _ui.Built -= Attach;
            var root = _ui.Root;

            TutorialCard = new TutorialCardView();
            root.Add(TutorialCard.Root);

            Captions = new CaptionView();
            Captions.Bind(CaptionDirector.Queue);
            root.Add(Captions.Root);

            Options = new OptionsScreen();
            Options.SettingsEdited += OnSettingsEdited;
            Options.ReplayTutorialRequested += () => _tutorial?.ReplayTutorial();
            Options.SkipTutorialRequested += () => _tutorial?.SkipTutorial();
            Options.Closed += OnOptionsClosed;
            root.Add(Options.Root);
            _ui.RegisterModal(Options);

            // Before "Save and quit" / "Quit" so those stay last.
            if (_ui.PauseMenu != null) _ui.PauseMenu.InsertButton(IndexOf(_ui.PauseMenu, "pause-title"), "pause-options", "Options", OpenOptions);
            if (_ui.TitleMenu != null) _ui.TitleMenu.InsertButton(IndexOf(_ui.TitleMenu, "title-quit"), "title-options", "Options", OpenOptions);

            _tutorial = GetComponent<TutorialDirector>();
            if (_tutorial == null) _tutorial = TutorialDirector.Instance;
            if (_tutorial != null) _tutorial.CardChanged += RefreshCard;

            ApplySettings(CurrentSettings);
            RefreshCard();
        }

        /// <summary>Position of the named button, or the end of the column.</summary>
        public static int IndexOf(MenuScreen menu, string buttonName)
        {
            for (int i = 0; i < menu.Buttons.Count; i++)
                if (menu.Buttons[i].name == buttonName) return i;
            return menu.Buttons.Count;
        }

        public void OpenOptions()
        {
            if (Options == null) return;
            Options.Open(CurrentSettings, _tutorial != null && _tutorial.Model.IsActive);
            _skipInputFrame = true;
        }

        void OnOptionsClosed()
        {
            // Back to the menu we came from: focus its first button for keyboard players.
            if (_ui == null) return;
            if (_ui.PauseMenu != null && _ui.PauseMenu.Visible && _ui.PauseMenu.Buttons.Count > 0) _ui.PauseMenu.Buttons[0].Focus();
            else if (_ui.TitleMenu != null && _ui.TitleMenu.Visible && _ui.TitleMenu.Buttons.Count > 0) _ui.TitleMenu.Buttons[0].Focus();
        }

        void OnSettingsEdited(GameSettings edited)
        {
            if (SettingsManager.Instance != null) SettingsManager.Instance.Apply(edited);
            else
            {
                // No settings manager in this scene: still apply what we can for this session.
                _fallbackSettings = edited.Clone().Sanitize();
                KeyBindings.ApplyOverrides(_fallbackSettings.bindingOverridesJson);
                ApplySettings(_fallbackSettings);
            }
        }

        void OnPhaseChanged(GamePhase phase)
        {
            // Leaving the menus (resume, new yard) closes the options too.
            if (phase == GamePhase.Playing && Options != null && Options.IsOpen) Options.Close();
            if (phase == GamePhase.TitleMenu) CaptionDirector.Queue.Clear();
        }

        /// <summary>Palette class on the UI root and caption options.</summary>
        public void ApplySettings(GameSettings s)
        {
            if (s == null) return;
            CaptionDirector.Enabled = s.captions;
            if (!s.captions) CaptionDirector.Queue.Clear();
            Captions?.ApplySettings(s);

            var root = _ui != null ? _ui.Root : null;
            if (root != null)
            {
                foreach (ColorBlindMode mode in System.Enum.GetValues(typeof(ColorBlindMode)))
                {
                    string cls = AccessibilityPalette.UssClass(mode);
                    if (cls != null) root.EnableInClassList(cls, mode == s.colorBlindMode);
                }
            }
        }

        void RefreshCard()
        {
            if (TutorialCard == null) return;
            if (_tutorial == null) TutorialCard.Set("", "", false);
            else TutorialCard.Set(_tutorial.CardTitle, _tutorial.CardText, _tutorial.CardIsHint);
            Options?.SetTutorialActive(_tutorial != null && _tutorial.Model.IsActive);
        }

        void Update()
        {
            if (!_attached)
            {
                TryAttach();
                return;
            }

            CaptionDirector.Queue.Tick(Time.unscaledDeltaTime);
            Captions.UpdateFade();

            // Key capture for the rebinding screen (Escape is handled by UIManager via HandleBack).
            if (Options.IsOpen && Options.ListeningAction.HasValue)
            {
                if (_skipInputFrame) _skipInputFrame = false;
                else if (GameInput.TryGetPressedControl(out string control)) Options.CompleteRebind(control);
            }
            else _skipInputFrame = true;
        }

        static Vector3? ListenerForward()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.forward : (Vector3?)null;
        }
    }
}

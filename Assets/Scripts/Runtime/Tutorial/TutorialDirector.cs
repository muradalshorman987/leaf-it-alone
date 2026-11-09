using System;
using FallenYard.Core;
using FallenYard.Leaves;
using FallenYard.Progression;
using FallenYard.Save;
using FallenYard.Settings;
using FallenYard.UI;
using UnityEngine;

namespace FallenYard.Tutorial
{
    /// <summary>
    /// Runs the first-yard tutorial (<see cref="TutorialModel"/>) and contextual hints
    /// (<see cref="HintRules"/>) from <see cref="GameEvents"/>, and saves progress in
    /// <see cref="SaveData.tutorial"/>. It only decides what to say: the UI shows
    /// <see cref="CardTitle"/>/<see cref="CardText"/> and listens to <see cref="CardChanged"/>.
    /// Zero setup: added next to the UIManager by <see cref="Accessibility.OnboardingBootstrap"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialDirector : MonoBehaviour, ISaveable
    {
        public static TutorialDirector Instance { get; private set; }

        [Tooltip("Seconds a contextual hint stays on screen.")]
        public float hintSeconds = 8f;
        [Tooltip("Seconds between hint checks.")]
        public float hintCheckInterval = 1f;
        [Tooltip("Gusts at least this strong can trigger the wind hint.")]
        public float minGustStrength = 0.5f;

        public TutorialModel Model { get; } = new TutorialModel();

        /// <summary>False when the player turned "Tutorial and hints" off.</summary>
        public bool HintsEnabled { get; private set; } = true;

        public string CardTitle { get; private set; } = "";
        public string CardText { get; private set; } = "";
        /// <summary>True while the card shows a contextual hint rather than a tutorial step.</summary>
        public bool CardIsHint { get; private set; }
        public bool HasCard => !string.IsNullOrEmpty(CardText);

        /// <summary>Card text or visibility changed.</summary>
        public event Action CardChanged;

        GamePhase _phase = GamePhase.Boot;
        bool _toggleToolUse;
        string _hintId;
        float _hintRemaining;
        float _sinceLastHint = HintRules.MinGapSeconds;
        float _sinceBag;
        float _nextCheck;
        float _gustFlag;
        bool _toolUnlockedFlag;
        UpgradeManager _upgrades;
        MetaProgressionService _meta;

        static SaveData Data => GameManager.Instance != null ? GameManager.Instance.Data : null;
        TutorialSave Save => Data?.tutorial;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            Model.StepChanged += OnStepChanged;
        }

        void OnDestroy()
        {
            Model.StepChanged -= OnStepChanged;
            if (Instance == this) Instance = null;
        }

        void OnEnable()
        {
            GameEvents.StateChanged += OnPhase;
            GameEvents.PileBagged += OnPileBagged;
            GameEvents.YardCompleted += OnYardCompleted;
            GameEvents.WindGust += OnWindGust;
            SettingsManager.Changed += ApplySettings;
            KeyBindings.Changed += RefreshCard;
            SaveRegistry.Register(this, Data);
        }

        void OnDisable()
        {
            GameEvents.StateChanged -= OnPhase;
            GameEvents.PileBagged -= OnPileBagged;
            GameEvents.YardCompleted -= OnYardCompleted;
            GameEvents.WindGust -= OnWindGust;
            SettingsManager.Changed -= ApplySettings;
            KeyBindings.Changed -= RefreshCard;
            SaveRegistry.Unregister(this);
            SetUnlockSources(null, null);
        }

        void Start()
        {
            if (SettingsManager.Instance != null) ApplySettings(SettingsManager.Instance.Current);
            _phase = UIManager.Instance != null && UIManager.Instance.Flow != null ? UIManager.Instance.Flow.Phase : GamePhase.Playing;
            if (Save != null) Model.Load(Save);
            SetUnlockSources(UpgradeManager.Instance, MetaProgressionService.Instance);
            RefreshCard();
        }

        void Update() => Step(Time.unscaledDeltaTime, Time.deltaTime);

        /// <summary>One frame of tutorial logic. Public so tests can drive it.</summary>
        public void Step(float unscaledDt, float dt)
        {
            if (_phase != GamePhase.Playing) return;

            _sinceBag += dt;
            if (_gustFlag > 0f) _gustFlag -= unscaledDt;

            if (Model.IsActive)
            {
                if (!GameState.InputBlocked && GameInput.Move.sqrMagnitude > 0.01f) Model.NotifyMoved(dt);
                var piles = PileDetector.Instance;
                if (piles != null && piles.Piles.Count > 0) Model.NotifyPileFormed();
                Model.Tick(dt);
            }

            if (_hintId != null)
            {
                _hintRemaining -= unscaledDt;
                if (_hintRemaining <= 0f) ClearHint();
            }
            else _sinceLastHint += unscaledDt;

            _nextCheck -= unscaledDt;
            if (_nextCheck <= 0f || _toolUnlockedFlag || _gustFlag > 0f)
            {
                _nextCheck = hintCheckInterval;
                CheckHints();
            }
        }

        // ------------------------------------------------------------ public API

        public void SkipTutorial() => Model.Skip();

        public void ReplayTutorial()
        {
            ClearHint();
            Model.Restart();
        }

        /// <summary>Shows a hint now, ignoring the rules (used by tests and future systems).</summary>
        public void ShowHint(string hintId)
        {
            string text = HintRules.Text(hintId, KeyBindings.Current);
            if (string.IsNullOrEmpty(text)) return;
            _hintId = hintId;
            _hintRemaining = hintSeconds;
            HintRules.MarkShown(hintId, Save);
            RefreshCard();
        }

        public void ApplySettings(GameSettings s)
        {
            if (s == null) return;
            HintsEnabled = s.tutorialHints;
            _toggleToolUse = s.toggleToolUse;
            if (!HintsEnabled) _hintId = null;
            RefreshCard();
        }

        /// <summary>Where tool unlocks come from. Called on Start; tests pass their own.</summary>
        public void SetUnlockSources(UpgradeManager upgrades, MetaProgressionService meta)
        {
            if (_upgrades != null) _upgrades.OnToolUnlocked -= OnToolUnlocked;
            if (_meta != null) _meta.ToolGranted -= OnToolUnlocked;
            _upgrades = upgrades;
            _meta = meta;
            if (_upgrades != null) _upgrades.OnToolUnlocked += OnToolUnlocked;
            if (_meta != null) _meta.ToolGranted += OnToolUnlocked;
        }

        // ------------------------------------------------------------ ISaveable

        public void CaptureState(SaveData data)
        {
            if (data == null) return;
            data.tutorial ??= new TutorialSave();
            Model.Store(data.tutorial);
        }

        public void RestoreState(SaveData data)
        {
            if (data?.tutorial == null) return;
            Model.Load(data.tutorial);
            RefreshCard();
        }

        // ------------------------------------------------------------ events

        void OnPhase(GamePhase phase)
        {
            _phase = phase;
            Model.NotifyPhase(phase);
            RefreshCard();
        }

        void OnPileBagged(int leaves, int coins)
        {
            _sinceBag = 0f;
            Model.NotifyPileBagged();
        }

        void OnYardCompleted(string yardId) => Model.NotifyYardCompleted();

        void OnWindGust(Vector3 direction, float strength)
        {
            if (strength >= minGustStrength) _gustFlag = 3f;
        }

        void OnToolUnlocked(string toolId) => _toolUnlockedFlag = true;

        void OnStepChanged(TutorialStep step)
        {
            if (Save != null) Model.Store(Save);
            if (step == TutorialStep.Done) _sinceLastHint = 0f; // let the player breathe before hints
            RefreshCard();
        }

        // ------------------------------------------------------------ hints

        void CheckHints()
        {
            if (!HintsEnabled || _hintId != null) return;
            var gm = GameManager.Instance;
            int coins = gm != null && gm.Data != null ? gm.Coins : 0;
            var context = new HintContext
            {
                Phase = _phase,
                TutorialActive = Model.IsActive,
                Coins = coins,
                CheapestPurchase = CheapestPurchase(UpgradeManager.Instance, coins),
                PileCount = PileDetector.Instance != null ? PileDetector.Instance.Piles.Count : 0,
                SecondsSinceBag = _sinceBag,
                GustJustHappened = _gustFlag > 0f,
                ToolJustUnlocked = _toolUnlockedFlag,
                SecondsSinceLastHint = _sinceLastHint,
            };

            string id = HintRules.Choose(context, Save ?? _sessionHints);
            if (id == HintRules.NewTool) _toolUnlockedFlag = false;
            if (id == HintRules.Wind) _gustFlag = 0f;
            if (id == null) return;
            HintRules.MarkShown(id, Save == null ? _sessionHints : null);
            ShowHint(id);
        }

        // Hint limits when there is no save (scene without a GameManager).
        readonly TutorialSave _sessionHints = new TutorialSave();

        void ClearHint()
        {
            if (_hintId == null) return;
            _hintId = null;
            _sinceLastHint = 0f;
            RefreshCard();
        }

        /// <summary>Cheapest unlock or upgrade in the shop, or -1 when nothing is for sale.</summary>
        public static int CheapestPurchase(UpgradeManager upgrades, int coins)
        {
            int best = -1;
            foreach (var row in ShopModel.BuildRows(upgrades, coins))
            {
                if (row.Action == ShopAction.Maxed || row.Cost < 0) continue;
                if (best < 0 || row.Cost < best) best = row.Cost;
            }
            return best;
        }

        // ------------------------------------------------------------ card

        void RefreshCard()
        {
            string title = "", text = "";
            bool isHint = false;
            if (HintsEnabled && _phase == GamePhase.Playing)
            {
                if (Model.IsActive)
                {
                    title = Model.Progress;
                    text = Model.Prompt(KeyBindings.Current, _toggleToolUse);
                }
                else if (_hintId != null)
                {
                    title = "Tip";
                    text = HintRules.Text(_hintId, KeyBindings.Current);
                    isHint = true;
                }
            }

            if (title == CardTitle && text == CardText && isHint == CardIsHint) return;
            CardTitle = title;
            CardText = text;
            CardIsHint = isHint;
            CardChanged?.Invoke();
        }
    }
}

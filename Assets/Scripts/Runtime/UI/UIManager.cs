using System;
using System.Collections.Generic;
using FallenYard.Core;
using FallenYard.Levels;
using FallenYard.Progression;
using FallenYard.Tools;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace FallenYard.UI
{
    /// <summary>
    /// Owns the UI Toolkit document and every screen: HUD, title menu, pause menu,
    /// upgrade shop, yard-complete card and toasts. Drives the game phase through
    /// <see cref="UIFlow"/> (which raises <see cref="GameEvents.StateChanged"/>) and
    /// applies its side effects: time scale, cursor lock and <see cref="GameState.InputBlocked"/>.
    ///
    /// Zero setup: add it to any GameObject. It adds a UIDocument and, if none is
    /// assigned, creates PanelSettings at runtime using Resources/FallenYardTheme.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Tooltip("Optional. Leave empty to create panel settings at runtime (scale with screen, 1920x1080 reference).")]
        public PanelSettings panelSettings;

        [Tooltip("Optional. Leave empty to load Resources/FallenYardUI.")]
        public StyleSheet styleSheet;

        [Tooltip("Open on the title menu. Turn off to drop straight into the yard (handy while testing).")]
        public bool showTitleOnStart = true;

        [Tooltip("Seconds a toast stays on screen.")]
        [Min(0.5f)] public float toastSeconds = 3f;

        public UIFlow Flow { get; private set; }
        public HudView Hud { get; private set; }
        public ShopView Shop { get; private set; }
        public MenuScreen TitleMenu { get; private set; }
        public MenuScreen PauseMenu { get; private set; }
        public MenuScreen YardCompleteCard { get; private set; }
        public YardMapView YardMap { get; private set; }
        /// <summary>"On to the next yard" button on the yard-complete card (hidden when there's nowhere to go).</summary>
        public Button NextYardButton { get; private set; }
        public ToastQueue Toasts { get; private set; }
        public ToastView ToastView { get; private set; }

        /// <summary>Root of the UI document, once built. Other packages add their own views here.</summary>
        public VisualElement Root => _root;
        public bool IsBuilt => _built;

        /// <summary>Raised once every screen exists, so other packages can add buttons and views.</summary>
        public event Action Built;

        readonly List<IUIModal> _modals = new List<IUIModal>();

        UIDocument _document;
        VisualElement _root;
        BagInteractor _bag;
        YardProgress _yard;
        bool _built;
        string _arrivalNote;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            Flow = new UIFlow();
            Flow.PhaseChanged += OnPhaseChanged;
            Toasts = new ToastQueue();
            EnsureDocument();
        }

        void OnEnable()
        {
            GameEvents.PileBagged += OnPileBagged;
            GameEvents.ToolUpgraded += OnToolUpgraded;
            GameEvents.YardCompleted += OnYardCompleted;
            GameEvents.CoinsChanged += OnCoinsChanged;
            GameEvents.YardLoaded += OnYardLoaded;
            Hud?.Bind(LookupToolName);
        }

        void OnDisable()
        {
            GameEvents.PileBagged -= OnPileBagged;
            GameEvents.ToolUpgraded -= OnToolUpgraded;
            GameEvents.YardCompleted -= OnYardCompleted;
            GameEvents.CoinsChanged -= OnCoinsChanged;
            GameEvents.YardLoaded -= OnYardLoaded;
            Hud?.Unbind();
            Time.timeScale = 1f;
            GameState.InputBlocked = false;
        }

        void OnDestroy()
        {
            ToastView?.Unbind();
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            Build();
            _bag = FindFirstObjectByType<BagInteractor>();
            _yard = FindFirstObjectByType<YardProgress>();
            if (_yard != null) Hud.SetGoal(_yard.completeAtFraction);
            if (GameManager.Instance != null) Hud.SetCoins(GameManager.Instance.Coins);
            // Arriving from another yard skips the title (LevelFlow sets the flag before loading).
            bool arrived = GameState.ConsumeSkipTitle();
            Flow.SetPhase(showTitleOnStart && !arrived ? GamePhase.TitleMenu : GamePhase.Playing);
        }

        void Update()
        {
            if (!_built) return;

            var modal = TopModal();
            if (modal != null)
            {
                // An extra screen (options, rebinding) is on top: Escape closes it, Tab does nothing.
                if (UIInput.PausePressed) modal.HandleBack();
            }
            else if (UIInput.PausePressed) Flow.TogglePause();
            else if (GameInput.MenuPressed) Flow.ToggleShop();

            Hud.SetPrompt(Flow.Phase == GamePhase.Playing && _bag != null ? _bag.Prompt : "");

            Toasts.Tick(Time.unscaledDeltaTime);
            ToastView.UpdateFade();
        }

        void LateUpdate()
        {
            // Other scripts (e.g. the player controller's Start) may lock the cursor;
            // keep it free while a menu is up.
            if (_built && UIFlow.ShowsCursor(Flow.Phase) && Cursor.lockState != CursorLockMode.None)
                ApplyCursor(true);
        }

        // ---------------------------------------------------------------- setup

        void EnsureDocument()
        {
            _document = GetComponent<UIDocument>();
            if (_document != null)
            {
                if (_document.panelSettings == null) _document.panelSettings = panelSettings != null ? panelSettings : CreatePanelSettings();
                return;
            }

            // Add the document on an inactive child so it gets its panel settings before OnEnable runs.
            var host = new GameObject("UIDocument");
            host.SetActive(false);
            host.transform.SetParent(transform, false);
            _document = host.AddComponent<UIDocument>();
            _document.panelSettings = panelSettings != null ? panelSettings : CreatePanelSettings();
            host.SetActive(true);
        }

        static PanelSettings CreatePanelSettings()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "FallenYard Runtime PanelSettings";
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("FallenYardTheme");
            return settings;
        }

        void Build()
        {
            if (_built) return;
            _root = _document.rootVisualElement;
            if (_root == null)
            {
                // UIDocument was enabled before it had panel settings; re-enable to attach.
                _document.enabled = false;
                _document.enabled = true;
                _root = _document.rootVisualElement;
            }
            if (_root == null)
            {
                Debug.LogError("[UIManager] UIDocument has no root visual element; check its PanelSettings.", this);
                enabled = false;
                return;
            }

            var sheet = styleSheet != null ? styleSheet : Resources.Load<StyleSheet>("FallenYardUI");
            if (sheet != null) _root.styleSheets.Add(sheet);
            else Debug.LogWarning("[UIManager] Resources/FallenYardUI.uss not found; UI will be unstyled.", this);
            _root.AddToClassList("fy-root");
            _root.pickingMode = PickingMode.Ignore;

            Hud = new HudView();
            Hud.Bind(LookupToolName);
            _root.Add(Hud.Root);

            ToastView = new ToastView();
            ToastView.Bind(Toasts);
            _root.Add(ToastView.Root);

            Shop = new ShopView();
            Shop.PurchaseRequested += OnPurchaseRequested;
            Shop.CloseRequested += () => Flow.ToggleShop();
            _root.Add(Shop.Root);

            PauseMenu = new MenuScreen("pause", "Paused", "Hollow Creek will wait.");
            PauseMenu.AddButton("pause-resume", "Resume", () => Flow.Resume(), primary: true);
            PauseMenu.AddButton("pause-shop", "Tool Shed", () => Flow.ToggleShop());
            var pauseMap = PauseMenu.AddButton("pause-map", "Hollow Creek map", () => Flow.OpenYardMap());
            PauseMenu.AddButton("pause-title", "Save and quit to title", () => Flow.ReturnToTitle());
            PauseMenu.AddButton("pause-quit", "Quit game", QuitGame);
            _root.Add(PauseMenu.Root);

            YardCompleteCard = new MenuScreen("yard-complete", "Yard cleaned!", "Your neighbour left a little thank-you in your coin jar.", "fy-overlay--soft");
            NextYardButton = YardCompleteCard.AddButton("yard-complete-next", "On to the next yard", GoToNextYard, primary: true);
            YardCompleteCard.AddButton("yard-complete-continue", "Keep raking", () => Flow.Resume());
            YardCompleteCard.AddButton("yard-complete-shop", "Visit the Tool Shed", () => Flow.ToggleShop());
            var completeMap = YardCompleteCard.AddButton("yard-complete-map", "Hollow Creek map", () => Flow.OpenYardMap());
            _root.Add(YardCompleteCard.Root);

            TitleMenu = new MenuScreen("title", "Fallen Yard", "An autumn in Hollow Creek", "fy-overlay--title");
            TitleMenu.AddButton("title-play", TitlePlayText(), PlayFromTitle, primary: true);
            var titleMap = TitleMenu.AddButton("title-map", "Hollow Creek map", () => Flow.OpenYardMap());
            TitleMenu.AddButton("title-quit", "Quit", QuitGame);
            _root.Add(TitleMenu.Root);

            YardMap = new YardMapView();
            YardMap.RowChosen += OnYardRowChosen;
            YardMap.CloseRequested += () => Flow.CloseYardMap();
            _root.Add(YardMap.Root);

            // Scenes without yard travel (e.g. the test yard) hide the map buttons.
            bool hasYards = LevelFlow.Instance != null && LevelFlow.Instance.catalog != null;
            UIViewUtil.SetVisible(pauseMap, hasYards);
            UIViewUtil.SetVisible(completeMap, hasYards);
            UIViewUtil.SetVisible(titleMap, hasYards);

            _built = true;
            Built?.Invoke();
        }

        // ---------------------------------------------------------------- modals

        /// <summary>Registers a screen that sits on top of the menus and takes Escape while open.</summary>
        public void RegisterModal(IUIModal modal)
        {
            if (modal != null && !_modals.Contains(modal)) _modals.Add(modal);
        }

        public void UnregisterModal(IUIModal modal) => _modals.Remove(modal);

        /// <summary>The most recently registered open modal, or null.</summary>
        public IUIModal TopModal()
        {
            for (int i = _modals.Count - 1; i >= 0; i--)
                if (_modals[i] != null && _modals[i].IsOpen) return _modals[i];
            return null;
        }

        static string TitlePlayText()
        {
            var gm = GameManager.Instance;
            bool hasProgress = gm != null && gm.Data != null && (gm.Data.coins > 0 || gm.Data.completedYards.Count > 0);
            return hasProgress ? "Continue" : "Start raking";
        }

        // ---------------------------------------------------------------- phase

        void OnPhaseChanged(GamePhase previous, GamePhase next)
        {
            GameState.InputBlocked = UIFlow.BlocksInput(next);
            Time.timeScale = UIFlow.FreezesTime(next) ? 0f : 1f;
            ApplyCursor(UIFlow.ShowsCursor(next));

            if (!_built) return;

            UIViewUtil.SetVisible(Hud.Root, next != GamePhase.TitleMenu && next != GamePhase.Boot);
            if (next == GamePhase.TitleMenu)
            {
                TitleMenu.Buttons[0].text = TitlePlayText();
                TitleMenu.Show();
            }
            else TitleMenu.Hide();

            if (next == GamePhase.Paused) PauseMenu.Show(); else PauseMenu.Hide();
            if (next == GamePhase.YardComplete)
            {
                RefreshNextYardButton();
                YardCompleteCard.Show();
            }
            else YardCompleteCard.Hide();

            if (next == GamePhase.YardMap)
            {
                RefreshYardMap();
                YardMap.Show();
            }
            else YardMap.Hide();

            if (next == GamePhase.Playing && !string.IsNullOrEmpty(_arrivalNote))
            {
                Toasts.Push(_arrivalNote, toastSeconds * 3f);
                _arrivalNote = null;
            }

            if (next == GamePhase.Shop)
            {
                RefreshShop();
                Shop.Show();
            }
            else Shop.Hide();

            if ((next == GamePhase.Paused || next == GamePhase.TitleMenu) && GameManager.Instance != null)
                GameManager.Instance.Save();
        }

        static void ApplyCursor(bool free)
        {
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        // ---------------------------------------------------------------- shop

        void RefreshShop()
        {
            int coins = GameManager.Instance != null ? GameManager.Instance.Coins : 0;
            Shop.Refresh(ShopModel.BuildRows(UpgradeManager.Instance, coins), coins);
        }

        void OnPurchaseRequested(ShopRow row)
        {
            if (!ShopModel.TryBuy(UpgradeManager.Instance, row)) return;
            if (row.Action == ShopAction.Unlock) Toasts.Push($"{row.Title} unlocked", toastSeconds);
            RefreshShop();
        }

        // ---------------------------------------------------------------- events

        void OnCoinsChanged(int _)
        {
            if (_built && Flow.Phase == GamePhase.Shop) RefreshShop();
            if (_built && Flow.Phase == GamePhase.YardMap) RefreshYardMap();
        }

        void OnPileBagged(int leaves, int coins) => Toasts.Push(HudFormat.PileBagged(leaves, coins), toastSeconds);

        void OnToolUpgraded(string toolId, int level) =>
            Toasts.Push($"{HudFormat.ToolName(toolId, LookupToolName)} upgraded to Lv {level}", toastSeconds);

        void OnYardCompleted(string yardId) => Flow.OnYardCompleted();

        // ---------------------------------------------------------------- yards (WP7)

        /// <summary>Title "Start raking / Continue": travels to the saved yard if it's another scene, else plays here.</summary>
        void PlayFromTitle()
        {
            var flow = LevelFlow.Instance;
            if (flow != null && flow.TryContinueElsewhere()) return;
            Flow.StartGame();
        }

        void OnYardLoaded(string yardId)
        {
            var def = LevelFlow.Instance != null ? LevelFlow.Instance.GetYard(yardId) : null;
            _arrivalNote = def != null ? HudFormat.ArrivalNote(def.displayName, def.neighbourName, def.neighbourNote) : null;
            if (_built && Flow.Phase == GamePhase.Playing && _arrivalNote != null)
            {
                Toasts.Push(_arrivalNote, toastSeconds * 3f);
                _arrivalNote = null;
            }
        }

        void RefreshYardMap()
        {
            var flow = LevelFlow.Instance;
            var gm = GameManager.Instance;
            int coins = gm != null ? gm.Coins : 0;
            var rows = YardMapModel.BuildRows(flow != null ? flow.catalog : null, gm != null ? gm.Data : null,
                flow != null ? flow.currentYardId : null, coins);
            YardMap.Refresh(rows, coins);
        }

        void OnYardRowChosen(YardMapRow row)
        {
            switch (row.Action)
            {
                case YardMapAction.Current:
                    Flow.Resume();
                    break;
                case YardMapAction.Play:
                    GameEvents.RaiseYardTravelRequested(row.YardId);
                    break;
                case YardMapAction.Buy:
                    if (TryOpenYard(row.YardId, row.Title)) RefreshYardMap();
                    break;
            }
        }

        bool TryOpenYard(string yardId, string title)
        {
            var meta = MetaProgressionService.Instance;
            if (meta == null || !meta.TryPurchaseYard(yardId)) return false;
            Toasts.Push($"{title} is open", toastSeconds);
            return true;
        }

        void RefreshNextYardButton()
        {
            var flow = LevelFlow.Instance;
            string text = null;
            if (flow != null)
            {
                var (next, status) = flow.NextYard;
                var rule = next != null && flow.catalog != null && flow.catalog.unlockTable != null ? flow.catalog.unlockTable.Get(next.yardId) : null;
                text = YardMapModel.NextYardButtonText(next, status, rule != null ? rule.unlockCost : 0);
            }
            UIViewUtil.SetVisible(NextYardButton, text != null);
            if (text != null) NextYardButton.text = text;
        }

        void GoToNextYard()
        {
            var flow = LevelFlow.Instance;
            if (flow == null) return;
            var (next, status) = flow.NextYard;
            if (next == null) return;
            if (status == YardStatus.Purchasable && !TryOpenYard(next.yardId, next.displayName))
            {
                Flow.OpenYardMap();
                return;
            }
            GameEvents.RaiseYardTravelRequested(next.yardId);
        }

        static string LookupToolName(string toolId)
        {
            var upgrades = UpgradeManager.Instance;
            if (upgrades == null) return null;
            var def = upgrades.GetDefinition(toolId);
            return def != null ? def.displayName : null;
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

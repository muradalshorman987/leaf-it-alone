using FallenYard.Core;
using FallenYard.Leaves;
using FallenYard.Player;
using FallenYard.Progression;
using FallenYard.Tools;
using UnityEngine;

namespace FallenYard.UI
{
    /// <summary>
    /// Developer overlay (IMGUI). When a <see cref="UIManager"/> is in the scene this
    /// only draws a stats box, is hidden by default and toggles with F3 in the editor
    /// and development builds. Without a UIManager it falls back to the original
    /// zero-setup HUD + Tab shop so old scenes stay playable.
    /// </summary>
    public class DebugHUD : MonoBehaviour
    {
        public ToolController toolController;
        public BagInteractor bagInteractor;
        public YardProgress yardProgress;

        [Tooltip("Show the stats overlay as soon as play starts (editor and development builds only). F3 toggles it.")]
        public bool visibleOnStart;

        bool _shopOpen;
        bool _visible;
        bool _hasRealUI;
        float _cleanedBannerUntil;
        GUIStyle _big, _label;

        void Start()
        {
            if (toolController == null) toolController = FindFirstObjectByType<ToolController>();
            if (bagInteractor == null) bagInteractor = FindFirstObjectByType<BagInteractor>();
            if (yardProgress == null) yardProgress = FindFirstObjectByType<YardProgress>();
            if (yardProgress != null) yardProgress.OnYardCleaned += () => _cleanedBannerUntil = Time.time + 6f;

            _hasRealUI = FindFirstObjectByType<UIManager>() != null;
            if (_hasRealUI && !Debug.isDebugBuild)
            {
                enabled = false;
                return;
            }
            _visible = visibleOnStart || !_hasRealUI;
        }

        void Update()
        {
            if (UIInput.DebugTogglePressed) _visible = !_visible;
            if (_hasRealUI) return; // UIManager owns Tab, pause and the shop.

            if (GameInput.MenuPressed)
            {
                _shopOpen = !_shopOpen;
                GameState.InputBlocked = _shopOpen;
                FirstPersonController.SetCursorLocked(!_shopOpen);
            }
        }

        void OnGUI()
        {
            if (!_visible) return;
            if (_hasRealUI)
            {
                DrawDevStats();
                return;
            }

            _big ??= new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };

            var field = LeafField.Instance;
            int coins = GameManager.Instance != null ? GameManager.Instance.Coins : 0;
            float clean = field != null ? field.CleanFraction * 100f : 0f;
            int piles = PileDetector.Instance != null ? PileDetector.Instance.Piles.Count : 0;
            string tool = toolController != null && toolController.Current != null ? toolController.Current.displayName : "-";

            GUI.Box(new Rect(10, 10, 260, 110), GUIContent.none);
            GUI.Label(new Rect(20, 16, 240, 24), $"Coins: {coins}", _label);
            GUI.Label(new Rect(20, 40, 240, 24), $"Yard clean: {clean:0.0}%", _label);
            GUI.Label(new Rect(20, 64, 240, 24), $"Piles: {piles}   Tool: {tool}", _label);
            GUI.Label(new Rect(20, 88, 240, 24), "1/2 tools  E bag  Tab shop", _label);

            // Crosshair
            GUI.Label(new Rect(Screen.width / 2f - 5, Screen.height / 2f - 12, 20, 24), "+", _label);

            if (bagInteractor != null && !string.IsNullOrEmpty(bagInteractor.Prompt))
                GUI.Label(new Rect(Screen.width / 2f - 150, Screen.height / 2f + 30, 300, 30), bagInteractor.Prompt, _label);

            if (Time.time < _cleanedBannerUntil)
                GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 50), "Yard cleaned! Bonus coins earned.", _big);

            if (_shopOpen) DrawShop();
        }

        /// <summary>Compact stats box in the top-right corner, next to the real HUD.</summary>
        void DrawDevStats()
        {
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 14 };
            var field = LeafField.Instance;
            float clean = field != null ? field.CleanFraction * 100f : 0f;
            int piles = PileDetector.Instance != null ? PileDetector.Instance.Piles.Count : 0;
            int leaves = field != null ? field.TotalLeaves : 0;
            float fps = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f;

            var box = new Rect(Screen.width - 230, 10, 220, 92);
            GUI.Box(box, "Dev (F3)");
            GUI.Label(new Rect(box.x + 10, box.y + 20, 200, 20), $"FPS {fps:0}   Leaves {leaves}", _label);
            GUI.Label(new Rect(box.x + 10, box.y + 42, 200, 20), $"Clean {clean:0.0}%   Piles {piles}", _label);
            GUI.Label(new Rect(box.x + 10, box.y + 64, 200, 20), $"Phase {(UIManager.Instance != null ? UIManager.Instance.Flow.Phase.ToString() : "-")}", _label);
        }

        void DrawShop()
        {
            var um = UpgradeManager.Instance;
            float w = 420, h = 60 + (um != null ? um.definitions.Length : 0) * 44;
            var area = new Rect(Screen.width / 2f - w / 2, Screen.height / 2f - h / 2, w, h);
            GUI.Box(area, "Upgrades (Tab to close)");
            if (um == null) return;

            float y = area.y + 30;
            foreach (var def in um.definitions)
            {
                if (def == null) continue;
                var row = new Rect(area.x + 12, y, w - 24, 36);
                int level = um.GetLevel(def.toolId);
                GUI.Label(new Rect(row.x, row.y + 8, 220, 24), $"{def.displayName}  (Lv {level}/{def.levels.Length})", _label);

                var button = new Rect(row.xMax - 160, row.y, 160, 32);
                if (!um.IsUnlocked(def.toolId))
                {
                    if (GUI.Button(button, $"Unlock ({def.unlockCost})")) um.TryUnlock(def.toolId);
                }
                else
                {
                    int cost = um.NextUpgradeCost(def.toolId);
                    GUI.enabled = cost >= 0;
                    if (GUI.Button(button, cost >= 0 ? $"Upgrade ({cost})" : "Maxed")) um.TryUpgrade(def.toolId);
                    GUI.enabled = true;
                }
                y += 44;
            }
        }
    }
}

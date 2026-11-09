using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallenYard.Core
{
    /// <summary>Gameplay actions the player can rebind. Pause (Escape) and the debug overlay (F3) are fixed.</summary>
    public enum GameAction
    {
        MoveForward,
        MoveBack,
        MoveLeft,
        MoveRight,
        Sprint,
        Jump,
        UseTool,
        Interact,
        Shop,
        Tool1,
        Tool2,
        Tool3,
        Tool4,
    }

    /// <summary>
    /// Which key or mouse button drives each <see cref="GameAction"/>. Pure C#: <see cref="GameInput"/>
    /// reads <see cref="Current"/>, the options screen edits it, and the overrides are stored as JSON in
    /// <c>GameSettings.bindingOverridesJson</c> (only actions that differ from the defaults).
    ///
    /// Controls are named after Input System <c>Key</c> values ("W", "LeftShift", "Digit1") plus
    /// "Mouse0".."Mouse4" for mouse buttons, so the same names work on both input backends
    /// (see <see cref="TryGetLegacyKeyCode"/>).
    /// </summary>
    public class KeyBindings
    {
        /// <summary>One bindable control: Input System name, label shown to the player, legacy KeyCode.</summary>
        public readonly struct ControlInfo
        {
            public readonly string Name;
            public readonly string DisplayName;
            public readonly KeyCode LegacyKey;

            public ControlInfo(string name, string displayName, KeyCode legacyKey)
            {
                Name = name;
                DisplayName = displayName;
                LegacyKey = legacyKey;
            }

            public bool IsMouse => Name.StartsWith("Mouse", StringComparison.Ordinal);
        }

        [Serializable]
        class Entry
        {
            public string action;
            public string control;
        }

        [Serializable]
        class Overrides
        {
            public List<Entry> bindings = new List<Entry>();
        }

        /// <summary>Bindings used by <see cref="GameInput"/>.</summary>
        public static KeyBindings Current { get; private set; }

        /// <summary>Raised after any binding of <see cref="Current"/> changes.</summary>
        public static event Action Changed;

        /// <summary>Every action, in the order the options screen lists them.</summary>
        public static readonly GameAction[] AllActions = (GameAction[])Enum.GetValues(typeof(GameAction));

        static readonly Dictionary<GameAction, string> Defaults = new Dictionary<GameAction, string>
        {
            { GameAction.MoveForward, "W" },
            { GameAction.MoveBack, "S" },
            { GameAction.MoveLeft, "A" },
            { GameAction.MoveRight, "D" },
            { GameAction.Sprint, "LeftShift" },
            { GameAction.Jump, "Space" },
            { GameAction.UseTool, "Mouse0" },
            { GameAction.Interact, "E" },
            { GameAction.Shop, "Tab" },
            { GameAction.Tool1, "Digit1" },
            { GameAction.Tool2, "Digit2" },
            { GameAction.Tool3, "Digit3" },
            { GameAction.Tool4, "Digit4" },
        };

        static readonly List<ControlInfo> ControlList = BuildControls();
        static readonly Dictionary<string, ControlInfo> ControlsByName = BuildLookup();

        /// <summary>Every key/button an action can be bound to. Escape and F3 are not in the list.</summary>
        public static IReadOnlyList<ControlInfo> Controls => ControlList;

        readonly Dictionary<GameAction, string> _bindings = new Dictionary<GameAction, string>();

        // Runs after the static tables above are initialised.
        static KeyBindings() => Current = new KeyBindings();

        public KeyBindings()
        {
            foreach (var pair in Defaults) _bindings[pair.Key] = pair.Value;
        }

        // ------------------------------------------------------------ queries

        public static string DefaultFor(GameAction action) => Defaults.TryGetValue(action, out var c) ? c : "";

        public string Get(GameAction action) => _bindings.TryGetValue(action, out var c) ? c : DefaultFor(action);

        /// <summary>What to show the player for this action, e.g. "E", "Left Shift", "Left Mouse".</summary>
        public string Label(GameAction action) => DisplayName(Get(action));

        public bool IsDefault(GameAction action) => Get(action) == DefaultFor(action);

        public bool AllDefault
        {
            get
            {
                foreach (var a in AllActions)
                    if (!IsDefault(a)) return false;
                return true;
            }
        }

        /// <summary>The action already using <paramref name="control"/>, or null.</summary>
        public GameAction? ActionUsing(string control)
        {
            foreach (var a in AllActions)
                if (Get(a) == control) return a;
            return null;
        }

        public static bool IsValidControl(string control) =>
            !string.IsNullOrEmpty(control) && ControlsByName.ContainsKey(control);

        public static string DisplayName(string control) =>
            !string.IsNullOrEmpty(control) && ControlsByName.TryGetValue(control, out var info) ? info.DisplayName : "?";

        public static bool TryGetControl(string control, out ControlInfo info)
        {
            info = default;
            return !string.IsNullOrEmpty(control) && ControlsByName.TryGetValue(control, out info);
        }

        public static bool TryGetLegacyKeyCode(string control, out KeyCode key)
        {
            key = KeyCode.None;
            if (!TryGetControl(control, out var info)) return false;
            key = info.LegacyKey;
            return true;
        }

        // ------------------------------------------------------------ edits

        /// <summary>
        /// Binds <paramref name="action"/> to <paramref name="control"/>. If another action already used
        /// that control, the two swap, so no action is ever left unbound. Returns the action that was
        /// moved to the old control (or null), and false in <paramref name="changed"/> for invalid input.
        /// </summary>
        public GameAction? Set(GameAction action, string control, out bool changed)
        {
            changed = false;
            if (!IsValidControl(control)) return null;
            string previous = Get(action);
            if (previous == control) return null;

            GameAction? swapped = ActionUsing(control);
            _bindings[action] = control;
            if (swapped.HasValue) _bindings[swapped.Value] = previous;
            changed = true;
            RaiseIfCurrent();
            return swapped;
        }

        public GameAction? Set(GameAction action, string control) => Set(action, control, out _);

        public void ResetToDefaults()
        {
            if (AllDefault) return;
            foreach (var pair in Defaults) _bindings[pair.Key] = pair.Value;
            RaiseIfCurrent();
        }

        public void CopyFrom(KeyBindings other)
        {
            if (other == null) return;
            bool any = false;
            foreach (var a in AllActions)
            {
                string c = other.Get(a);
                if (Get(a) == c) continue;
                _bindings[a] = c;
                any = true;
            }
            if (any) RaiseIfCurrent();
        }

        public KeyBindings Clone()
        {
            var copy = new KeyBindings();
            foreach (var a in AllActions) copy._bindings[a] = Get(a);
            return copy;
        }

        void RaiseIfCurrent()
        {
            if (ReferenceEquals(this, Current)) Changed?.Invoke();
        }

        // ------------------------------------------------------------ JSON

        /// <summary>Only the actions that differ from the defaults. Empty string when everything is default.</summary>
        public string ToJson()
        {
            var o = new Overrides();
            foreach (var a in AllActions)
                if (!IsDefault(a)) o.bindings.Add(new Entry { action = a.ToString(), control = Get(a) });
            return o.bindings.Count == 0 ? "" : JsonUtility.ToJson(o);
        }

        /// <summary>
        /// Loads overrides on top of the defaults. Unknown actions, invalid controls and duplicate
        /// controls are skipped, so a hand-edited or outdated file never leaves an action unusable.
        /// </summary>
        public void LoadJson(string json)
        {
            var loaded = FromJson(json);
            CopyFrom(loaded);
        }

        public static KeyBindings FromJson(string json)
        {
            var result = new KeyBindings();
            if (string.IsNullOrWhiteSpace(json)) return result;

            Overrides o = null;
            try { o = JsonUtility.FromJson<Overrides>(json); }
            catch (Exception) { /* not our format (e.g. an old InputActionAsset override string): keep defaults */ }
            if (o?.bindings == null) return result;

            foreach (var e in o.bindings)
            {
                if (e == null || !Enum.TryParse(e.action, out GameAction action) || !Enum.IsDefined(typeof(GameAction), action)) continue;
                if (!IsValidControl(e.control)) continue;
                result.Set(action, e.control);
            }
            return result;
        }

        // ------------------------------------------------------------ globals

        /// <summary>Loads the overrides stored in settings into <see cref="Current"/>.</summary>
        public static void ApplyOverrides(string json) => Current.LoadJson(json);

        /// <summary>For tests: fresh default bindings and no listeners.</summary>
        public static void ResetCurrent()
        {
            Current = new KeyBindings();
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => ResetCurrent();

        // ------------------------------------------------------------ control table

        static List<ControlInfo> BuildControls()
        {
            var list = new List<ControlInfo>();
            for (char c = 'A'; c <= 'Z'; c++)
                list.Add(new ControlInfo(c.ToString(), c.ToString(), KeyCode.A + (c - 'A')));
            for (int i = 0; i <= 9; i++)
                list.Add(new ControlInfo("Digit" + i, i.ToString(), KeyCode.Alpha0 + i));
            for (int i = 1; i <= 12; i++)
            {
                if (i == 3) continue; // F3 toggles the debug overlay
                list.Add(new ControlInfo("F" + i, "F" + i, KeyCode.F1 + (i - 1)));
            }
            for (int i = 0; i <= 9; i++)
                list.Add(new ControlInfo("Numpad" + i, "Num " + i, KeyCode.Keypad0 + i));

            void Add(string name, string label, KeyCode key) => list.Add(new ControlInfo(name, label, key));
            Add("Space", "Space", KeyCode.Space);
            Add("Enter", "Enter", KeyCode.Return);
            Add("Tab", "Tab", KeyCode.Tab);
            Add("Backspace", "Backspace", KeyCode.Backspace);
            Add("LeftShift", "Left Shift", KeyCode.LeftShift);
            Add("RightShift", "Right Shift", KeyCode.RightShift);
            Add("LeftCtrl", "Left Ctrl", KeyCode.LeftControl);
            Add("RightCtrl", "Right Ctrl", KeyCode.RightControl);
            Add("LeftAlt", "Left Alt", KeyCode.LeftAlt);
            Add("RightAlt", "Right Alt", KeyCode.RightAlt);
            Add("CapsLock", "Caps Lock", KeyCode.CapsLock);
            Add("UpArrow", "Up", KeyCode.UpArrow);
            Add("DownArrow", "Down", KeyCode.DownArrow);
            Add("LeftArrow", "Left", KeyCode.LeftArrow);
            Add("RightArrow", "Right", KeyCode.RightArrow);
            Add("Insert", "Insert", KeyCode.Insert);
            Add("Delete", "Delete", KeyCode.Delete);
            Add("Home", "Home", KeyCode.Home);
            Add("End", "End", KeyCode.End);
            Add("PageUp", "Page Up", KeyCode.PageUp);
            Add("PageDown", "Page Down", KeyCode.PageDown);
            Add("Backquote", "`", KeyCode.BackQuote);
            Add("Minus", "-", KeyCode.Minus);
            Add("Equals", "=", KeyCode.Equals);
            Add("LeftBracket", "[", KeyCode.LeftBracket);
            Add("RightBracket", "]", KeyCode.RightBracket);
            Add("Backslash", "\\", KeyCode.Backslash);
            Add("Semicolon", ";", KeyCode.Semicolon);
            Add("Quote", "'", KeyCode.Quote);
            Add("Comma", ",", KeyCode.Comma);
            Add("Period", ".", KeyCode.Period);
            Add("Slash", "/", KeyCode.Slash);
            Add("Mouse0", "Left Mouse", KeyCode.Mouse0);
            Add("Mouse1", "Right Mouse", KeyCode.Mouse1);
            Add("Mouse2", "Middle Mouse", KeyCode.Mouse2);
            Add("Mouse3", "Mouse 4", KeyCode.Mouse3);
            Add("Mouse4", "Mouse 5", KeyCode.Mouse4);
            return list;
        }

        static Dictionary<string, ControlInfo> BuildLookup()
        {
            var map = new Dictionary<string, ControlInfo>(StringComparer.Ordinal);
            foreach (var c in ControlList) map[c.Name] = c;
            return map;
        }
    }
}

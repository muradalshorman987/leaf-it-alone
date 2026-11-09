using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace FallenYard.Core
{
    /// <summary>
    /// Thin input wrapper. Uses the Input System package when it is the active
    /// backend, and falls back to the legacy Input Manager otherwise, so the
    /// project works whichever "Active Input Handling" option is selected.
    /// Keys and buttons come from <see cref="KeyBindings.Current"/>, so the
    /// options screen can rebind them; mouse look stays on the mouse.
    /// </summary>
    public static class GameInput
    {
        const float NewInputMouseScale = 0.05f; // pixels -> roughly legacy "Mouse X" units

        static KeyBindings Bindings => KeyBindings.Current;

        public static Vector2 Move
        {
            get
            {
                float x = (Held(GameAction.MoveRight) ? 1f : 0f) - (Held(GameAction.MoveLeft) ? 1f : 0f);
                float y = (Held(GameAction.MoveForward) ? 1f : 0f) - (Held(GameAction.MoveBack) ? 1f : 0f);
                return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            }
        }

        public static Vector2 Look
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var m = Mouse.current;
                return m == null ? Vector2.zero : m.delta.ReadValue() * NewInputMouseScale;
#else
                return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#endif
            }
        }

        public static bool Sprint => Held(GameAction.Sprint);

        /// <summary>Sprint went down this frame (for toggle-sprint).</summary>
        public static bool SprintPressed => Pressed(GameAction.Sprint);

        public static bool JumpPressed => Pressed(GameAction.Jump);

        /// <summary>Primary tool action (left mouse by default) held.</summary>
        public static bool PrimaryHeld => Held(GameAction.UseTool);

        /// <summary>Primary tool action went down this frame (for toggle tool use).</summary>
        public static bool PrimaryPressed => Pressed(GameAction.UseTool);

        /// <summary>Interact / bag a pile (E by default).</summary>
        public static bool InteractPressed => Pressed(GameAction.Interact);

        /// <summary>Toggle the upgrade shop (Tab by default).</summary>
        public static bool MenuPressed => Pressed(GameAction.Shop);

        /// <summary>Returns 0-based tool slot pressed this frame (keys 1-4 by default), or -1.</summary>
        public static int ToolSlotPressed
        {
            get
            {
                if (Pressed(GameAction.Tool1)) return 0;
                if (Pressed(GameAction.Tool2)) return 1;
                if (Pressed(GameAction.Tool3)) return 2;
                if (Pressed(GameAction.Tool4)) return 3;
                return -1;
            }
        }

        /// <summary>Short label of the control bound to an action, for prompts: "E", "Left Mouse".</summary>
        public static string Label(GameAction action) => Bindings.Label(action);

        public static bool Held(GameAction action) => IsHeld(Bindings.Get(action));

        public static bool Pressed(GameAction action) => WasPressed(Bindings.Get(action));

        /// <summary>
        /// First bindable key or mouse button that went down this frame (see <see cref="KeyBindings.Controls"/>).
        /// Used by the rebinding screen. Escape is never reported; callers treat it as "cancel".
        /// </summary>
        public static bool TryGetPressedControl(out string control)
        {
            foreach (var c in KeyBindings.Controls)
            {
                if (!WasPressed(c.Name)) continue;
                control = c.Name;
                return true;
            }
            control = null;
            return false;
        }

        // ------------------------------------------------------------ backend

        public static bool IsHeld(string control)
        {
#if ENABLE_INPUT_SYSTEM
            var button = Find(control);
            return button != null && button.isPressed;
#else
            return KeyBindings.TryGetLegacyKeyCode(control, out var key) && Input.GetKey(key);
#endif
        }

        public static bool WasPressed(string control)
        {
#if ENABLE_INPUT_SYSTEM
            var button = Find(control);
            return button != null && button.wasPressedThisFrame;
#else
            return KeyBindings.TryGetLegacyKeyCode(control, out var key) && Input.GetKeyDown(key);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        static ButtonControl Find(string control)
        {
            if (!KeyBindings.TryGetControl(control, out var info)) return null;
            if (info.IsMouse)
            {
                var m = Mouse.current;
                if (m == null) return null;
                switch (info.Name)
                {
                    case "Mouse0": return m.leftButton;
                    case "Mouse1": return m.rightButton;
                    case "Mouse2": return m.middleButton;
                    case "Mouse3": return m.backButton;
                    case "Mouse4": return m.forwardButton;
                    default: return null;
                }
            }

            var k = Keyboard.current;
            if (k == null || !System.Enum.TryParse(info.Name, out Key key) || key == Key.None) return null;
            return k[key];
        }
#endif
    }
}

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FallenYard.Core
{
    /// <summary>
    /// Thin input wrapper. Uses the Input System package when it is the active
    /// backend, and falls back to the legacy Input Manager otherwise, so the
    /// project works whichever "Active Input Handling" option is selected.
    /// Swap this for an InputActionAsset later if you want rebinding/gamepads.
    /// </summary>
    public static class GameInput
    {
        const float NewInputMouseScale = 0.05f; // pixels -> roughly legacy "Mouse X" units

        public static Vector2 Move
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                if (k == null) return Vector2.zero;
                float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
                float y = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
                return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
#else
                return Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
#endif
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

        public static bool Sprint
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
#else
                return Input.GetKey(KeyCode.LeftShift);
#endif
            }
        }

        public static bool JumpPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Space);
#endif
            }
        }

        /// <summary>Primary tool action (left mouse held).</summary>
        public static bool PrimaryHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
                return Input.GetMouseButton(0);
#endif
            }
        }

        /// <summary>Interact / bag a pile (E).</summary>
        public static bool InteractPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.E);
#endif
            }
        }

        /// <summary>Toggle the upgrade shop (Tab).</summary>
        public static bool MenuPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Tab);
#endif
            }
        }

        /// <summary>Returns 0-based tool slot pressed this frame (keys 1-4), or -1.</summary>
        public static int ToolSlotPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                if (k == null) return -1;
                if (k.digit1Key.wasPressedThisFrame) return 0;
                if (k.digit2Key.wasPressedThisFrame) return 1;
                if (k.digit3Key.wasPressedThisFrame) return 2;
                if (k.digit4Key.wasPressedThisFrame) return 3;
                return -1;
#else
                for (int i = 0; i < 4; i++)
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i)) return i;
                return -1;
#endif
            }
        }
    }
}

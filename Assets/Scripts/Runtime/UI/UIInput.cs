using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FallenYard.UI
{
    /// <summary>
    /// Menu keys the UI needs that <see cref="Core.GameInput"/> does not cover yet.
    /// Same dual-backend pattern as GameInput. (The shop keeps using GameInput.MenuPressed / Tab.)
    /// </summary>
    public static class UIInput
    {
        /// <summary>Pause / back (Escape).</summary>
        public static bool PausePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Escape);
#endif
            }
        }

        /// <summary>Toggle the developer overlay (F3).</summary>
        public static bool DebugTogglePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.F3);
#endif
            }
        }
    }
}

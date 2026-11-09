namespace FallenYard.Core
{
    /// <summary>
    /// Turns a held button into either "active while held" or "press to switch on, press
    /// again to switch off" (an accessibility option for sprint and tool use).
    /// </summary>
    public class HoldToggle
    {
        /// <summary>True: each press flips <see cref="Active"/>. False: <see cref="Active"/> follows the button.</summary>
        public bool ToggleMode { get; set; }

        public bool Active { get; private set; }

        public HoldToggle(bool toggleMode = false)
        {
            ToggleMode = toggleMode;
        }

        /// <summary>Feed this frame's button state; returns <see cref="Active"/>.</summary>
        public bool Update(bool held, bool pressedThisFrame)
        {
            if (!ToggleMode) Active = held;
            else if (pressedThisFrame) Active = !Active;
            return Active;
        }

        /// <summary>Switches off (menu opened, tool changed, player stopped).</summary>
        public void Reset() => Active = false;
    }
}

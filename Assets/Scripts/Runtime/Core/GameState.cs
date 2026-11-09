using UnityEngine;

namespace FallenYard.Core
{
    /// <summary>Global flags shared by gameplay systems.</summary>
    public static class GameState
    {
        /// <summary>True while a menu is open; player look/move and tools ignore input.</summary>
        public static bool InputBlocked { get; set; }

        /// <summary>
        /// Set by LevelFlow (WP7) just before it loads another yard, so the next scene drops the
        /// player straight into play instead of the title menu. UIManager reads and clears it.
        /// </summary>
        public static bool SkipTitleOnNextLoad { get; set; }

        /// <summary>Returns <see cref="SkipTitleOnNextLoad"/> and clears it.</summary>
        public static bool ConsumeSkipTitle()
        {
            bool skip = SkipTitleOnNextLoad;
            SkipTitleOnNextLoad = false;
            return skip;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => SkipTitleOnNextLoad = false;
    }
}

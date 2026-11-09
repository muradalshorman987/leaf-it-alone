using System;
using FallenYard.Core;

namespace FallenYard.UI
{
    /// <summary>
    /// Plain C# state machine for the high-level game phase (title, playing, paused,
    /// shop, yard complete). It is the only thing that raises
    /// <see cref="GameEvents.StateChanged"/>. <see cref="UIManager"/> owns one and
    /// applies the side effects (time scale, cursor, input blocking, screens).
    /// </summary>
    public class UIFlow
    {
        public GamePhase Phase { get; private set; }

        /// <summary>(previous, next). Raised before <see cref="GameEvents.StateChanged"/>.</summary>
        public event Action<GamePhase, GamePhase> PhaseChanged;

        public UIFlow(GamePhase initial = GamePhase.Boot)
        {
            Phase = initial;
        }

        /// <summary>True while gameplay input (look, move, tools) should be ignored.</summary>
        public static bool BlocksInput(GamePhase phase) => phase != GamePhase.Playing;

        /// <summary>True while the simulation should be frozen (Time.timeScale = 0).</summary>
        public static bool FreezesTime(GamePhase phase) => phase == GamePhase.Paused || phase == GamePhase.TitleMenu || phase == GamePhase.YardMap;

        /// <summary>True while the mouse cursor should be free for clicking menus.</summary>
        public static bool ShowsCursor(GamePhase phase) => phase != GamePhase.Playing;

        /// <summary>Moves to <paramref name="next"/>. Returns false if already there.</summary>
        public bool SetPhase(GamePhase next)
        {
            if (next == Phase) return false;
            var previous = Phase;
            Phase = next;
            PhaseChanged?.Invoke(previous, next);
            GameEvents.RaiseStateChanged(next);
            return true;
        }

        /// <summary>Title "Play" button: Boot/TitleMenu to Playing.</summary>
        public bool StartGame()
        {
            if (Phase != GamePhase.Boot && Phase != GamePhase.TitleMenu) return false;
            return SetPhase(GamePhase.Playing);
        }

        /// <summary>
        /// Escape key. Pauses while playing, and otherwise backs out of whatever is
        /// open (pause, shop, yard-complete card). Does nothing on the title menu.
        /// </summary>
        public bool TogglePause()
        {
            switch (Phase)
            {
                case GamePhase.Playing: return SetPhase(GamePhase.Paused);
                case GamePhase.Paused:
                case GamePhase.Shop:
                case GamePhase.YardComplete: return SetPhase(GamePhase.Playing);
                case GamePhase.YardMap: return CloseYardMap();
                default: return false;
            }
        }

        /// <summary>Tab key / shop buttons. Opens the shop from play, pause or the yard-complete card; closes it back to play.</summary>
        public bool ToggleShop()
        {
            switch (Phase)
            {
                case GamePhase.Playing:
                case GamePhase.Paused:
                case GamePhase.YardComplete: return SetPhase(GamePhase.Shop);
                case GamePhase.Shop: return SetPhase(GamePhase.Playing);
                default: return false;
            }
        }

        /// <summary>Resume button: back to play from any overlay.</summary>
        public bool Resume()
        {
            if (Phase == GamePhase.Boot || Phase == GamePhase.TitleMenu) return false;
            return SetPhase(GamePhase.Playing);
        }

        public bool ReturnToTitle() => SetPhase(GamePhase.TitleMenu);

        /// <summary>Where <see cref="CloseYardMap"/> goes back to.</summary>
        public GamePhase YardMapReturnPhase { get; private set; } = GamePhase.TitleMenu;

        /// <summary>Opens the Hollow Creek map from the title, play, pause or the yard-complete card.</summary>
        public bool OpenYardMap()
        {
            switch (Phase)
            {
                case GamePhase.TitleMenu:
                case GamePhase.Playing:
                case GamePhase.Paused:
                case GamePhase.YardComplete:
                    YardMapReturnPhase = Phase;
                    return SetPhase(GamePhase.YardMap);
                default:
                    return false;
            }
        }

        /// <summary>Back button / Escape on the map: returns to wherever it was opened from.</summary>
        public bool CloseYardMap()
        {
            if (Phase != GamePhase.YardMap) return false;
            return SetPhase(YardMapReturnPhase);
        }

        /// <summary>Shows the yard-complete card if the player is in the yard.</summary>
        public bool OnYardCompleted()
        {
            if (Phase != GamePhase.Playing && Phase != GamePhase.Shop) return false;
            return SetPhase(GamePhase.YardComplete);
        }
    }
}

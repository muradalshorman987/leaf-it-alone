namespace FallenYard.Core
{
    /// <summary>
    /// High-level phase of the game, broadcast through <see cref="GameEvents.StateChanged"/>.
    /// (Named GamePhase because <see cref="GameState"/> already holds global flags.)
    /// </summary>
    public enum GamePhase
    {
        Boot,
        TitleMenu,
        Playing,
        Paused,
        Shop,
        YardComplete,
        /// <summary>The Hollow Creek map: pick, open or travel to a yard (WP7).</summary>
        YardMap,
    }
}

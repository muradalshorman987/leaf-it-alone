namespace FallenYard.Core
{
    /// <summary>Global flags shared by gameplay systems.</summary>
    public static class GameState
    {
        /// <summary>True while a menu is open; player look/move and tools ignore input.</summary>
        public static bool InputBlocked { get; set; }
    }
}

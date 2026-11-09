namespace FallenYard.UI
{
    /// <summary>
    /// A screen layered over the regular menus (options, key rebinding). While one is open,
    /// <see cref="UIManager"/> sends Escape to it instead of pausing/resuming, and ignores the shop key.
    /// </summary>
    public interface IUIModal
    {
        bool IsOpen { get; }

        /// <summary>Escape pressed: cancel what's in progress or close.</summary>
        void HandleBack();
    }
}

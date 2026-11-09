namespace FallenYard.Save
{
    /// <summary>
    /// Implement on any system that keeps its own state and wants it in the save file
    /// (shared contract, owned by WP5). Register with <see cref="SaveRegistry"/> in
    /// OnEnable and unregister in OnDisable.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>Copy your live state into the data. Called right before every write.</summary>
        void CaptureState(SaveData data);

        /// <summary>Apply loaded data to your live state. Called after a load, and once when you register late.</summary>
        void RestoreState(SaveData data);
    }
}

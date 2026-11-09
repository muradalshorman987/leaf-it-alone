namespace FallenYard.Audio
{
    /// <summary>
    /// Every sound the game plays. Each cue has a procedural recipe in
    /// <see cref="ProceduralSounds"/>; an <see cref="AudioLibrary"/> can swap any of them for
    /// recorded clips later without code changes.
    /// Append new values at the end: the numbers are serialized in AudioLibrary assets.
    /// </summary>
    public enum SoundCue
    {
        // Tool loops
        RakeRustle = 0,
        BlowerLoop = 1,
        VacuumLoop = 2,
        MulcherLoop = 3,

        // Gameplay one-shots
        ToolEquip = 10,
        BagRustle = 11,
        CoinChime = 12,
        Purchase = 13,
        Upgrade = 14,
        CompostThud = 15,
        CompostReady = 16,
        RestoreSparkle = 17,
        YardComplete = 18,
        UiClick = 19,

        // Ambience
        WindBed = 30,
        WindGust = 31,
        Birdsong = 32,

        // Music
        MusicTitle = 40,
        MusicYard = 41,
    }
}

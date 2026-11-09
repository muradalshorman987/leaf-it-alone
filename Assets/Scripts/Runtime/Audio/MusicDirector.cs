using FallenYard.Core;
using UnityEngine;

namespace FallenYard.Audio
{
    public enum MusicTrack { None, Title, Yard }

    /// <summary>
    /// Music hooks: which track plays in each <see cref="GamePhase"/>, how much it ducks
    /// under menus, and a muffled low-pass while paused. A yard-complete stinger ducks the
    /// music for a few seconds. Pure logic; <see cref="AudioManager"/> applies the result.
    /// Other systems can force a track with <see cref="Override"/> (cutscenes, a later
    /// per-yard soundtrack) without touching this mapping.
    /// </summary>
    public sealed class MusicDirector
    {
        public const float OpenCutoff = 22000f;
        public const float MuffledCutoff = 1100f;

        public GamePhase Phase { get; private set; } = GamePhase.Boot;

        /// <summary>When set, wins over the phase mapping. Null = follow the phase.</summary>
        public MusicTrack? Override { get; set; }

        /// <summary>Seconds left on a stinger duck.</summary>
        public float StingerDuckTime { get; private set; }

        public float StingerDuckLevel { get; set; } = 0.25f;
        public float MenuDuckLevel { get; set; } = 0.55f;

        /// <summary>
        /// Scenes without a menu flow never leave Boot. After this many seconds in Boot the
        /// yard music starts anyway, so a bare test scene still has music.
        /// </summary>
        public float BootFallbackSeconds { get; set; } = 1f;

        float _bootTime;

        public void SetPhase(GamePhase phase) => Phase = phase;

        public void StartStinger(float seconds) => StingerDuckTime = Mathf.Max(StingerDuckTime, seconds);

        public void Tick(float dt)
        {
            dt = Mathf.Max(0f, dt);
            StingerDuckTime = Mathf.Max(0f, StingerDuckTime - dt);
            if (Phase == GamePhase.Boot) _bootTime += dt;
        }

        public MusicTrack Track
        {
            get
            {
                if (Override.HasValue) return Override.Value;
                if (Phase == GamePhase.Boot && _bootTime >= BootFallbackSeconds) return MusicTrack.Yard;
                return TrackFor(Phase);
            }
        }

        public static MusicTrack TrackFor(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.TitleMenu: return MusicTrack.Title;
                case GamePhase.Playing:
                case GamePhase.Paused:
                case GamePhase.Shop:
                case GamePhase.YardComplete: return MusicTrack.Yard;
                default: return MusicTrack.None;
            }
        }

        /// <summary>Music gain from menus and stingers (1 = full).</summary>
        public float Duck
        {
            get
            {
                float d = 1f;
                if (Phase == GamePhase.Paused || Phase == GamePhase.Shop) d = MenuDuckLevel;
                if (StingerDuckTime > 0f) d = Mathf.Min(d, StingerDuckLevel);
                return d;
            }
        }

        /// <summary>Low-pass cutoff for the music: muffled while paused, open otherwise.</summary>
        public float Cutoff => Phase == GamePhase.Paused ? MuffledCutoff : OpenCutoff;

        /// <summary>True when gameplay sounds (tool loops, birds) should keep running.</summary>
        public bool GameplayAudible => Phase == GamePhase.Playing || Phase == GamePhase.YardComplete || Phase == GamePhase.Boot;

        public static SoundCue? CueFor(MusicTrack track)
        {
            switch (track)
            {
                case MusicTrack.Title: return SoundCue.MusicTitle;
                case MusicTrack.Yard: return SoundCue.MusicYard;
                default: return null;
            }
        }
    }
}

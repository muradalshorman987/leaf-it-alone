using FallenYard.Settings;
using UnityEngine;

namespace FallenYard.Audio
{
    /// <summary>
    /// The four mix buses (Master, Music, Sfx, Ambience) as plain numbers.
    ///
    /// Master is applied once, by <see cref="SettingsManager"/> on the AudioListener, so it
    /// covers every sound in the game including ones that don't go through the AudioManager.
    /// These buses carry only the per-channel slider (and music ducking) on top of that.
    /// When an AudioMixer is assigned, the channel volume goes to the mixer instead and
    /// <see cref="SourceGain"/> returns 1 for the bus part.
    /// </summary>
    public sealed class AudioBuses
    {
        float _music = 0.7f, _sfx = 1f, _ambience = 0.8f, _master = 0.8f;

        /// <summary>True when channel volumes are applied by an AudioMixer instead of per source.</summary>
        public bool MixerDrivesChannels { get; set; }

        /// <summary>Extra music attenuation from menus and stingers, 0..1 (1 = no ducking).</summary>
        public float MusicDuck { get; set; } = 1f;

        public float Master => _master;

        public void Apply(GameSettings settings)
        {
            if (settings == null) return;
            _master = Mathf.Clamp01(settings.masterVolume);
            _music = Mathf.Clamp01(settings.musicVolume);
            _sfx = Mathf.Clamp01(settings.sfxVolume);
            _ambience = Mathf.Clamp01(settings.ambienceVolume);
        }

        /// <summary>The player's slider for a channel (Master returns the master slider).</summary>
        public float Channel(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Music: return _music;
                case AudioChannel.Sfx: return _sfx;
                case AudioChannel.Ambience: return _ambience;
                default: return _master;
            }
        }

        /// <summary>
        /// Final AudioSource.volume for a sound of <paramref name="level"/> on a channel, before
        /// the listener applies Master.
        /// </summary>
        public float SourceGain(AudioChannel channel, float level)
        {
            float bus = MixerDrivesChannels || channel == AudioChannel.Master ? 1f : Channel(channel);
            if (channel == AudioChannel.Music) bus *= Mathf.Clamp01(MusicDuck);
            return Mathf.Clamp01(level) * bus;
        }

        /// <summary>Mixer value in dB for a channel's exposed volume parameter.</summary>
        public float MixerDecibels(AudioChannel channel) => GameSettings.LinearToDecibels(Channel(channel));
    }
}

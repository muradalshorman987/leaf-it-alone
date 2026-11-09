using System;
using UnityEngine;

namespace FallenYard.Settings
{
    public enum AudioChannel { Master, Music, Sfx, Ambience }

    /// <summary>
    /// Player options. Stored in their own file (see <see cref="SettingsStore"/>) so that
    /// starting a new game or deleting the save keeps them. Serialized with JsonUtility.
    /// </summary>
    [Serializable]
    public class GameSettings
    {
        public const int CurrentVersion = 1;

        public const float MinSensitivity = 0.1f;
        public const float MaxSensitivity = 10f;
        public const float MinFov = 60f;
        public const float MaxFov = 110f;
        public const int MinFrameRate = 30;
        public const int MaxFrameRate = 360;

        public int version = CurrentVersion;

        [Header("Audio (0..1, linear)")]
        public float masterVolume = 0.8f;
        public float musicVolume = 0.7f;
        public float sfxVolume = 1f;
        public float ambienceVolume = 0.8f;
        public bool muteWhenUnfocused = true;

        [Header("Controls")]
        public float mouseSensitivity = 2f;
        public bool invertY;
        /// <summary>Input System binding overrides (InputActionAsset.SaveBindingOverridesAsJson). Empty = defaults.</summary>
        public string bindingOverridesJson = "";

        [Header("Camera")]
        public float fieldOfView = 75f;
        public bool headBob = true;

        [Header("Graphics")]
        /// <summary>Index into QualitySettings.names, or -1 to keep the project default.</summary>
        public int qualityLevel = -1;
        public FullScreenMode fullScreenMode = FullScreenMode.FullScreenWindow;
        /// <summary>0 x 0 means "keep the current resolution".</summary>
        public int resolutionWidth;
        public int resolutionHeight;
        public bool vSync = true;
        /// <summary>Used only when vSync is off. 0 = unlimited.</summary>
        public int targetFrameRate = 144;

        public float GetVolume(AudioChannel channel) => channel switch
        {
            AudioChannel.Master => masterVolume,
            AudioChannel.Music => musicVolume,
            AudioChannel.Sfx => sfxVolume,
            AudioChannel.Ambience => ambienceVolume,
            _ => 1f,
        };

        public void SetVolume(AudioChannel channel, float value)
        {
            value = Clamp01Safe(value, 1f);
            switch (channel)
            {
                case AudioChannel.Master: masterVolume = value; break;
                case AudioChannel.Music: musicVolume = value; break;
                case AudioChannel.Sfx: sfxVolume = value; break;
                case AudioChannel.Ambience: ambienceVolume = value; break;
            }
        }

        /// <summary>Master × channel, what a mixer group or AudioSource should actually use.</summary>
        public float GetEffectiveVolume(AudioChannel channel) =>
            channel == AudioChannel.Master ? masterVolume : masterVolume * GetVolume(channel);

        /// <summary>Converts a 0..1 slider value to decibels for an AudioMixer (-80 dB = silent).</summary>
        public static float LinearToDecibels(float linear) =>
            linear <= 0.0001f ? -80f : Mathf.Max(-80f, 20f * Mathf.Log10(linear));

        /// <summary>Clamps every value into a safe range and fixes values a hand-edited file could break. Returns this.</summary>
        public GameSettings Sanitize(int qualityLevelCount = int.MaxValue)
        {
            if (version < CurrentVersion) version = CurrentVersion;

            masterVolume = Clamp01Safe(masterVolume, 0.8f);
            musicVolume = Clamp01Safe(musicVolume, 0.7f);
            sfxVolume = Clamp01Safe(sfxVolume, 1f);
            ambienceVolume = Clamp01Safe(ambienceVolume, 0.8f);

            mouseSensitivity = ClampSafe(mouseSensitivity, MinSensitivity, MaxSensitivity, 2f);
            fieldOfView = ClampSafe(fieldOfView, MinFov, MaxFov, 75f);
            bindingOverridesJson ??= "";

            if (qualityLevel < -1 || qualityLevel >= qualityLevelCount) qualityLevel = -1;
            if (!Enum.IsDefined(typeof(FullScreenMode), fullScreenMode)) fullScreenMode = FullScreenMode.FullScreenWindow;
            if (resolutionWidth <= 0 || resolutionHeight <= 0) resolutionWidth = resolutionHeight = 0;
            if (targetFrameRate != 0) targetFrameRate = Mathf.Clamp(targetFrameRate, MinFrameRate, MaxFrameRate);
            return this;
        }

        public GameSettings Clone() => (GameSettings)MemberwiseClone();

        static float Clamp01Safe(float v, float fallback) => float.IsNaN(v) || float.IsInfinity(v) ? fallback : Mathf.Clamp01(v);

        static float ClampSafe(float v, float min, float max, float fallback) =>
            float.IsNaN(v) || float.IsInfinity(v) ? fallback : Mathf.Clamp(v, min, max);
    }
}

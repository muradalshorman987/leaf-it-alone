using System;
using FallenYard.Core;
using UnityEngine;

namespace FallenYard.Settings
{
    /// <summary>
    /// Owns the player's settings: loads them at startup, applies them to the engine,
    /// and saves them when they change. Put one next to the GameManager.
    ///
    /// Other systems read <see cref="Current"/> and listen to <see cref="Changed"/>:
    /// - the settings screen (WP4) edits a copy and calls <see cref="Apply"/>;
    /// - audio (WP8) maps <see cref="GameSettings.GetEffectiveVolume"/> to mixer groups;
    /// - the player (WP3) reads sensitivity, invert Y, FOV and head bob;
    /// - input rebinding loads <see cref="GameSettings.bindingOverridesJson"/>.
    /// </summary>
    [DefaultExecutionOrder(-150)] // after GameManager (-200), before gameplay
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        /// <summary>Raised after settings are applied. Static so listeners don't need the instance to exist yet.</summary>
        public static event Action<GameSettings> Changed;

        /// <summary>Live settings. Treat as read-only; to change them, edit a <see cref="GameSettings.Clone"/> and call <see cref="Apply"/>.</summary>
        public GameSettings Current { get; private set; }

        [Tooltip("Apply volume, quality, resolution and frame rate to the engine. Off in tests and in edit mode.")]
        public bool applyToEngine = true;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            bool firstRun = !SettingsStore.Exists;
            var loaded = SettingsStore.Load(QualitySettings.names.Length);

            // First run after updating from a build that kept sensitivity in the save file.
            if (firstRun && GameManager.Instance != null && GameManager.Instance.Data != null)
                loaded.mouseSensitivity = GameManager.Instance.Data.mouseSensitivity;

            Apply(loaded, save: firstRun);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnApplicationFocus(bool focused)
        {
            if (Current != null && Current.muteWhenUnfocused && CanTouchEngine)
                AudioListener.pause = !focused;
        }

        bool CanTouchEngine => applyToEngine && Application.isPlaying;

        /// <summary>Sanitizes, stores and applies new settings, then raises <see cref="Changed"/>.</summary>
        public void Apply(GameSettings settings, bool save = true)
        {
            if (settings == null) return;
            Current = settings.Clone().Sanitize(QualitySettings.names.Length);

            // Keep the legacy save field in sync for code that still reads it.
            if (GameManager.Instance != null && GameManager.Instance.Data != null)
                GameManager.Instance.Data.mouseSensitivity = Current.mouseSensitivity;

            KeyBindings.ApplyOverrides(Current.bindingOverridesJson);
            if (CanTouchEngine) ApplyToEngine(Current);
            if (save) SettingsStore.Save(Current);
            Changed?.Invoke(Current);
        }

        /// <summary>Convenience for a single slider: copies, edits, applies.</summary>
        public void Modify(Action<GameSettings> edit, bool save = true)
        {
            if (edit == null || Current == null) return;
            var copy = Current.Clone();
            edit(copy);
            Apply(copy, save);
        }

        public void ResetToDefaults() => Apply(new GameSettings());

        static void ApplyToEngine(GameSettings s)
        {
            // Until WP8 adds a mixer, master volume drives the listener directly.
            AudioListener.volume = s.masterVolume;

            if (s.qualityLevel >= 0 && s.qualityLevel != QualitySettings.GetQualityLevel())
                QualitySettings.SetQualityLevel(s.qualityLevel, true);

            QualitySettings.vSyncCount = s.vSync ? 1 : 0;
            Application.targetFrameRate = s.vSync || s.targetFrameRate == 0 ? -1 : s.targetFrameRate;

            if (!Application.isEditor)
            {
                if (s.resolutionWidth > 0 && s.resolutionHeight > 0)
                    Screen.SetResolution(s.resolutionWidth, s.resolutionHeight, s.fullScreenMode);
                else
                    Screen.fullScreenMode = s.fullScreenMode;
            }

            var cam = Camera.main;
            if (cam != null) cam.fieldOfView = s.fieldOfView;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Changed = null;

        /// <summary>For tests: drops every listener.</summary>
        public static void ClearListeners() => Changed = null;
    }
}

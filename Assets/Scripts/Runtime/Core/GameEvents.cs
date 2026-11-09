using System;
using UnityEngine;

namespace FallenYard.Core
{
    /// <summary>
    /// Shared event contract between work packages. Systems raise these instead of
    /// holding references to each other: UI, audio, VFX and tutorial only listen.
    ///
    /// Rules:
    /// - Subscribe in OnEnable, unsubscribe in OnDisable.
    /// - Only the owner listed on each event raises it (see docs/PLAN.md section 5).
    /// - Adding an event is fine; changing or removing one needs a PR reviewed by WP1.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>New coin total. Raised by GameManager (core).</summary>
        public static event Action<int> CoinsChanged;

        /// <summary>Fraction of the current yard that is clean, 0..1. Raised by Progression (WP5/WP2).</summary>
        public static event Action<float> YardCleanChanged;

        /// <summary>(leaves bagged, coins earned). Raised by the bag / vacuum tools (WP3).</summary>
        public static event Action<int, int> PileBagged;

        /// <summary>Tool id that is now in hand. Raised by ToolController (WP3).</summary>
        public static event Action<string> ToolEquipped;

        /// <summary>(tool id, new level). Raised by UpgradeManager (WP5).</summary>
        public static event Action<string, int> ToolUpgraded;

        /// <summary>Tool effort 0..1 for audio/VFX (blower throttle, rake drag). Raised by tools (WP3).</summary>
        public static event Action<float> ToolThrottle;

        /// <summary>(fill of the next batch 0..1, fertilizer batches ready). Raised by the compost bin (WP3).</summary>
        public static event Action<float, int> CompostChanged;

        /// <summary>How restored the yard is, 0..1, after spreading fertilizer. Raised by the compost bin (WP3).</summary>
        public static event Action<float> YardRestorationChanged;

        /// <summary>(world direction, strength). Raised by the wind system (WP2).</summary>
        public static event Action<Vector3, float> WindGust;

        /// <summary>Yard id, raised once per yard completion. Raised by GameManager (core).</summary>
        public static event Action<string> YardCompleted;

        /// <summary>New high-level phase. Raised by whoever drives menus / flow (WP4).</summary>
        public static event Action<GamePhase> StateChanged;

        /// <summary>(world position, leaves) where a pile was just bagged, for the bag poof. Raised by BagInteractor (WP3) next to PileBagged.</summary>
        public static event Action<Vector3, int> LeavesBaggedAt;

        /// <summary>Weather id now settling in ("clear", "breezy", "overcast", "blustery"). Raised by WeatherSystem (WP10).</summary>
        public static event Action<string> WeatherChanged;

        /// <summary>Yard id, once the yard's scene is set up and its leaves are about to spawn. Raised by YardLoader (WP7).</summary>
        public static event Action<string> YardLoaded;

        /// <summary>Yard id the player picked on the yard map or yard-complete card. Raised by UI (WP4); LevelFlow (WP7) travels there.</summary>
        public static event Action<string> YardTravelRequested;

        public static void RaiseCoinsChanged(int total) => CoinsChanged?.Invoke(total);
        public static void RaiseYardCleanChanged(float fraction) => YardCleanChanged?.Invoke(Mathf.Clamp01(fraction));
        public static void RaisePileBagged(int leaves, int coinsEarned) => PileBagged?.Invoke(leaves, coinsEarned);
        public static void RaiseToolEquipped(string toolId) => ToolEquipped?.Invoke(toolId);
        public static void RaiseToolUpgraded(string toolId, int level) => ToolUpgraded?.Invoke(toolId, level);
        public static void RaiseToolThrottle(float throttle) => ToolThrottle?.Invoke(Mathf.Clamp01(throttle));
        public static void RaiseCompostChanged(float fill, int readyBatches) => CompostChanged?.Invoke(Mathf.Clamp01(fill), Mathf.Max(0, readyBatches));
        public static void RaiseYardRestorationChanged(float restoration) => YardRestorationChanged?.Invoke(Mathf.Clamp01(restoration));
        public static void RaiseWindGust(Vector3 direction, float strength) => WindGust?.Invoke(direction, strength);
        public static void RaiseYardCompleted(string yardId) => YardCompleted?.Invoke(yardId);
        public static void RaiseStateChanged(GamePhase phase) => StateChanged?.Invoke(phase);
        public static void RaiseLeavesBaggedAt(Vector3 position, int leaves) => LeavesBaggedAt?.Invoke(position, Mathf.Max(0, leaves));
        public static void RaiseWeatherChanged(string weatherId) => WeatherChanged?.Invoke(weatherId);
        public static void RaiseYardLoaded(string yardId) => YardLoaded?.Invoke(yardId);
        public static void RaiseYardTravelRequested(string yardId) => YardTravelRequested?.Invoke(yardId);

        /// <summary>
        /// Drops every subscriber. Runs automatically before each play session so stale
        /// listeners don't survive when "Enter Play Mode Options" skips the domain reload.
        /// Tests call it in SetUp/TearDown.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ClearAllSubscribers()
        {
            CoinsChanged = null;
            YardCleanChanged = null;
            PileBagged = null;
            ToolEquipped = null;
            ToolUpgraded = null;
            ToolThrottle = null;
            CompostChanged = null;
            YardRestorationChanged = null;
            WindGust = null;
            YardCompleted = null;
            StateChanged = null;
            LeavesBaggedAt = null;
            WeatherChanged = null;
            YardLoaded = null;
            YardTravelRequested = null;
        }
    }
}

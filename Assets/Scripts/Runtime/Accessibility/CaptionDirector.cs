using System;
using FallenYard.Core;
using UnityEngine;

namespace FallenYard.Accessibility
{
    /// <summary>
    /// Decides which sounds get a caption. Listens to <see cref="GameEvents"/>:
    /// <see cref="GameEvents.AudioCuePlayed"/> from the audio system, plus the gameplay events that are
    /// always audible (wind, bagging, coins, tool loops, upgrades, yard complete), so captions work
    /// before and alongside WP8's audio manager. Cue ids are shared, so a sound captioned both ways
    /// shows one line.
    ///
    /// Plain C#: the host calls <see cref="Bind"/>/<see cref="Unbind"/> and ticks <see cref="Queue"/>.
    /// </summary>
    public class CaptionDirector
    {
        public CaptionQueue Queue { get; }

        /// <summary>Captions are only shown while this is true (the player's "Captions" option).</summary>
        public bool Enabled { get; set; }

        [Tooltip("Gusts weaker than this are not captioned.")]
        public float minGustStrength = 0.35f;

        [Tooltip("Tool loops are captioned once throttle passes this.")]
        public float toolThrottleThreshold = 0.25f;

        /// <summary>Optional: the listener's forward direction, to say where a gust comes from.</summary>
        public Func<Vector3?> ListenerForward;

        string _toolId;
        int _lastReadyBatches;
        bool _bound;

        public CaptionDirector(CaptionQueue queue = null)
        {
            Queue = queue ?? new CaptionQueue();
        }

        public void Bind()
        {
            if (_bound) return;
            _bound = true;
            GameEvents.AudioCuePlayed += OnSoundCue;
            GameEvents.WindGust += OnWindGust;
            GameEvents.PileBagged += OnPileBagged;
            GameEvents.ToolEquipped += OnToolEquipped;
            GameEvents.ToolThrottle += OnToolThrottle;
            GameEvents.ToolUpgraded += OnToolUpgraded;
            GameEvents.YardCompleted += OnYardCompleted;
            GameEvents.CompostChanged += OnCompostChanged;
        }

        public void Unbind()
        {
            if (!_bound) return;
            _bound = false;
            GameEvents.AudioCuePlayed -= OnSoundCue;
            GameEvents.WindGust -= OnWindGust;
            GameEvents.PileBagged -= OnPileBagged;
            GameEvents.ToolEquipped -= OnToolEquipped;
            GameEvents.ToolThrottle -= OnToolThrottle;
            GameEvents.ToolUpgraded -= OnToolUpgraded;
            GameEvents.YardCompleted -= OnYardCompleted;
            GameEvents.CompostChanged -= OnCompostChanged;
        }

        /// <summary>Shows the catalog caption for a cue (if captions are on and the cue is known).</summary>
        public bool ShowCue(string cueId, string textOverride = null)
        {
            if (!Enabled || !CaptionCatalog.TryGet(cueId, out var info)) return false;
            return Queue.Show(cueId, textOverride ?? info.Text, info.Seconds) != null;
        }

        void OnSoundCue(string cueId) => ShowCue(cueId);

        void OnWindGust(Vector3 direction, float strength)
        {
            if (strength < minGustStrength) return;
            string where = null;
            var forward = ListenerForward?.Invoke();
            if (forward.HasValue) where = GustSide(forward.Value, direction);
            ShowCue(CaptionCatalog.WindGust, where != null ? $"[Wind gusts {where}]" : null);
        }

        void OnPileBagged(int leaves, int coins)
        {
            if (leaves > 0) ShowCue(CaptionCatalog.BagRustle);
            if (coins > 0) ShowCue(CaptionCatalog.CoinChime);
        }

        void OnToolEquipped(string toolId) => _toolId = toolId;

        void OnToolThrottle(float throttle)
        {
            if (throttle < toolThrottleThreshold) return;
            string cue = CaptionCatalog.ToolLoopCue(_toolId);
            if (cue != null) ShowCue(cue);
        }

        void OnToolUpgraded(string toolId, int level) => ShowCue(CaptionCatalog.UpgradeChime);

        void OnYardCompleted(string yardId) => ShowCue(CaptionCatalog.YardComplete);

        void OnCompostChanged(float fill, int readyBatches)
        {
            if (readyBatches > _lastReadyBatches) ShowCue(CaptionCatalog.CompostReady);
            _lastReadyBatches = readyBatches;
        }

        /// <summary>
        /// Where a gust blowing along <paramref name="windDirection"/> comes from, relative to a
        /// listener facing <paramref name="listenerForward"/>: "from the left", "from behind"...
        /// Null when either direction is flat zero.
        /// </summary>
        public static string GustSide(Vector3 listenerForward, Vector3 windDirection)
        {
            var f = new Vector2(listenerForward.x, listenerForward.z);
            var from = new Vector2(-windDirection.x, -windDirection.z); // wind comes from the opposite side
            if (f.sqrMagnitude < 1e-6f || from.sqrMagnitude < 1e-6f) return null;
            float angle = Vector2.SignedAngle(f, from); // + = counter-clockwise = to the left
            float abs = Mathf.Abs(angle);
            if (abs <= 45f) return "ahead";
            if (abs >= 135f) return "from behind";
            return angle > 0f ? "from the left" : "from the right";
        }
    }
}

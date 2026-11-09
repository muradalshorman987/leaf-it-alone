using FallenYard.Core;
using UnityEngine;

namespace FallenYard.Save
{
    /// <summary>
    /// Autosave hooks. Saves through GameManager when gameplay milestones happen
    /// (yard completed, tool upgraded, pause / title menu, bagging), plus a periodic
    /// fallback, and counts play time. Put one next to the GameManager.
    ///
    /// It takes over GameManager's own timer (sets its autosaveInterval to 0) so the
    /// two never write twice in a row.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class Autosaver : MonoBehaviour
    {
        [Tooltip("Seconds between periodic saves. 0 = only on events.")]
        public float periodicInterval = 60f;

        [Tooltip("Minimum seconds between event-driven saves, so bursts of events cause one write.")]
        public float minGap = 5f;

        [Tooltip("Save after bagging piles (debounced by Min Gap).")]
        public bool saveOnPileBagged = true;

        public AutosaveScheduler Scheduler { get; private set; }

        /// <summary>Reason for the most recent autosave, for debugging and tests.</summary>
        public string LastSaveReason { get; private set; }

        public int SaveCount { get; private set; }

        GamePhase _phase = GamePhase.Playing;

        void Awake()
        {
            Scheduler = new AutosaveScheduler(periodicInterval, minGap, Time.unscaledTimeAsDouble);
        }

        void Start()
        {
            if (GameManager.Instance != null) GameManager.Instance.autosaveInterval = 0f;
        }

        void OnEnable()
        {
            GameEvents.YardCompleted += OnYardCompleted;
            GameEvents.ToolUpgraded += OnToolUpgraded;
            GameEvents.PileBagged += OnPileBagged;
            GameEvents.StateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            GameEvents.YardCompleted -= OnYardCompleted;
            GameEvents.ToolUpgraded -= OnToolUpgraded;
            GameEvents.PileBagged -= OnPileBagged;
            GameEvents.StateChanged -= OnStateChanged;
        }

        void Update() => Tick(Time.unscaledTimeAsDouble, Time.unscaledDeltaTime);

        /// <summary>One frame of autosave logic. Public so tests can drive it with a fake clock.</summary>
        public void Tick(double now, float deltaTime)
        {
            var gm = GameManager.Instance;
            if (gm == null || Scheduler == null) return;

            if (_phase == GamePhase.Playing) gm.Data.playTimeSeconds += deltaTime;

            if (Scheduler.ShouldSave(now, out string reason)) SaveNow(reason, now);
        }

        /// <summary>Saves immediately, whatever the schedule says.</summary>
        public void SaveNow(string reason, double now)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            gm.Save();
            Scheduler?.MarkSaved(now);
            LastSaveReason = reason;
            SaveCount++;
        }

        public void Request(string reason, bool urgent = false) => Scheduler?.Request(reason, urgent);

        void OnYardCompleted(string yardId) => Request("yard completed", true);
        void OnToolUpgraded(string toolId, int level) => Request("tool upgraded");

        void OnPileBagged(int leaves, int coins)
        {
            if (saveOnPileBagged) Request("pile bagged");
        }

        void OnStateChanged(GamePhase phase)
        {
            _phase = phase;
            if (phase == GamePhase.Paused || phase == GamePhase.TitleMenu || phase == GamePhase.YardComplete)
                Request("phase " + phase, true);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) SaveNow("app paused", Time.unscaledTimeAsDouble);
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) Request("focus lost", true);
        }
    }
}

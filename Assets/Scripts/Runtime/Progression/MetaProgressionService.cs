using System;
using System.Collections.Generic;
using FallenYard.Core;
using FallenYard.Save;
using FallenYard.Tools;
using UnityEngine;

namespace FallenYard.Progression
{
    /// <summary>
    /// Connects <see cref="MetaProgression"/> to the running game: listens for yard
    /// completion and clean progress, unlocks yards and tool rewards, and exposes
    /// yard status for the yard select screen (WP4). Put one next to the GameManager.
    ///
    /// It also saves the scene's <see cref="CompostBin"/> (restoration, fertilizer,
    /// pending leaves) into the current yard's record and puts it back on load.
    /// </summary>
    [DefaultExecutionOrder(-120)]
    public class MetaProgressionService : MonoBehaviour, ISaveable
    {
        public static MetaProgressionService Instance { get; private set; }

        public YardUnlockTable unlockTable;

        [Tooltip("Yard being played. Taken from the scene's YardProgress if left empty.")]
        public string currentYardId;

        /// <summary>A yard became playable (free unlock or purchase).</summary>
        public event Action<string> YardUnlocked;

        /// <summary>A yard's requirements are now met but it costs coins.</summary>
        public event Action<string> YardPurchasable;

        /// <summary>A tool was unlocked as a yard reward.</summary>
        public event Action<string> ToolGranted;

        /// <summary>Full result of the latest completion, for a results screen.</summary>
        public CompletionResult LastCompletion { get; private set; }

        int _lastCoins;

        static SaveData Data => GameManager.Instance != null ? GameManager.Instance.Data : null;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            if (string.IsNullOrEmpty(currentYardId))
            {
                var yard = FindFirstObjectByType<YardProgress>();
                if (yard != null) currentYardId = yard.yardId;
            }
            Initialize();
            RestoreCompost(Data);
        }

        void OnEnable()
        {
            GameEvents.YardCompleted += HandleYardCompleted;
            GameEvents.YardCleanChanged += HandleYardCleanChanged;
            GameEvents.CoinsChanged += HandleCoinsChanged;
            GameEvents.YardRestorationChanged += HandleRestorationChanged;
            SaveRegistry.Register(this);
        }

        void OnDisable()
        {
            GameEvents.YardCompleted -= HandleYardCompleted;
            GameEvents.YardCleanChanged -= HandleYardCleanChanged;
            GameEvents.CoinsChanged -= HandleCoinsChanged;
            GameEvents.YardRestorationChanged -= HandleRestorationChanged;
            SaveRegistry.Unregister(this);
        }

        /// <summary>Opens default and already-earned yards in the save. Called on Start; tests call it directly.</summary>
        public void Initialize()
        {
            var data = Data;
            if (data == null) return;
            _lastCoins = data.coins;
            foreach (var y in MetaProgression.RefreshUnlocks(data, unlockTable)) YardUnlocked?.Invoke(y);
            if (!string.IsNullOrEmpty(currentYardId)) data.lastYardId = currentYardId;
        }

        public YardStatus GetStatus(string yardId) =>
            Data != null ? MetaProgression.GetStatus(Data, unlockTable, yardId) : YardStatus.Locked;

        public bool IsPlayable(string yardId) => Data != null && MetaProgression.IsPlayable(Data, unlockTable, yardId);

        public YardRecord GetRecord(string yardId) => Data?.GetYardRecord(yardId, false);

        public float OverallCompletion => Data != null ? MetaProgression.OverallCompletion(Data, unlockTable) : 0f;

        /// <summary>Every yard in the table with its status, in table order.</summary>
        public List<(YardUnlockRule rule, YardStatus status)> GetAllYards()
        {
            var list = new List<(YardUnlockRule, YardStatus)>();
            if (unlockTable == null) return list;
            foreach (var rule in unlockTable.yards)
                if (rule != null) list.Add((rule, GetStatus(rule.yardId)));
            return list;
        }

        public bool TryPurchaseYard(string yardId)
        {
            var gm = GameManager.Instance;
            if (gm == null) return false;
            if (!MetaProgression.TryPurchase(gm.Data, unlockTable, yardId, gm.TrySpend)) return false;
            gm.Save();
            YardUnlocked?.Invoke(yardId);
            return true;
        }

        void HandleYardCleanChanged(float fraction)
        {
            var data = Data;
            if (data != null && !string.IsNullOrEmpty(currentYardId))
                MetaProgression.RecordProgress(data, currentYardId, fraction);
        }

        /// <summary>Saved restoration of a yard, 0..1.</summary>
        public float GetRestoration(string yardId) => Data != null ? MetaProgression.GetRestoration(Data, yardId) : 0f;

        void HandleRestorationChanged(float restoration)
        {
            var data = Data;
            if (data != null) MetaProgression.RecordRestoration(data, currentYardId, restoration);
        }

        /// <summary>ISaveable: copies the compost bin into the current yard's record before each write.</summary>
        public void CaptureState(SaveData data)
        {
            var bin = CompostBin.Instance;
            if (data == null || bin == null || string.IsNullOrEmpty(currentYardId)) return;
            var m = bin.Model;
            var r = data.GetYardRecord(currentYardId, true);
            r.restoration = m.Restoration;
            r.compostPendingLeaves = m.PendingLeaves;
            r.compostReadyBatches = m.ReadyBatches;
            r.compostIsComposting = m.IsComposting;
            r.compostProgress = m.CompostProgress;
        }

        /// <summary>ISaveable: puts the current yard's compost state back into the bin.</summary>
        public void RestoreState(SaveData data) => RestoreCompost(data);

        void RestoreCompost(SaveData data)
        {
            var bin = CompostBin.Instance;
            if (data == null || bin == null || string.IsNullOrEmpty(currentYardId)) return;
            var r = data.GetYardRecord(currentYardId, false);
            if (r == null) return;
            bin.ApplySavedState(r.restoration, r.compostPendingLeaves, r.compostReadyBatches, r.compostIsComposting, r.compostProgress);
        }

        void HandleCoinsChanged(int total)
        {
            var data = Data;
            if (data == null) return;
            int delta = total - _lastCoins;
            _lastCoins = total;
            if (delta <= 0) return; // spending or a reset
            data.lifetimeCoinsEarned += delta;
            if (!string.IsNullOrEmpty(currentYardId))
                data.GetYardRecord(currentYardId, true).coinsEarned += delta;
        }

        void HandleYardCompleted(string yardId)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Data == null) return;

            // Coins were already counted per yard by HandleCoinsChanged.
            LastCompletion = MetaProgression.RecordCompletion(gm.Data, unlockTable, yardId);

            foreach (var t in LastCompletion.grantedTools) ToolGranted?.Invoke(t);
            foreach (var y in LastCompletion.newlyUnlockedYards) YardUnlocked?.Invoke(y);
            foreach (var y in LastCompletion.newlyPurchasableYards) YardPurchasable?.Invoke(y);

            // GameManager saved before raising the event, so write again to keep the unlocks.
            gm.Save();
        }
    }
}

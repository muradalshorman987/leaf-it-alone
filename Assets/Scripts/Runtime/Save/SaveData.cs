using System;
using System.Collections.Generic;

namespace FallenYard.Save
{
    [Serializable]
    public class ToolSaveEntry
    {
        public string toolId;
        public int level;
        public bool unlocked;
    }

    /// <summary>Per-yard history used by meta-progression and the yard select screen. Added in v2.</summary>
    [Serializable]
    public class YardRecord
    {
        public string yardId;
        public int timesCompleted;
        public float bestCleanFraction;
        public int coinsEarned;

        // v3: compost bin state, so restoration and unspread fertilizer survive a reload.
        /// <summary>How restored the yard is, 0..1 (the compost bin's YardRestorationChanged value).</summary>
        public float restoration;
        public int compostPendingLeaves;
        public int compostReadyBatches;
        public bool compostIsComposting;
        public float compostProgress;
    }

    /// <summary>First-yard tutorial and hint progress. Added in v4 (WP11).</summary>
    [Serializable]
    public class TutorialSave
    {
        /// <summary>Index of the current Tutorial.TutorialStep (the last value means done).</summary>
        public int step;
        /// <summary>Player chose "Skip tutorial".</summary>
        public bool skipped;
        /// <summary>One entry per time a hint was shown, so hints can be limited per save.</summary>
        public List<string> hintsShown = new List<string>();

        public int TimesShown(string hintId)
        {
            int n = 0;
            if (hintsShown == null) return 0;
            foreach (var h in hintsShown)
                if (h == hintId) n++;
            return n;
        }
    }

    /// <summary>
    /// Everything that persists between sessions. Serialized with JsonUtility.
    ///
    /// Changing the format: bump <see cref="CurrentVersion"/>, add the new fields with
    /// safe defaults, and add one step to <see cref="SaveMigrator"/> that upgrades the
    /// previous version. Never rename or remove a field without a migration step.
    /// Player options (volume, graphics, keys) live in the separate settings file,
    /// so "new game" never resets them.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>
        /// v1: coins, tools, completed yards. v2: yard unlocks, yard records, play time, timestamps.
        /// v3: per-yard restoration and compost bin state in <see cref="YardRecord"/>.
        /// v4: tutorial and hint progress in <see cref="tutorial"/>.
        /// </summary>
        public const int CurrentVersion = 4;

        public int version = CurrentVersion;

        // v1
        public int coins;
        public long totalLeavesBagged;
        public List<ToolSaveEntry> tools = new List<ToolSaveEntry>();
        public List<string> completedYards = new List<string>();

        /// <summary>
        /// Legacy (v1). The real value now lives in the settings file; SettingsManager keeps
        /// this in sync so code that still reads it (FirstPersonController) gets the right value.
        /// </summary>
        public float mouseSensitivity = 2f;

        // v2
        public List<string> unlockedYards = new List<string>();
        public List<YardRecord> yardRecords = new List<YardRecord>();
        public string lastYardId = "";
        public long lifetimeCoinsEarned;
        public double playTimeSeconds;
        /// <summary>UTC ticks of the last successful write, 0 if never saved.</summary>
        public long savedAtUtcTicks;

        // v4
        public TutorialSave tutorial = new TutorialSave();

        public ToolSaveEntry GetTool(string toolId, bool create)
        {
            foreach (var t in tools)
                if (t.toolId == toolId) return t;
            if (!create) return null;
            var entry = new ToolSaveEntry { toolId = toolId };
            tools.Add(entry);
            return entry;
        }

        public YardRecord GetYardRecord(string yardId, bool create)
        {
            foreach (var r in yardRecords)
                if (r.yardId == yardId) return r;
            if (!create) return null;
            var record = new YardRecord { yardId = yardId };
            yardRecords.Add(record);
            return record;
        }

        public bool IsYardUnlocked(string yardId) => unlockedYards.Contains(yardId);

        /// <summary>Adds the yard to the unlocked list. Returns false if it was already there.</summary>
        public bool UnlockYard(string yardId)
        {
            if (string.IsNullOrEmpty(yardId) || unlockedYards.Contains(yardId)) return false;
            unlockedYards.Add(yardId);
            return true;
        }

        /// <summary>Upgrades this object in place to <see cref="CurrentVersion"/>. See <see cref="SaveMigrator"/>.</summary>
        public SaveData Migrate()
        {
            SaveMigrator.Migrate(this);
            return this;
        }
    }
}

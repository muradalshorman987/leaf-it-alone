using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallenYard.Save
{
    /// <summary>
    /// Upgrades save data one version at a time: v0 → v1 → v2 → ... → <see cref="SaveData.CurrentVersion"/>.
    /// Each step only needs to know about the version right before it.
    /// </summary>
    public static class SaveMigrator
    {
        /// <summary>Step that upgrades data from version <c>key</c> to <c>key + 1</c>.</summary>
        static readonly Dictionary<int, Action<SaveData>> Steps = new Dictionary<int, Action<SaveData>>
        {
            { 0, V0ToV1 },
            { 1, V1ToV2 },
            { 2, V2ToV3 },
        };

        /// <summary>
        /// Migrates <paramref name="data"/> in place and returns the version it started at.
        /// Data from a newer build (version above current) is left at its version so the
        /// caller can tell; unknown fields from that build are already lost by JsonUtility,
        /// which is why SaveSystem backs the file up first.
        /// </summary>
        public static int Migrate(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            int from = data.version;
            if (data.version < 0) data.version = 0;

            while (data.version < SaveData.CurrentVersion)
            {
                if (!Steps.TryGetValue(data.version, out var step))
                    throw new InvalidOperationException($"No save migration from version {data.version}.");
                step(data);
                data.version++;
            }

            Normalize(data);
            return from;
        }

        /// <summary>True when every version below current has a step. Guarded by a test.</summary>
        public static bool HasCompleteChain()
        {
            for (int v = 0; v < SaveData.CurrentVersion; v++)
                if (!Steps.ContainsKey(v)) return false;
            return true;
        }

        /// <summary>Repairs nulls and impossible values that any version could contain.</summary>
        static void Normalize(SaveData d)
        {
            d.tools ??= new List<ToolSaveEntry>();
            d.completedYards ??= new List<string>();
            d.unlockedYards ??= new List<string>();
            d.yardRecords ??= new List<YardRecord>();
            d.lastYardId ??= "";

            d.tools.RemoveAll(t => t == null || string.IsNullOrEmpty(t.toolId));
            d.completedYards.RemoveAll(string.IsNullOrEmpty);
            d.unlockedYards.RemoveAll(string.IsNullOrEmpty);
            d.yardRecords.RemoveAll(r => r == null || string.IsNullOrEmpty(r.yardId));

            d.coins = Mathf.Max(0, d.coins);
            if (d.totalLeavesBagged < 0) d.totalLeavesBagged = 0;
            if (d.lifetimeCoinsEarned < 0) d.lifetimeCoinsEarned = 0;
            if (double.IsNaN(d.playTimeSeconds) || d.playTimeSeconds < 0) d.playTimeSeconds = 0;
            foreach (var t in d.tools) t.level = Mathf.Max(0, t.level);
            foreach (var r in d.yardRecords)
            {
                r.timesCompleted = Mathf.Max(0, r.timesCompleted);
                r.bestCleanFraction = float.IsNaN(r.bestCleanFraction) ? 0f : Mathf.Clamp01(r.bestCleanFraction);
                r.coinsEarned = Mathf.Max(0, r.coinsEarned);
                r.restoration = float.IsNaN(r.restoration) ? 0f : Mathf.Clamp01(r.restoration);
                r.compostPendingLeaves = Mathf.Max(0, r.compostPendingLeaves);
                r.compostReadyBatches = Mathf.Max(0, r.compostReadyBatches);
                r.compostProgress = float.IsNaN(r.compostProgress) ? 0f : Mathf.Clamp01(r.compostProgress);
            }

            // A yard you've finished is always one you can play.
            foreach (var y in d.completedYards)
                if (!d.unlockedYards.Contains(y)) d.unlockedYards.Add(y);
        }

        /// <summary>Pre-release files: lists could be missing.</summary>
        static void V0ToV1(SaveData d)
        {
            d.tools ??= new List<ToolSaveEntry>();
            d.completedYards ??= new List<string>();
        }

        /// <summary>
        /// v3 adds restoration and compost state to yard records. Older saves never had a
        /// compost bin, so the defaults (0, empty bin) are right and there is nothing to convert.
        /// </summary>
        static void V2ToV3(SaveData d)
        {
        }

        /// <summary>v2 adds yard unlocks and per-yard records. Seed them from the completed list.</summary>
        static void V1ToV2(SaveData d)
        {
            d.unlockedYards ??= new List<string>();
            d.yardRecords ??= new List<YardRecord>();
            if (d.completedYards == null) return;
            foreach (var yardId in d.completedYards)
            {
                if (string.IsNullOrEmpty(yardId)) continue;
                d.UnlockYard(yardId);
                var record = d.GetYardRecord(yardId, true);
                record.timesCompleted = Mathf.Max(1, record.timesCompleted);
                record.bestCleanFraction = Mathf.Max(record.bestCleanFraction, 1f);
            }
            if (string.IsNullOrEmpty(d.lastYardId) && d.completedYards.Count > 0)
                d.lastYardId = d.completedYards[d.completedYards.Count - 1];
        }
    }
}

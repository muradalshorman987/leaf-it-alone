using System;
using System.Collections.Generic;
using FallenYard.Save;
using UnityEngine;

namespace FallenYard.Progression
{
    public enum YardStatus
    {
        /// <summary>Requirements not met yet.</summary>
        Locked,
        /// <summary>Requirements met, waiting for the player to pay the unlock cost.</summary>
        Purchasable,
        /// <summary>Playable.</summary>
        Unlocked,
        /// <summary>Playable and finished at least once.</summary>
        Completed,
    }

    /// <summary>What changed when a yard was completed.</summary>
    public class CompletionResult
    {
        public bool firstCompletion;
        public readonly List<string> newlyUnlockedYards = new List<string>();
        public readonly List<string> newlyPurchasableYards = new List<string>();
        public readonly List<string> grantedTools = new List<string>();
    }

    /// <summary>
    /// Rules for progress that carries across yards: which yards are open, what finishing
    /// one gives you, per-yard records. Pure logic over <see cref="SaveData"/> so it can be
    /// tested without a scene; <see cref="MetaProgressionService"/> wires it to the game.
    /// </summary>
    public static class MetaProgression
    {
        public static bool IsCompleted(SaveData data, string yardId) => data.completedYards.Contains(yardId);

        public static bool RequirementsMet(SaveData data, YardUnlockRule rule)
        {
            if (rule == null) return false;
            if (rule.unlockedByDefault) return true;
            if (data.completedYards.Count < rule.requiredCompletedCount) return false;
            if (rule.requiredYards != null)
                foreach (var r in rule.requiredYards)
                    if (!string.IsNullOrEmpty(r) && !data.completedYards.Contains(r)) return false;
            return true;
        }

        public static YardStatus GetStatus(SaveData data, YardUnlockTable table, string yardId)
        {
            if (IsCompleted(data, yardId)) return YardStatus.Completed;
            if (data.IsYardUnlocked(yardId)) return YardStatus.Unlocked;
            var rule = table != null ? table.Get(yardId) : null;
            if (rule == null) return YardStatus.Locked;
            if (!RequirementsMet(data, rule)) return YardStatus.Locked;
            return rule.unlockedByDefault || rule.unlockCost <= 0 ? YardStatus.Unlocked : YardStatus.Purchasable;
        }

        public static bool IsPlayable(SaveData data, YardUnlockTable table, string yardId)
        {
            var s = GetStatus(data, table, yardId);
            return s == YardStatus.Unlocked || s == YardStatus.Completed;
        }

        /// <summary>
        /// Writes every free yard whose requirements are met into the save. Returns the ones
        /// that were newly added. Safe to call any time (on load, after completion).
        /// </summary>
        public static List<string> RefreshUnlocks(SaveData data, YardUnlockTable table)
        {
            var added = new List<string>();
            if (table == null) return added;
            foreach (var rule in table.yards)
            {
                if (rule == null || string.IsNullOrEmpty(rule.yardId) || data.IsYardUnlocked(rule.yardId)) continue;
                if (!RequirementsMet(data, rule)) continue;
                if (rule.unlockedByDefault || rule.unlockCost <= 0)
                {
                    data.UnlockYard(rule.yardId);
                    added.Add(rule.yardId);
                }
            }
            return added;
        }

        /// <summary>Yards whose requirements are met but still cost coins.</summary>
        public static List<string> PurchasableYards(SaveData data, YardUnlockTable table)
        {
            var list = new List<string>();
            if (table == null) return list;
            foreach (var rule in table.yards)
                if (rule != null && GetStatus(data, table, rule.yardId) == YardStatus.Purchasable)
                    list.Add(rule.yardId);
            return list;
        }

        /// <summary>
        /// Buys a purchasable yard. <paramref name="trySpend"/> takes the cost and returns
        /// whether the player could pay (GameManager.TrySpend in the game).
        /// </summary>
        public static bool TryPurchase(SaveData data, YardUnlockTable table, string yardId, Func<int, bool> trySpend)
        {
            if (GetStatus(data, table, yardId) != YardStatus.Purchasable) return false;
            int cost = table.Get(yardId).unlockCost;
            if (trySpend == null || !trySpend(cost)) return false;
            data.UnlockYard(yardId);
            return true;
        }

        /// <summary>Keeps the best clean fraction for the yard and remembers it as the last one played.</summary>
        public static void RecordProgress(SaveData data, string yardId, float cleanFraction)
        {
            if (string.IsNullOrEmpty(yardId)) return;
            var record = data.GetYardRecord(yardId, true);
            record.bestCleanFraction = Mathf.Max(record.bestCleanFraction, Mathf.Clamp01(cleanFraction));
            data.lastYardId = yardId;
        }

        /// <summary>
        /// Applies a yard completion: bumps the record, grants first-clear tool rewards and
        /// opens any yards that now qualify. Works whether or not the yard id is already in
        /// <see cref="SaveData.completedYards"/> (GameManager adds it before raising the event).
        /// </summary>
        public static CompletionResult RecordCompletion(SaveData data, YardUnlockTable table, string yardId, int coinsEarned = 0)
        {
            var result = new CompletionResult();
            if (string.IsNullOrEmpty(yardId)) return result;

            var record = data.GetYardRecord(yardId, true);
            result.firstCompletion = record.timesCompleted == 0;
            record.timesCompleted++;
            record.bestCleanFraction = Mathf.Max(record.bestCleanFraction, 1f);
            record.coinsEarned += Mathf.Max(0, coinsEarned);
            data.lastYardId = yardId;

            if (!data.completedYards.Contains(yardId)) data.completedYards.Add(yardId);
            data.UnlockYard(yardId);

            var rule = table != null ? table.Get(yardId) : null;
            if (result.firstCompletion && rule?.rewardToolIds != null)
            {
                foreach (var toolId in rule.rewardToolIds)
                {
                    if (string.IsNullOrEmpty(toolId)) continue;
                    var tool = data.GetTool(toolId, true);
                    if (tool.unlocked) continue;
                    tool.unlocked = true;
                    result.grantedTools.Add(toolId);
                }
            }

            var purchasableBefore = new HashSet<string>(PurchasableYardsIgnoring(data, table, yardId));
            result.newlyUnlockedYards.AddRange(RefreshUnlocks(data, table));
            foreach (var y in PurchasableYards(data, table))
                if (!purchasableBefore.Contains(y)) result.newlyPurchasableYards.Add(y);
            return result;
        }

        /// <summary>Purchasable yards as they were before <paramref name="justCompleted"/> counted.</summary>
        static List<string> PurchasableYardsIgnoring(SaveData data, YardUnlockTable table, string justCompleted)
        {
            int index = data.completedYards.IndexOf(justCompleted);
            if (index < 0) return PurchasableYards(data, table);
            data.completedYards.RemoveAt(index);
            var list = PurchasableYards(data, table);
            data.completedYards.Insert(index, justCompleted);
            return list;
        }

        /// <summary>0..1 share of the table's yards completed, for a "town restored" meter.</summary>
        public static float OverallCompletion(SaveData data, YardUnlockTable table)
        {
            if (table == null || table.yards.Count == 0) return 0f;
            int done = 0;
            foreach (var y in table.yards)
                if (y != null && data.completedYards.Contains(y.yardId)) done++;
            return (float)done / table.yards.Count;
        }
    }
}

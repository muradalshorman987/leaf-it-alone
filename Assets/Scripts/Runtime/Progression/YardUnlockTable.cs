using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallenYard.Progression
{
    /// <summary>How one yard becomes available, and what finishing it gives you.</summary>
    [Serializable]
    public class YardUnlockRule
    {
        [Tooltip("Must match YardProgress.yardId (and the WP7 YardDefinition id).")]
        public string yardId = "yard_01";
        public string displayName = "Starter Lawn";

        [Tooltip("Playable from the start.")]
        public bool unlockedByDefault;

        [Tooltip("Every one of these yards must be completed first.")]
        public string[] requiredYards = Array.Empty<string>();

        [Tooltip("At least this many yards (any) must be completed first.")]
        public int requiredCompletedCount;

        [Tooltip("Coins to buy once requirements are met. 0 = unlocks by itself.")]
        public int unlockCost;

        [Tooltip("Tools unlocked for free the first time this yard is completed.")]
        public string[] rewardToolIds = Array.Empty<string>();
    }

    /// <summary>Data asset listing every yard in play order with its unlock rules.</summary>
    [CreateAssetMenu(menuName = "Fallen Yard/Yard Unlock Table", fileName = "YardUnlockTable")]
    public class YardUnlockTable : ScriptableObject
    {
        public List<YardUnlockRule> yards = new List<YardUnlockRule>();

        public YardUnlockRule Get(string yardId)
        {
            foreach (var y in yards)
                if (y != null && y.yardId == yardId) return y;
            return null;
        }

        /// <summary>Problems a designer should fix (duplicates, unknown or circular prerequisites, no starting yard).</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var ids = new HashSet<string>();
            bool anyDefault = false;
            foreach (var y in yards)
            {
                if (y == null || string.IsNullOrEmpty(y.yardId))
                {
                    problems.Add("A yard has no id.");
                    continue;
                }
                if (!ids.Add(y.yardId)) problems.Add($"Duplicate yard id '{y.yardId}'.");
                anyDefault |= y.unlockedByDefault;
                if (y.unlockCost < 0) problems.Add($"'{y.yardId}' has a negative unlock cost.");
                if (y.requiredCompletedCount >= yards.Count) problems.Add($"'{y.yardId}' needs more completed yards than exist.");
            }
            foreach (var y in yards)
            {
                if (y?.requiredYards == null) continue;
                foreach (var r in y.requiredYards)
                {
                    if (r == y.yardId) problems.Add($"'{y.yardId}' requires itself.");
                    else if (!ids.Contains(r)) problems.Add($"'{y.yardId}' requires unknown yard '{r}'.");
                }
            }
            if (yards.Count > 0 && !anyDefault) problems.Add("No yard is unlocked by default, so the game can't start.");
            return problems;
        }
    }
}

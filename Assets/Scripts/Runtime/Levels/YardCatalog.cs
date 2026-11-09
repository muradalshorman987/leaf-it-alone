using System.Collections.Generic;
using FallenYard.Progression;
using UnityEngine;

namespace FallenYard.Levels
{
    /// <summary>Every yard in play order plus the unlock table that gates them. One per game.</summary>
    [CreateAssetMenu(menuName = "Fallen Yard/Yard Catalog", fileName = "YardCatalog")]
    public class YardCatalog : ScriptableObject
    {
        [Tooltip("Yards in play order. The first one is where a new game starts.")]
        public List<YardDefinition> yards = new List<YardDefinition>();

        [Tooltip("Who can play which yard. Its ids must match the yards above.")]
        public YardUnlockTable unlockTable;

        public int Count => yards.Count;

        public YardDefinition Get(string yardId)
        {
            if (string.IsNullOrEmpty(yardId)) return null;
            foreach (var y in yards)
                if (y != null && y.yardId == yardId) return y;
            return null;
        }

        public int IndexOf(string yardId)
        {
            for (int i = 0; i < yards.Count; i++)
                if (yards[i] != null && yards[i].yardId == yardId) return i;
            return -1;
        }

        /// <summary>The yard after <paramref name="yardId"/> in play order, or null if it is the last.</summary>
        public YardDefinition NextAfter(string yardId)
        {
            int i = IndexOf(yardId);
            if (i < 0) return null;
            for (int j = i + 1; j < yards.Count; j++)
                if (yards[j] != null) return yards[j];
            return null;
        }

        public YardDefinition FindByScene(string sceneName)
        {
            foreach (var y in yards)
                if (y != null && y.sceneName == sceneName) return y;
            return null;
        }

        /// <summary>Problems across the whole catalog: bad yards, duplicate ids or scenes, table mismatches.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            if (yards.Count == 0) problems.Add("The catalog has no yards.");
            var ids = new HashSet<string>();
            var scenes = new HashSet<string>();
            foreach (var y in yards)
            {
                if (y == null)
                {
                    problems.Add("The catalog has an empty slot.");
                    continue;
                }
                problems.AddRange(y.Validate());
                if (!string.IsNullOrEmpty(y.yardId) && !ids.Add(y.yardId)) problems.Add($"Duplicate yard id '{y.yardId}'.");
                if (!string.IsNullOrEmpty(y.sceneName) && !scenes.Add(y.sceneName)) problems.Add($"Two yards use scene '{y.sceneName}'.");
                if (unlockTable != null && unlockTable.Get(y.yardId) == null) problems.Add($"'{y.yardId}' has no unlock rule, so it can never be played.");
            }
            if (unlockTable == null) problems.Add("The catalog has no unlock table.");
            else
            {
                problems.AddRange(unlockTable.Validate());
                foreach (var rule in unlockTable.yards)
                    if (rule != null && Get(rule.yardId) == null) problems.Add($"Unlock rule '{rule.yardId}' has no yard in the catalog.");
            }
            return problems;
        }
    }
}

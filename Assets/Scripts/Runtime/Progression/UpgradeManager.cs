using System;
using System.Collections.Generic;
using FallenYard.Core;
using UnityEngine;

namespace FallenYard.Progression
{
    /// <summary>
    /// Upgrade hooks for tools. Tools ask GetMultiplier(toolId, stat); the shop
    /// calls TryUnlock / TryUpgrade. Levels are stored in the save file.
    /// </summary>
    public class UpgradeManager : MonoBehaviour
    {
        public static UpgradeManager Instance { get; private set; }

        public ToolUpgradeDefinition[] definitions = Array.Empty<ToolUpgradeDefinition>();

        /// <summary>(toolId, newLevel). Level 0 = base tool.</summary>
        public event Action<string, int> OnToolUpgraded;
        public event Action<string> OnToolUnlocked;

        readonly Dictionary<string, ToolUpgradeDefinition> _byId = new Dictionary<string, ToolUpgradeDefinition>();

        void Awake()
        {
            Instance = this;
            foreach (var d in definitions)
                if (d != null) _byId[d.toolId] = d;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public ToolUpgradeDefinition GetDefinition(string toolId) =>
            _byId.TryGetValue(toolId, out var d) ? d : null;

        static Save.SaveData Data => GameManager.Instance != null ? GameManager.Instance.Data : null;

        public int GetLevel(string toolId) => Data?.GetTool(toolId, false)?.level ?? 0;

        public int MaxLevel(string toolId) => GetDefinition(toolId)?.levels.Length ?? 0;

        public bool IsUnlocked(string toolId)
        {
            var def = GetDefinition(toolId);
            if (def == null || def.unlockedByDefault) return true;
            return Data?.GetTool(toolId, false)?.unlocked ?? false;
        }

        public float GetMultiplier(string toolId, UpgradeStat stat)
        {
            var def = GetDefinition(toolId);
            int level = GetLevel(toolId);
            if (def == null || level <= 0 || def.levels.Length == 0) return 1f;
            return def.levels[Mathf.Min(level, def.levels.Length) - 1].Get(stat);
        }

        /// <summary>Cost of the next upgrade, or -1 if maxed / unknown.</summary>
        public int NextUpgradeCost(string toolId)
        {
            var def = GetDefinition(toolId);
            int level = GetLevel(toolId);
            if (def == null || level >= def.levels.Length) return -1;
            return def.levels[level].cost;
        }

        public bool TryUnlock(string toolId)
        {
            var def = GetDefinition(toolId);
            if (def == null || IsUnlocked(toolId) || GameManager.Instance == null) return false;
            if (!GameManager.Instance.TrySpend(def.unlockCost)) return false;
            Data.GetTool(toolId, true).unlocked = true;
            GameManager.Instance.Save();
            OnToolUnlocked?.Invoke(toolId);
            return true;
        }

        public bool TryUpgrade(string toolId)
        {
            int cost = NextUpgradeCost(toolId);
            if (cost < 0 || !IsUnlocked(toolId) || GameManager.Instance == null) return false;
            if (!GameManager.Instance.TrySpend(cost)) return false;
            var entry = Data.GetTool(toolId, true);
            entry.level++;
            GameManager.Instance.Save();
            OnToolUpgraded?.Invoke(toolId, entry.level);
            GameEvents.RaiseToolUpgraded(toolId, entry.level);
            return true;
        }
    }
}

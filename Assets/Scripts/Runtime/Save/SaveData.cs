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

    /// <summary>Everything that persists between sessions. Serialized with JsonUtility.</summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int coins;
        public long totalLeavesBagged;
        public List<ToolSaveEntry> tools = new List<ToolSaveEntry>();
        public List<string> completedYards = new List<string>();
        public float mouseSensitivity = 2f;

        public ToolSaveEntry GetTool(string toolId, bool create)
        {
            foreach (var t in tools)
                if (t.toolId == toolId) return t;
            if (!create) return null;
            var entry = new ToolSaveEntry { toolId = toolId };
            tools.Add(entry);
            return entry;
        }

        /// <summary>Upgrade older save files here when the format changes.</summary>
        public SaveData Migrate()
        {
            tools ??= new List<ToolSaveEntry>();
            completedYards ??= new List<string>();
            version = CurrentVersion;
            return this;
        }
    }
}

using System;
using UnityEngine;

namespace FallenYard.Progression
{
    public enum UpgradeStat { Radius, Strength, Range, Capacity }

    /// <summary>Data asset describing how a tool is unlocked and upgraded.</summary>
    [CreateAssetMenu(menuName = "Fallen Yard/Tool Upgrade Definition", fileName = "ToolUpgrade")]
    public class ToolUpgradeDefinition : ScriptableObject
    {
        [Tooltip("Must match LeafTool.toolId / BagInteractor.toolId.")]
        public string toolId = "rake";
        public string displayName = "Rake";
        public bool unlockedByDefault = true;
        public int unlockCost;
        public UpgradeLevel[] levels = Array.Empty<UpgradeLevel>();

        [Serializable]
        public class UpgradeLevel
        {
            public int cost = 50;
            public float radiusMultiplier = 1f;
            public float strengthMultiplier = 1f;
            public float rangeMultiplier = 1f;
            public float capacityMultiplier = 1f;

            public float Get(UpgradeStat stat) => stat switch
            {
                UpgradeStat.Radius => radiusMultiplier,
                UpgradeStat.Strength => strengthMultiplier,
                UpgradeStat.Range => rangeMultiplier,
                UpgradeStat.Capacity => capacityMultiplier,
                _ => 1f,
            };
        }
    }
}

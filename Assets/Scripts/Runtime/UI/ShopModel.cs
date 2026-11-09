using System.Collections.Generic;
using System.Globalization;
using FallenYard.Progression;
using UnityEngine;

namespace FallenYard.UI
{
    public enum ShopAction { Unlock, Upgrade, Maxed }

    /// <summary>One line in the upgrade shop, already turned into display text.</summary>
    public struct ShopRow
    {
        public string ToolId;
        public string Title;
        public string LevelText;
        /// <summary>What the next purchase does, e.g. "+30% reach, +15% power".</summary>
        public string EffectText;
        public string ButtonText;
        public ShopAction Action;
        /// <summary>Price of the next purchase, or -1 when maxed.</summary>
        public int Cost;
        public bool CanAfford;

        public bool Interactable => Action != ShopAction.Maxed && CanAfford;
    }

    /// <summary>
    /// Turns <see cref="UpgradeManager"/> data into shop rows and performs purchases.
    /// No UI Toolkit types here, so it is easy to test.
    /// </summary>
    public static class ShopModel
    {
        public static List<ShopRow> BuildRows(UpgradeManager upgrades, int coins)
        {
            var rows = new List<ShopRow>();
            if (upgrades == null || upgrades.definitions == null) return rows;

            foreach (var def in upgrades.definitions)
            {
                if (def == null || string.IsNullOrEmpty(def.toolId)) continue;
                rows.Add(BuildRow(upgrades, def, coins));
            }
            return rows;
        }

        public static ShopRow BuildRow(UpgradeManager upgrades, ToolUpgradeDefinition def, int coins)
        {
            string id = def.toolId;
            int level = upgrades.GetLevel(id);
            int max = def.levels != null ? def.levels.Length : 0;
            var row = new ShopRow
            {
                ToolId = id,
                Title = HudFormat.ToolName(id, _ => def.displayName),
                LevelText = HudFormat.Level(level, max),
            };

            if (!upgrades.IsUnlocked(id))
            {
                row.Action = ShopAction.Unlock;
                row.Cost = Mathf.Max(0, def.unlockCost);
                row.ButtonText = $"Unlock  {HudFormat.Coins(row.Cost)}";
                row.EffectText = "Adds this tool to your belt";
            }
            else
            {
                int cost = upgrades.NextUpgradeCost(id);
                if (cost < 0)
                {
                    row.Action = ShopAction.Maxed;
                    row.Cost = -1;
                    row.ButtonText = "Maxed";
                    row.EffectText = "Fully upgraded";
                }
                else
                {
                    row.Action = ShopAction.Upgrade;
                    row.Cost = cost;
                    row.ButtonText = $"Upgrade  {HudFormat.Coins(cost)}";
                    row.EffectText = DescribeNextLevel(def, level);
                }
            }

            row.CanAfford = row.Action != ShopAction.Maxed && coins >= row.Cost;
            return row;
        }

        /// <summary>Buys whatever the row offers. Returns true if coins were spent.</summary>
        public static bool TryBuy(UpgradeManager upgrades, ShopRow row)
        {
            if (upgrades == null || !row.Interactable) return false;
            return row.Action == ShopAction.Unlock ? upgrades.TryUnlock(row.ToolId) : upgrades.TryUpgrade(row.ToolId);
        }

        static readonly (UpgradeStat stat, string label)[] StatLabels =
        {
            (UpgradeStat.Radius, "reach"),
            (UpgradeStat.Strength, "power"),
            (UpgradeStat.Range, "range"),
            (UpgradeStat.Capacity, "capacity"),
        };

        /// <summary>
        /// Compares the next level's multipliers with the current ones, e.g.
        /// "+30% reach, +15% power". Empty when nothing changes.
        /// </summary>
        public static string DescribeNextLevel(ToolUpgradeDefinition def, int currentLevel)
        {
            if (def == null || def.levels == null || currentLevel < 0 || currentLevel >= def.levels.Length) return "";
            var next = def.levels[currentLevel];
            var current = currentLevel > 0 ? def.levels[currentLevel - 1] : null;

            var parts = new List<string>();
            foreach (var (stat, label) in StatLabels)
            {
                float from = current != null ? current.Get(stat) : 1f;
                float to = next.Get(stat);
                if (from <= 0f) continue;
                int pct = Mathf.RoundToInt((to / from - 1f) * 100f);
                if (pct == 0) continue;
                parts.Add((pct > 0 ? "+" : "") + pct.ToString(CultureInfo.InvariantCulture) + "% " + label);
            }
            return string.Join(", ", parts);
        }
    }
}

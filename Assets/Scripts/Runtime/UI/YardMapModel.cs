using System.Collections.Generic;
using FallenYard.Levels;
using FallenYard.Progression;
using FallenYard.Save;
using UnityEngine;

namespace FallenYard.UI
{
    public enum YardMapAction
    {
        /// <summary>Travel to the yard.</summary>
        Play,
        /// <summary>The yard you're standing in; the button just closes the map.</summary>
        Current,
        /// <summary>Requirements met, pay coins to open it.</summary>
        Buy,
        /// <summary>Not yet; the button is disabled.</summary>
        Locked,
    }

    /// <summary>One yard on the Hollow Creek map.</summary>
    public class YardMapRow
    {
        public string YardId;
        public string Title;
        public string Neighbour;
        public string Note;
        public YardStatus Status;
        public YardMapAction Action;
        /// <summary>"Cleaned", "Best 62%", "Needs Corner Lot", ...</summary>
        public string StatusText;
        public string ButtonText;
        public int Cost;
        public bool CanAfford;
        public bool Interactable;
    }

    /// <summary>Builds the yard map rows from the catalog and the save. Pure logic, no UI Toolkit.</summary>
    public static class YardMapModel
    {
        public static List<YardMapRow> BuildRows(YardCatalog catalog, SaveData data, string currentYardId, int coins)
        {
            var rows = new List<YardMapRow>();
            if (catalog == null) return rows;
            data ??= new SaveData();
            foreach (var y in catalog.yards)
            {
                if (y == null) continue;
                var status = YardFlow.Status(data, catalog, y.yardId);
                var rule = catalog.unlockTable != null ? catalog.unlockTable.Get(y.yardId) : null;
                var row = new YardMapRow
                {
                    YardId = y.yardId,
                    Title = y.displayName,
                    Neighbour = y.neighbourName,
                    Note = y.neighbourNote,
                    Status = status,
                    Cost = rule != null ? Mathf.Max(0, rule.unlockCost) : 0,
                };
                bool here = y.yardId == currentYardId;
                var record = data.GetYardRecord(y.yardId, false);
                int best = record != null ? Mathf.RoundToInt(record.bestCleanFraction * 100f) : 0;

                switch (status)
                {
                    case YardStatus.Completed:
                    case YardStatus.Unlocked:
                        row.Action = here ? YardMapAction.Current : YardMapAction.Play;
                        row.StatusText = status == YardStatus.Completed ? "Cleaned" : best > 0 ? $"Best {best}%" : "New";
                        row.ButtonText = here ? "You're here" : status == YardStatus.Completed ? "Visit" : "Go rake";
                        row.Interactable = true;
                        row.CanAfford = true;
                        break;
                    case YardStatus.Purchasable:
                        row.Action = YardMapAction.Buy;
                        row.CanAfford = coins >= row.Cost;
                        row.StatusText = "Ready to open";
                        row.ButtonText = $"Open for {HudFormat.Coins(row.Cost)}";
                        row.Interactable = row.CanAfford;
                        break;
                    default:
                        row.Action = YardMapAction.Locked;
                        row.StatusText = LockedReason(catalog, data, rule);
                        row.ButtonText = "Locked";
                        row.Interactable = false;
                        break;
                }
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>"Needs Orchard", "Clean 2 more yards", or "Locked".</summary>
        public static string LockedReason(YardCatalog catalog, SaveData data, YardUnlockRule rule)
        {
            if (rule == null) return "Not open yet";
            var missing = new List<string>();
            if (rule.requiredYards != null)
                foreach (var id in rule.requiredYards)
                    if (!string.IsNullOrEmpty(id) && !data.completedYards.Contains(id))
                    {
                        var y = catalog.Get(id);
                        missing.Add(y != null ? y.displayName : id);
                    }
            if (missing.Count > 0) return "Clean " + string.Join(" and ", missing) + " first";
            int more = rule.requiredCompletedCount - data.completedYards.Count;
            if (more > 0) return more == 1 ? "Clean 1 more yard" : $"Clean {more} more yards";
            return "Not open yet";
        }

        /// <summary>Text for the yard-complete card's travel button, or null if there's nowhere new to go.</summary>
        public static string NextYardButtonText(YardDefinition next, YardStatus status, int cost)
        {
            if (next == null) return null;
            switch (status)
            {
                case YardStatus.Unlocked:
                case YardStatus.Completed:
                    return $"On to {next.displayName}";
                case YardStatus.Purchasable:
                    return $"Open {next.displayName} ({HudFormat.Coins(cost)})";
                default:
                    return null;
            }
        }
    }
}

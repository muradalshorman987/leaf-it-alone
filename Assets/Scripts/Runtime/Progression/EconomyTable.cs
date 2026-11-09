using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FallenYard.Progression
{
    /// <summary>One price in the game: a tool unlock, a tool upgrade level, or a yard purchase.</summary>
    public class EconomyRow
    {
        public const string ToolUnlock = "tool_unlock";
        public const string ToolLevel = "tool_level";
        public const string YardUnlock = "yard_unlock";

        public string kind;
        public string id;
        /// <summary>Upgrade level (1-based) for tool_level rows, 0 otherwise.</summary>
        public int level;
        public int cost;
        public float radius = 1f;
        public float strength = 1f;
        public float range = 1f;
        public float capacity = 1f;

        public string Key => $"{kind}:{id}:{level}";
    }

    /// <summary>
    /// The economy tuning table: every price and upgrade multiplier from the
    /// <see cref="ToolUpgradeDefinition"/> assets and the <see cref="YardUnlockTable"/>, as rows that
    /// round-trip through CSV so prices can be balanced in a spreadsheet. The "leaves" column is
    /// derived (cost / coins per leaf) to show how much raking each purchase takes; it is ignored on
    /// import. Editor menu: Fallen Yard > Economy.
    /// </summary>
    public static class EconomyTable
    {
        public const string Header = "kind,id,level,cost,radius,strength,range,capacity,leaves";
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static List<EconomyRow> Capture(IEnumerable<ToolUpgradeDefinition> tools, YardUnlockTable yards)
        {
            var rows = new List<EconomyRow>();
            if (tools != null)
            {
                foreach (var def in tools)
                {
                    if (def == null || string.IsNullOrEmpty(def.toolId)) continue;
                    if (!def.unlockedByDefault || def.unlockCost > 0)
                        rows.Add(new EconomyRow { kind = EconomyRow.ToolUnlock, id = def.toolId, cost = def.unlockCost });
                    if (def.levels == null) continue;
                    for (int i = 0; i < def.levels.Length; i++)
                    {
                        var l = def.levels[i];
                        if (l == null) continue;
                        rows.Add(new EconomyRow
                        {
                            kind = EconomyRow.ToolLevel, id = def.toolId, level = i + 1, cost = l.cost,
                            radius = l.radiusMultiplier, strength = l.strengthMultiplier,
                            range = l.rangeMultiplier, capacity = l.capacityMultiplier,
                        });
                    }
                }
            }
            if (yards != null && yards.yards != null)
            {
                foreach (var y in yards.yards)
                    if (y != null && !string.IsNullOrEmpty(y.yardId))
                        rows.Add(new EconomyRow { kind = EconomyRow.YardUnlock, id = y.yardId, cost = y.unlockCost });
            }
            return rows;
        }

        /// <summary>Leaves the player has to bag to earn <paramref name="cost"/> coins.</summary>
        public static int LeavesFor(int cost, float coinsPerLeaf) =>
            coinsPerLeaf <= 0f ? 0 : (int)Math.Ceiling(Math.Max(0, cost) / coinsPerLeaf);

        public static string ToCsv(IEnumerable<EconomyRow> rows, float coinsPerLeaf)
        {
            var sb = new StringBuilder();
            sb.Append(Header).Append('\n');
            foreach (var r in rows)
            {
                sb.Append(r.kind).Append(',')
                  .Append(Escape(r.id)).Append(',')
                  .Append(r.level.ToString(Inv)).Append(',')
                  .Append(r.cost.ToString(Inv)).Append(',')
                  .Append(F(r.radius)).Append(',')
                  .Append(F(r.strength)).Append(',')
                  .Append(F(r.range)).Append(',')
                  .Append(F(r.capacity)).Append(',')
                  .Append(LeavesFor(r.cost, coinsPerLeaf).ToString(Inv)).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Parses CSV written by <see cref="ToCsv"/> (or edited in a spreadsheet). Bad lines go to <paramref name="errors"/>.</summary>
        public static List<EconomyRow> Parse(string csv, List<string> errors = null)
        {
            var rows = new List<EconomyRow>();
            if (string.IsNullOrEmpty(csv)) return rows;
            var lines = csv.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n].Trim();
                if (line.Length == 0 || line.StartsWith("kind,", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal)) continue;
                var f = line.Split(',');
                if (f.Length < 4)
                {
                    errors?.Add($"line {n + 1}: expected at least 4 columns");
                    continue;
                }
                var row = new EconomyRow { kind = f[0].Trim(), id = f[1].Trim().Trim('"') };
                if (row.kind != EconomyRow.ToolUnlock && row.kind != EconomyRow.ToolLevel && row.kind != EconomyRow.YardUnlock)
                {
                    errors?.Add($"line {n + 1}: unknown kind '{row.kind}'");
                    continue;
                }
                if (!int.TryParse(f[2].Trim().Length == 0 ? "0" : f[2].Trim(), NumberStyles.Integer, Inv, out row.level) ||
                    !int.TryParse(f[3].Trim(), NumberStyles.Integer, Inv, out row.cost) || row.cost < 0)
                {
                    errors?.Add($"line {n + 1}: level/cost must be whole numbers (cost >= 0)");
                    continue;
                }
                row.radius = Mult(f, 4);
                row.strength = Mult(f, 5);
                row.range = Mult(f, 6);
                row.capacity = Mult(f, 7);
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>
        /// Writes prices and multipliers back into the assets. Rows that match nothing are reported;
        /// levels are never added or removed (do that in the asset). Returns the number of values changed.
        /// </summary>
        public static int Apply(IEnumerable<EconomyRow> rows, IEnumerable<ToolUpgradeDefinition> tools, YardUnlockTable yards, List<string> warnings = null)
        {
            var byTool = new Dictionary<string, ToolUpgradeDefinition>();
            if (tools != null)
                foreach (var d in tools)
                    if (d != null && !string.IsNullOrEmpty(d.toolId)) byTool[d.toolId] = d;

            int changed = 0;
            foreach (var r in rows)
            {
                switch (r.kind)
                {
                    case EconomyRow.ToolUnlock:
                        if (byTool.TryGetValue(r.id, out var u)) changed += Set(ref u.unlockCost, r.cost);
                        else warnings?.Add($"No tool '{r.id}'");
                        break;
                    case EconomyRow.ToolLevel:
                        if (!byTool.TryGetValue(r.id, out var t)) { warnings?.Add($"No tool '{r.id}'"); break; }
                        if (t.levels == null || r.level < 1 || r.level > t.levels.Length || t.levels[r.level - 1] == null)
                        {
                            warnings?.Add($"{r.id} has no level {r.level}");
                            break;
                        }
                        var l = t.levels[r.level - 1];
                        changed += Set(ref l.cost, r.cost);
                        changed += Set(ref l.radiusMultiplier, r.radius);
                        changed += Set(ref l.strengthMultiplier, r.strength);
                        changed += Set(ref l.rangeMultiplier, r.range);
                        changed += Set(ref l.capacityMultiplier, r.capacity);
                        break;
                    case EconomyRow.YardUnlock:
                        var rule = yards != null ? yards.Get(r.id) : null;
                        if (rule != null) changed += Set(ref rule.unlockCost, r.cost);
                        else warnings?.Add($"No yard '{r.id}'");
                        break;
                }
            }
            return changed;
        }

        static int Set(ref int field, int value)
        {
            if (field == value) return 0;
            field = value;
            return 1;
        }

        static int Set(ref float field, float value)
        {
            if (Math.Abs(field - value) < 1e-5f) return 0;
            field = value;
            return 1;
        }

        static float Mult(string[] f, int i)
        {
            if (i >= f.Length) return 1f;
            string s = f[i].Trim();
            return s.Length > 0 && float.TryParse(s, NumberStyles.Float, Inv, out float v) && v > 0f && !float.IsInfinity(v) ? v : 1f;
        }

        static string F(float v) => v.ToString("0.###", Inv);

        static string Escape(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace(",", "_");
    }
}

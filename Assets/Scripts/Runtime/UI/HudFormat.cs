using System;
using System.Globalization;
using System.Text;
using FallenYard.Core;
using UnityEngine;

namespace FallenYard.UI
{
    /// <summary>Pure text formatting for the HUD and shop, kept apart so it can be unit tested.</summary>
    public static class HudFormat
    {
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary>1234 -> "1,234". Negative totals show as 0.</summary>
        public static string Coins(int coins) => Mathf.Max(0, coins).ToString("N0", Invariant);

        /// <summary>
        /// Whole percent, rounded down so the HUD never shows 100% before the yard
        /// really is spotless. 0.999 -> "99%", 1 -> "100%".
        /// </summary>
        public static string CleanPercent(float fraction) => CleanPercentValue(fraction).ToString(Invariant) + "%";

        public static int CleanPercentValue(float fraction)
        {
            if (float.IsNaN(fraction)) return 0;
            // The epsilon stops 0.29f from flooring to 28 because of float error.
            return Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(fraction) * 100f + 1e-4f), 0, 100);
        }

        /// <summary>
        /// Display name for a tool id. Uses <paramref name="lookup"/> (e.g. the upgrade
        /// definition's displayName) when it returns text, else "leaf_blower" -> "Leaf Blower".
        /// </summary>
        public static string ToolName(string toolId, Func<string, string> lookup = null)
        {
            if (string.IsNullOrEmpty(toolId)) return "";
            string named = lookup?.Invoke(toolId);
            if (!string.IsNullOrEmpty(named)) return named;

            var sb = new StringBuilder(toolId.Length);
            bool upper = true;
            foreach (char c in toolId)
            {
                if (c == '_' || c == '-' || c == ' ')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                    upper = true;
                    continue;
                }
                sb.Append(upper ? char.ToUpperInvariant(c) : c);
                upper = false;
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>"Lv 1 / 3", or "Lv 0" for a tool with no upgrade levels.</summary>
        public static string Level(int level, int maxLevel) =>
            maxLevel > 0 ? $"Lv {level} / {maxLevel}" : $"Lv {level}";

        /// <summary>Toast text for a bagged pile.</summary>
        public static string PileBagged(int leaves, int coins)
        {
            string text = $"Bagged {leaves.ToString("N0", Invariant)} {(leaves == 1 ? "leaf" : "leaves")}";
            return coins > 0 ? $"{text}  +{coins.ToString("N0", Invariant)} coins" : text;
        }
    
        /// <summary>
        /// The HUD's key reminder line for the current bindings. With the default keys this is
        /// exactly <see cref="HudView.HintText"/>.
        /// </summary>
        public static string KeyHints(KeyBindings bindings)
        {
            bindings ??= new KeyBindings();
            return $"{ToolKeys(bindings)} tools   {bindings.Label(GameAction.Interact)} bag   {bindings.Label(GameAction.Shop)} shop   Esc pause";
        }

        /// <summary>"1-4" for the default digits, otherwise the four keys joined with "/".</summary>
        public static string ToolKeys(KeyBindings bindings)
        {
            bindings ??= new KeyBindings();
            string a = bindings.Get(GameAction.Tool1), b = bindings.Get(GameAction.Tool2),
                c = bindings.Get(GameAction.Tool3), d = bindings.Get(GameAction.Tool4);
            if (a == "Digit1" && b == "Digit2" && c == "Digit3" && d == "Digit4") return "1-4";
            return string.Join("/", bindings.Label(GameAction.Tool1), bindings.Label(GameAction.Tool2),
                bindings.Label(GameAction.Tool3), bindings.Label(GameAction.Tool4));
        }

        /// <summary>Toast shown on arriving in a yard: Corner Lot. A note from The Okafor twins: "..."</summary>
        public static string ArrivalNote(string yardName, string neighbour, string note)
        {
            if (string.IsNullOrEmpty(note)) return string.IsNullOrEmpty(yardName) ? null : yardName;
            string who = string.IsNullOrEmpty(neighbour) ? "the neighbour" : neighbour;
            return string.IsNullOrEmpty(yardName) ? $"A note from {who}: \"{note}\"" : $"{yardName}. A note from {who}: \"{note}\"";
        }
    }
}

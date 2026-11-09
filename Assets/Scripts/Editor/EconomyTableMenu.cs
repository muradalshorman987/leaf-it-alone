using System.Collections.Generic;
using System.IO;
using FallenYard.Progression;
using FallenYard.Tools;
using UnityEditor;
using UnityEngine;

namespace FallenYard.EditorTools
{
    /// <summary>
    /// Menu: Fallen Yard > Economy. Exports every tool price, upgrade multiplier and yard price to a
    /// CSV (see <see cref="EconomyTable"/>) for balancing in a spreadsheet, and imports it back into
    /// the ToolUpgradeDefinition and YardUnlockTable assets.
    /// </summary>
    public static class EconomyTableMenu
    {
        const string DefaultFile = "economy.csv";

        [MenuItem("Fallen Yard/Economy/Export Tuning Table (CSV)...")]
        static void Export()
        {
            var tools = LoadAll<ToolUpgradeDefinition>();
            var yards = LoadAll<YardUnlockTable>();
            if (tools.Count == 0 && yards.Count == 0)
            {
                EditorUtility.DisplayDialog("Economy", "No ToolUpgradeDefinition or YardUnlockTable assets found. Run Fallen Yard > Build Test Yard first.", "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanel("Export economy tuning table", "docs", DefaultFile, "csv");
            if (string.IsNullOrEmpty(path)) return;

            var rows = EconomyTable.Capture(tools, yards.Count > 0 ? yards[0] : null);
            File.WriteAllText(path, EconomyTable.ToCsv(rows, CoinsPerLeaf()));
            Debug.Log($"[Economy] Wrote {rows.Count} rows to {path}");
        }

        [MenuItem("Fallen Yard/Economy/Import Tuning Table (CSV)...")]
        static void Import()
        {
            string path = EditorUtility.OpenFilePanel("Import economy tuning table", "docs", "csv");
            if (string.IsNullOrEmpty(path)) return;

            var errors = new List<string>();
            var rows = EconomyTable.Parse(File.ReadAllText(path), errors);
            foreach (var e in errors) Debug.LogWarning($"[Economy] {e}");

            var tools = LoadAll<ToolUpgradeDefinition>();
            var yards = LoadAll<YardUnlockTable>();
            var table = yards.Count > 0 ? yards[0] : null;

            var touched = new List<Object>(tools);
            if (table != null) touched.Add(table);
            Undo.RecordObjects(touched.ToArray(), "Import economy table");

            var warnings = new List<string>();
            int changed = EconomyTable.Apply(rows, tools, table, warnings);
            foreach (var w in warnings) Debug.LogWarning($"[Economy] {w}");
            foreach (var o in touched) EditorUtility.SetDirty(o);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Economy] Imported {rows.Count} rows, {changed} values changed.");
        }

        static float CoinsPerLeaf()
        {
            var bag = Object.FindFirstObjectByType<BagInteractor>();
            return bag != null ? bag.coinsPerLeaf : 0.5f;
        }

        static List<T> LoadAll<T>() where T : Object
        {
            var list = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            return list;
        }
    }
}

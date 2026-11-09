using System.Collections.Generic;
using FallenYard.Progression;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Progression
{
    public class EconomyTableTests
    {
        readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) Object.DestroyImmediate(o);
            _made.Clear();
        }

        ToolUpgradeDefinition Tool(string id, bool unlocked, int unlockCost, params int[] levelCosts)
        {
            var d = ScriptableObject.CreateInstance<ToolUpgradeDefinition>();
            d.toolId = id;
            d.unlockedByDefault = unlocked;
            d.unlockCost = unlockCost;
            d.levels = new ToolUpgradeDefinition.UpgradeLevel[levelCosts.Length];
            for (int i = 0; i < levelCosts.Length; i++)
                d.levels[i] = new ToolUpgradeDefinition.UpgradeLevel { cost = levelCosts[i], radiusMultiplier = 1f + 0.25f * (i + 1) };
            _made.Add(d);
            return d;
        }

        YardUnlockTable Yards()
        {
            var t = ScriptableObject.CreateInstance<YardUnlockTable>();
            t.yards.Add(new YardUnlockRule { yardId = "yard_01", unlockedByDefault = true });
            t.yards.Add(new YardUnlockRule { yardId = "yard_02", unlockCost = 300 });
            _made.Add(t);
            return t;
        }

        [Test]
        public void Capture_ListsEveryPrice()
        {
            var rows = EconomyTable.Capture(new[] { Tool("rake", true, 0, 50, 120), Tool("blower", false, 200, 150) }, Yards());
            Assert.AreEqual(2 + 2 + 2, rows.Count, "rake: 2 levels; blower: unlock + 1 level; 2 yards");
            Assert.IsTrue(rows.Exists(r => r.kind == EconomyRow.ToolUnlock && r.id == "blower" && r.cost == 200));
            Assert.IsFalse(rows.Exists(r => r.kind == EconomyRow.ToolUnlock && r.id == "rake"), "free default tools have no unlock row");
            Assert.IsTrue(rows.Exists(r => r.kind == EconomyRow.ToolLevel && r.id == "rake" && r.level == 2 && r.cost == 120 && Mathf.Approximately(r.radius, 1.5f)));
            Assert.IsTrue(rows.Exists(r => r.kind == EconomyRow.YardUnlock && r.id == "yard_02" && r.cost == 300));
        }

        [Test]
        public void Csv_RoundTrips_WithLeavesColumn()
        {
            var rows = EconomyTable.Capture(new[] { Tool("rake", true, 0, 50) }, Yards());
            string csv = EconomyTable.ToCsv(rows, 0.5f);
            StringAssert.StartsWith(EconomyTable.Header, csv);
            StringAssert.Contains("tool_level,rake,1,50,1.25,1,1,1,100", csv);

            var parsed = EconomyTable.Parse(csv);
            Assert.AreEqual(rows.Count, parsed.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                Assert.AreEqual(rows[i].Key, parsed[i].Key);
                Assert.AreEqual(rows[i].cost, parsed[i].cost);
                Assert.AreEqual(rows[i].radius, parsed[i].radius, 1e-4f);
            }
        }

        [Test]
        public void Parse_ReportsBadLines_AndKeepsGoodOnes()
        {
            var errors = new List<string>();
            var rows = EconomyTable.Parse("kind,id\r\nbogus,x,0,1\ntool_level,rake,1,abc\n# comment\n\ntool_level,rake,1,75,,,,\n", errors);
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(75, rows[0].cost);
            Assert.AreEqual(1f, rows[0].radius, "blank multipliers mean 1");
            Assert.AreEqual(2, errors.Count);
        }

        [Test]
        public void Apply_WritesPricesBack_AndWarnsAboutUnknownRows()
        {
            var rake = Tool("rake", true, 0, 50, 120);
            var yards = Yards();
            var rows = EconomyTable.Parse(
                "tool_level,rake,2,140,1.6,1,1,1\n" +
                "yard_unlock,yard_02,0,250\n" +
                "tool_level,rake,9,1\n" +
                "tool_unlock,hoover,0,10\n");
            var warnings = new List<string>();
            int changed = EconomyTable.Apply(rows, new[] { rake }, yards, warnings);

            Assert.AreEqual(140, rake.levels[1].cost);
            Assert.AreEqual(1.6f, rake.levels[1].radiusMultiplier, 1e-4f);
            Assert.AreEqual(250, yards.Get("yard_02").unlockCost);
            Assert.AreEqual(3, changed);
            Assert.AreEqual(2, warnings.Count);
        }

        [Test]
        public void LeavesFor_RoundsUp()
        {
            Assert.AreEqual(101, EconomyTable.LeavesFor(50, 0.497f));
            Assert.AreEqual(100, EconomyTable.LeavesFor(50, 0.5f));
            Assert.AreEqual(0, EconomyTable.LeavesFor(50, 0f));
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using FallenYard.Leaves;
using FallenYard.Levels;
using FallenYard.Progression;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Levels
{
    public class YardLayoutsTests
    {
        List<YardDefinition> _yards;

        [SetUp]
        public void SetUp() => _yards = YardLayouts.CreateAll();

        [TearDown]
        public void TearDown()
        {
            foreach (var y in _yards) Object.DestroyImmediate(y);
        }

        [Test]
        public void ThereAreFiveYardsInPlayOrder()
        {
            Assert.AreEqual(5, _yards.Count);
            CollectionAssert.AreEqual(YardLayouts.Order, _yards.Select(y => y.yardId).ToArray());
        }

        [Test]
        public void EveryLayoutValidates()
        {
            foreach (var y in _yards)
                CollectionAssert.IsEmpty(y.Validate(), $"{y.yardId}: " + string.Join("; ", y.Validate()));
        }

        [Test]
        public void IdsAndScenesAreUnique()
        {
            Assert.AreEqual(5, _yards.Select(y => y.yardId).Distinct().Count());
            Assert.AreEqual(5, _yards.Select(y => y.sceneName).Distinct().Count());
        }

        [Test]
        public void YardsGrowAndStayWithinTheLeafBudget()
        {
            for (int i = 1; i < _yards.Count; i++)
            {
                Assert.Greater(_yards[i].leafCount, _yards[i - 1].leafCount, _yards[i].yardId);
                Assert.Greater(_yards[i].size.x * _yards[i].size.y, _yards[i - 1].size.x * _yards[i - 1].size.y, _yards[i].yardId);
                Assert.Greater(_yards[i].completionBonus, _yards[i - 1].completionBonus, _yards[i].yardId);
            }
            Assert.LessOrEqual(_yards.Max(y => y.leafCount), 30000, "M2 target is 30k leaves at 60 fps");
        }

        [Test]
        public void EveryYardHasAHouseANeighbourAndANote()
        {
            foreach (var y in _yards)
            {
                Assert.IsTrue(y.obstacles.Any(o => o.kind == ObstacleKind.House), y.yardId);
                Assert.IsNotEmpty(y.neighbourName, y.yardId);
                Assert.IsNotEmpty(y.neighbourNote, y.yardId);
                Assert.IsNotEmpty(y.leafMix, y.yardId);
                Assert.GreaterOrEqual(y.palette.leafColors.Length, 3, y.yardId);
            }
        }

        [Test]
        public void LeafMixesTeachTheLeafTypes()
        {
            bool HasMix(string id, LeafPreset p) => _yards.First(y => y.yardId == id).leafMix.Any(m => m.preset == p && m.weight > 0f);
            Assert.IsFalse(HasMix(YardLayouts.StarterLawn, LeafPreset.WetClump), "first yard stays simple");
            Assert.IsTrue(HasMix(YardLayouts.Orchard, LeafPreset.WetClump));
            Assert.IsTrue(HasMix(YardLayouts.ChurchGreen, LeafPreset.PineNeedles));
            foreach (LeafPreset p in System.Enum.GetValues(typeof(LeafPreset)))
                Assert.IsTrue(HasMix(YardLayouts.HilltopManor, p), "the last yard uses every leaf type: " + p);
        }

        [Test]
        public void CreateById_MatchesCreateAll()
        {
            foreach (var id in YardLayouts.Order)
            {
                var y = YardLayouts.Create(id);
                Assert.AreEqual(id, y.yardId);
                Object.DestroyImmediate(y);
            }
            Assert.IsNull(YardLayouts.Create("nowhere"));
        }

        [Test]
        public void UnlockRules_ChainTheYardsAndValidate()
        {
            var table = ScriptableObject.CreateInstance<YardUnlockTable>();
            table.yards = YardLayouts.CreateUnlockRules();
            try
            {
                CollectionAssert.IsEmpty(table.Validate());
                CollectionAssert.AreEqual(YardLayouts.Order, table.yards.Select(r => r.yardId).ToArray());
                Assert.IsTrue(table.yards[0].unlockedByDefault);
                for (int i = 1; i < table.yards.Count; i++)
                {
                    Assert.IsFalse(table.yards[i].unlockedByDefault);
                    CollectionAssert.Contains(table.yards[i].requiredYards, table.yards[i - 1].yardId);
                    Assert.GreaterOrEqual(table.yards[i].unlockCost, table.yards[i - 1].unlockCost);
                }
                var rewards = table.yards.SelectMany(r => r.rewardToolIds).ToList();
                CollectionAssert.AreEquivalent(new[] { "blower", "vacuum", "mulcher" }, rewards);
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }
    }
}

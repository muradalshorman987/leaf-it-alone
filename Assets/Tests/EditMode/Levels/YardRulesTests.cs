using FallenYard.Levels;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Levels
{
    public class YardRulesTests
    {
        YardDefinition _def;

        [SetUp]
        public void SetUp()
        {
            _def = ScriptableObject.CreateInstance<YardDefinition>();
            _def.yardId = "test";
            _def.displayName = "Test";
            _def.sceneName = "Yard_Test";
            _def.size = new Vector2(20f, 20f);
            _def.leafCount = 100;
            _def.playerSpawn = new Vector2(0f, -8f);
            _def.compostBinPosition = new Vector2(-8f, -8f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_def);

        static YardObstacle Box(ObstacleKind kind, Vector2 pos, Vector3 size, float rot = 0f) =>
            new YardObstacle { kind = kind, position = pos, size = size, rotation = rot };

        [Test]
        public void RotatedBox_MatchesUnityRotation()
        {
            var house = Box(ObstacleKind.House, new Vector2(2f, 3f), new Vector3(6f, 3f, 2f), 30f);
            var rot = Quaternion.Euler(0f, 30f, 0f);
            // Points inside the box's own frame, moved into the world the way Unity would.
            foreach (var local in new[] { new Vector3(2.9f, 0f, 0.9f), new Vector3(-2.9f, 0f, -0.9f), Vector3.zero })
            {
                var w = rot * local;
                Assert.IsTrue(YardRules.Contains(house, new Vector2(2f + w.x, 3f + w.z)), local.ToString());
            }
            foreach (var local in new[] { new Vector3(3.2f, 0f, 0f), new Vector3(0f, 0f, 1.2f) })
            {
                var w = rot * local;
                Assert.IsFalse(YardRules.Contains(house, new Vector2(2f + w.x, 3f + w.z)), local.ToString());
            }
        }

        [Test]
        public void Margin_GrowsTheFootprint()
        {
            var shed = Box(ObstacleKind.Shed, Vector2.zero, new Vector3(2f, 2f, 2f));
            Assert.IsFalse(YardRules.Contains(shed, new Vector2(1.3f, 0f)));
            Assert.IsTrue(YardRules.Contains(shed, new Vector2(1.3f, 0f), 0.5f));
        }

        [Test]
        public void Trees_BlockOnlyTheirTrunk()
        {
            var tree = Box(ObstacleKind.Tree, Vector2.zero, new Vector3(6f, 6f, 6f));
            Assert.IsTrue(YardRules.Contains(tree, new Vector2(0.2f, 0f)));
            Assert.IsFalse(YardRules.Contains(tree, new Vector2(1.5f, 0f)), "leaves pile under the canopy");
        }

        [Test]
        public void PathsBenchesAndFlowerbeds_DontBlockLeaves()
        {
            Assert.IsFalse(YardRules.BlocksLeaves(ObstacleKind.Path));
            Assert.IsFalse(YardRules.BlocksLeaves(ObstacleKind.Bench));
            Assert.IsFalse(YardRules.BlocksLeaves(ObstacleKind.Flowerbed));
            Assert.IsTrue(YardRules.BlocksLeaves(ObstacleKind.House));
            Assert.IsTrue(YardRules.BlocksLeaves(ObstacleKind.Hedge));
            Assert.IsFalse(YardRules.IsSolid(ObstacleKind.Path));
            Assert.IsTrue(YardRules.IsSolid(ObstacleKind.Bench));
        }

        [Test]
        public void OpenGround_ExcludesHousesTheBinAndOutside()
        {
            _def.obstacles.Add(Box(ObstacleKind.House, new Vector2(0f, 6f), new Vector3(8f, 4f, 4f)));
            _def.obstacles.Add(Box(ObstacleKind.Path, new Vector2(0f, -2f), new Vector3(1f, 0f, 8f)));
            Assert.IsTrue(YardRules.IsOpenGround(_def, new Vector2(0f, -2f)), "paths are open");
            Assert.IsFalse(YardRules.IsOpenGround(_def, new Vector2(0f, 6f)));
            Assert.IsFalse(YardRules.IsOpenGround(_def, new Vector2(-8f, -8f)), "compost bin");
            Assert.IsFalse(YardRules.IsOpenGround(_def, new Vector2(11f, 0f)), "outside the fence");
            Assert.IsTrue(YardRules.IsOpenGround(_def, new Vector2(5f, -5f)));
        }

        [Test]
        public void Validate_CleanYardHasNoProblems()
        {
            CollectionAssert.IsEmpty(_def.Validate());
        }

        [Test]
        public void Validate_CatchesDesignMistakes()
        {
            _def.leafCount = 0;
            _def.yardId = "";
            _def.obstacles.Add(Box(ObstacleKind.House, new Vector2(0f, -8f), new Vector3(4f, 3f, 4f)));
            _def.obstacles.Add(Box(ObstacleKind.Rock, new Vector2(-8f, -8f), new Vector3(1f, 1f, 1f)));
            _def.obstacles.Add(Box(ObstacleKind.Bush, new Vector2(30f, 0f), Vector3.one));
            _def.spawnZones.Add(new LeafSpawnZone { label = "Far away", center = new Vector2(0f, 50f), weight = 1f });
            var problems = string.Join("\n", _def.Validate());
            StringAssert.Contains("no id", problems);
            StringAssert.Contains("no leaves", problems);
            StringAssert.Contains("player spawn", problems);
            StringAssert.Contains("compost bin overlaps", problems);
            StringAssert.Contains("outside the yard", problems);
            StringAssert.Contains("Far away", problems);
        }

        [Test]
        public void Validate_NeedsSomewhereForLeaves()
        {
            _def.backgroundWeight = 0f;
            StringAssert.Contains("nowhere to spawn", string.Join("\n", _def.Validate()));
            _def.spawnZones.Add(new LeafSpawnZone { center = Vector2.zero, weight = 1f });
            CollectionAssert.IsEmpty(_def.Validate());
        }

        [Test]
        public void Weather_CopiesOntoGustSettings()
        {
            var s = new FallenYard.Leaves.GustSettings();
            _def.weather.prevailingDirection = 123f;
            _def.weather.breezeStrength = 0.4f;
            _def.weather.gustsEnabled = false;
            _def.weather.gustStrength = new Vector2(1f, 2f);
            _def.weather.ApplyTo(s);
            Assert.AreEqual(123f, s.prevailingDirection);
            Assert.AreEqual(0.4f, s.breezeStrength);
            Assert.IsFalse(s.gustsEnabled);
            Assert.AreEqual(new Vector2(1f, 2f), s.gustStrength);
        }
    }
}

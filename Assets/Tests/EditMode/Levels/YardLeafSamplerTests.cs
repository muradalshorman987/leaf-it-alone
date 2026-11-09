using FallenYard.Levels;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Levels
{
    public class YardLeafSamplerTests
    {
        YardDefinition _def;

        [SetUp]
        public void SetUp()
        {
            _def = ScriptableObject.CreateInstance<YardDefinition>();
            _def.size = new Vector2(20f, 16f);
            _def.compostBinPosition = new Vector2(-8f, -6f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_def);

        [Test]
        public void Samples_StayInsideTheYardAndOffBlockedGround()
        {
            _def.obstacles.Add(new YardObstacle { kind = ObstacleKind.House, position = new Vector2(0f, 4f), size = new Vector3(10f, 4f, 6f) });
            _def.spawnZones.Add(new LeafSpawnZone { shape = ZoneShape.Circle, center = new Vector2(0f, 4f), size = new Vector2(6f, 6f), weight = 5f });
            var sampler = new YardLeafSampler(_def);
            var rng = new System.Random(5);
            for (int i = 0; i < 3000; i++)
            {
                var p = sampler.SamplePosition(rng);
                Assert.IsTrue(YardRules.InsideYard(_def.size, p), p.ToString());
                Assert.IsTrue(YardRules.IsOpenGround(_def, p), "leaf spawned in a blocked spot " + p);
            }
        }

        [Test]
        public void HeavyZone_GetsMostLeaves()
        {
            _def.backgroundWeight = 1f;
            var zone = new LeafSpawnZone { shape = ZoneShape.Rectangle, center = new Vector2(6f, 4f), size = new Vector2(4f, 4f), weight = 9f };
            _def.spawnZones.Add(zone);
            var sampler = new YardLeafSampler(_def);
            var rng = new System.Random(11);
            int inside = 0;
            const int n = 4000;
            for (int i = 0; i < n; i++)
            {
                var p = sampler.SamplePosition(rng);
                if (Mathf.Abs(p.x - 6f) <= 2f && Mathf.Abs(p.y - 4f) <= 2f) inside++;
            }
            // 90% from the zone, plus the background's small share of that patch.
            Assert.Greater(inside, n * 0.85f);
        }

        [Test]
        public void NoZones_SpreadsEvenly()
        {
            var sampler = new YardLeafSampler(_def);
            var rng = new System.Random(3);
            int left = 0;
            const int n = 4000;
            for (int i = 0; i < n; i++)
                if (sampler.SamplePosition(rng).x < 0f) left++;
            Assert.AreEqual(0.5f, left / (float)n, 0.05f);
        }

        [Test]
        public void SameSeed_SameLeaves()
        {
            _def.spawnZones.Add(new LeafSpawnZone { center = new Vector2(3f, 3f), size = new Vector2(2f, 2f), weight = 2f });
            var a = new YardLeafSampler(_def);
            var b = new YardLeafSampler(_def);
            var ra = new System.Random(42);
            var rb = new System.Random(42);
            for (int i = 0; i < 200; i++) Assert.AreEqual(a.SamplePosition(ra), b.SamplePosition(rb));
        }

        [Test]
        public void EveryRealYard_SpawnsOnlyOnOpenGround()
        {
            foreach (var y in YardLayouts.CreateAll())
            {
                var sampler = new YardLeafSampler(y);
                var rng = new System.Random(y.seed);
                for (int i = 0; i < 1500; i++)
                {
                    var p = sampler.SamplePosition(rng);
                    Assert.IsTrue(YardRules.IsOpenGround(y, p), $"{y.yardId}: {p}");
                }
                Object.DestroyImmediate(y);
            }
        }
    }
}

using System.Collections.Generic;
using FallenYard.Leaves;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Leaves
{
    /// <summary>Drives the Burst/Jobs simulation synchronously through LeafField.Step.</summary>
    public class LeafFieldSimulationTests
    {
        readonly List<LeafField> _fields = new List<LeafField>();
        readonly List<Object> _assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fields) TestUtil.DestroyLeafField(f);
            _fields.Clear();
            foreach (var a in _assets) if (a != null) Object.DestroyImmediate(a);
            _assets.Clear();
        }

        LeafField Field(Vector2 size, float cell, int count, int seed = 1)
        {
            var f = TestUtil.CreateLeafField(size, cell, count, seed);
            _fields.Add(f);
            return f;
        }

        /// <summary>A field whose leaves are all one preset type.</summary>
        LeafField SingleTypeField(LeafPreset preset, Vector2 size, int count)
        {
            var def = ScriptableObject.CreateInstance<LeafTypeDefinition>();
            def.ApplyPreset(preset);
            _assets.Add(def);

            var f = new GameObject("LeafField (" + preset + ")").AddComponent<LeafField>();
            f.yardSize = size;
            f.cellSize = 0.5f;
            f.leafCount = count;
            f.spawnOnStart = false;
            f.leafTypes = new[] { def };
            TestUtil.Call(f, "Awake");
            f.Spawn(3);
            _fields.Add(f);
            return f;
        }

        static float MeanSpeed(LeafField f)
        {
            float sum = 0f;
            int n = 0;
            for (int i = 0; i < f.TotalLeaves; i++)
            {
                var s = f.GetLeaf(i);
                if (s.IsCollected) continue;
                sum += new Vector2(s.vel.x, s.vel.y).magnitude;
                n++;
            }
            return n == 0 ? 0f : sum / n;
        }

        static Vector2 MeanPosition(LeafField f)
        {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < f.TotalLeaves; i++)
            {
                var s = f.GetLeaf(i);
                sum += new Vector2(s.pos.x, s.pos.y);
            }
            return sum / Mathf.Max(1, f.TotalLeaves);
        }

        [Test]
        public void Spawn_PlacesEveryLeafInsideTheYardAndGridCountsMatch()
        {
            var f = Field(new Vector2(10f, 6f), 0.5f, 3000);
            Assert.AreEqual(3000, f.TotalLeaves);
            Assert.AreEqual(20, f.GridWidth);
            Assert.AreEqual(12, f.GridHeight);

            int gridTotal = 0;
            for (int y = 0; y < f.GridHeight; y++)
            for (int x = 0; x < f.GridWidth; x++)
                gridTotal += f.CellCount(x, y);
            Assert.AreEqual(3000, gridTotal);

            for (int i = 0; i < f.TotalLeaves; i++)
                Assert.IsTrue(f.IsInsideYard(f.GetLeafPosition(i)), $"leaf {i} outside the yard");
        }

        [Test]
        public void Spawn_DefaultFieldUsesTheBuiltInFourTypeMix()
        {
            var f = Field(new Vector2(10f, 10f), 0.5f, 4000);
            Assert.AreEqual(4, f.LeafTypeCount);
            var seen = new int[f.LeafTypeCount];
            for (int i = 0; i < f.TotalLeaves; i++) seen[f.GetLeaf(i).type]++;
            for (int t = 0; t < seen.Length; t++)
                Assert.Greater(seen[t], 100, $"type {f.LeafTypeId(t)} should be in the mix");
        }

        [Test]
        public void Spawn_WithBuiltInMixOff_SpawnsOneType()
        {
            var f = new GameObject("LeafField (plain)").AddComponent<LeafField>();
            f.yardSize = new Vector2(4f, 4f);
            f.leafCount = 500;
            f.spawnOnStart = false;
            f.builtInTypeMix = false;
            TestUtil.Call(f, "Awake");
            f.Spawn(1);
            _fields.Add(f);
            Assert.AreEqual(1, f.LeafTypeCount);
            Assert.AreEqual("oak", f.LeafTypeId(0));
        }

        [Test]
        public void Spawn_SameSeed_IsDeterministic()
        {
            var a = Field(new Vector2(8f, 8f), 0.5f, 1500, seed: 42);
            var b = Field(new Vector2(8f, 8f), 0.5f, 1500, seed: 42);
            for (int i = 0; i < a.TotalLeaves; i++)
                Assert.AreEqual(a.GetLeafPosition(i), b.GetLeafPosition(i), $"leaf {i}");
        }

        [Test]
        public void Spawn_SlicesNeverExceedDrawLimit()
        {
            var f = Field(new Vector2(30f, 30f), 0.4f, 30000);
            Assert.AreEqual(30000, f.TotalLeaves);
            Assert.GreaterOrEqual(f.RenderSliceCount, 30000 / LeafField.SliceSize);
        }

        [Test]
        public void Step_WithNoForces_LeavesStayPut()
        {
            var f = Field(new Vector2(20f, 20f), 0.5f, 800);
            Vector3 before = f.GetLeafPosition(10);
            for (int i = 0; i < 30; i++) f.Step(1f / 60f);
            Vector3 after = f.GetLeafPosition(10);
            Assert.Less(Vector2.Distance(new Vector2(before.x, before.z), new Vector2(after.x, after.z)), 0.01f);
        }

        [Test]
        public void Push_MovesLeavesTowardTargetVelocity()
        {
            var f = Field(new Vector2(6f, 6f), 0.5f, 1500);
            int touched = f.Push(Vector3.zero, 1.5f, new Vector3(2f, 0f, 0f), 10f, 1f / 30f);
            Assert.Greater(touched, 0);

            Vector2 before = MeanPosition(f);
            for (int i = 0; i < 20; i++) f.Step(1f / 60f);
            Vector2 after = MeanPosition(f);
            Assert.Greater(after.x, before.x, "pushed leaves drift +X");
        }

        [Test]
        public void Step_KeepsLeavesInsideTheYard()
        {
            var f = Field(new Vector2(4f, 4f), 0.5f, 600);
            for (int i = 0; i < 30; i++)
            {
                f.Push(Vector3.zero, 10f, new Vector3(8f, 0f, 8f), 50f, 1f / 30f);
                f.Step(1f / 30f);
            }
            for (int i = 0; i < f.TotalLeaves; i++)
                Assert.IsTrue(f.IsInsideYard(f.GetLeafPosition(i)), $"leaf {i} escaped");
        }

        [Test]
        public void BlowCone_LiftsLeavesAndPushesAwayFromNozzle()
        {
            var f = SingleTypeField(LeafPreset.Oak, new Vector2(10f, 10f), 2000);
            Vector3 nozzle = new Vector3(0f, 1f, -4f);
            int touched = f.BlowCone(nozzle, Vector3.forward, 5f, 30f, 25f, 1.6f, 1f / 30f);
            Assert.Greater(touched, 0);

            bool anyLifted = false;
            for (int i = 0; i < f.TotalLeaves; i++) anyLifted |= f.GetLeaf(i).liftVel > 0f;
            Assert.IsTrue(anyLifted, "the blower lifts leaves");
        }

        [Test]
        public void BlowCone_PineMovesMuchMoreThanWetClumps()
        {
            var pine = SingleTypeField(LeafPreset.PineNeedles, new Vector2(10f, 10f), 2000);
            var wet = SingleTypeField(LeafPreset.WetClump, new Vector2(10f, 10f), 2000);
            Vector3 nozzle = new Vector3(0f, 1f, -4f);
            pine.BlowCone(nozzle, Vector3.forward, 5f, 30f, 25f, 1.6f, 1f / 30f);
            wet.BlowCone(nozzle, Vector3.forward, 5f, 30f, 25f, 1.6f, 1f / 30f);
            Assert.Greater(MeanSpeed(pine), MeanSpeed(wet) * 4f);
        }

        [Test]
        public void Push_WetClumpsFollowTheRakeBetterThanPine()
        {
            var pine = SingleTypeField(LeafPreset.PineNeedles, new Vector2(6f, 6f), 1500);
            var wet = SingleTypeField(LeafPreset.WetClump, new Vector2(6f, 6f), 1500);
            pine.Push(Vector3.zero, 2f, new Vector3(2f, 0f, 0f), 10f, 1f / 30f);
            wet.Push(Vector3.zero, 2f, new Vector3(2f, 0f, 0f), 10f, 1f / 30f);
            Assert.Greater(MeanSpeed(wet), MeanSpeed(pine) * 2f);
        }

        [Test]
        public void Attract_DrawsLeavesTowardThePoint()
        {
            var f = SingleTypeField(LeafPreset.Oak, new Vector2(6f, 6f), 1500);
            Vector3 mouth = new Vector3(2f, 0f, 0f);
            int touched = f.Attract(mouth, 2f, 3f, 10f, 1f / 30f);
            Assert.Greater(touched, 0);
            float before = Vector2.Distance(MeanPosition(f), new Vector2(mouth.x, mouth.z));
            for (int i = 0; i < 20; i++)
            {
                f.Attract(mouth, 2f, 3f, 10f, 1f / 60f);
                f.Step(1f / 60f);
            }
            float after = Vector2.Distance(MeanPosition(f), new Vector2(mouth.x, mouth.z));
            Assert.Less(after, before);
        }

        [Test]
        public void Wind_BelowThreshold_DoesNotMoveOak()
        {
            var f = SingleTypeField(LeafPreset.Oak, new Vector2(10f, 10f), 1000);
            f.SetWind(Vector3.right, LeafPhysicsSettings.For(LeafPreset.Oak).windThreshold * 0.5f);
            for (int i = 0; i < 30; i++) f.Step(1f / 30f);
            Assert.AreEqual(0, f.MovingLeaves);
        }

        [Test]
        public void Wind_StrongGust_BlowsLeavesDownwind()
        {
            var f = SingleTypeField(LeafPreset.Maple, new Vector2(20f, 20f), 1000);
            Vector2 before = MeanPosition(f);
            f.SetWind(Vector3.right, 6f);
            for (int i = 0; i < 60; i++) f.Step(1f / 30f);
            Vector2 after = MeanPosition(f);
            Assert.Greater(f.WindStrength, 0f);
            Assert.Greater(after.x - before.x, 0.2f, "leaves should travel with the wind");
            Assert.Less(Mathf.Abs(after.y - before.y), (after.x - before.x) * 0.5f, "mostly along the wind");
        }

        [Test]
        public void Wind_MapleTravelsFurtherThanWetClumps()
        {
            var maple = SingleTypeField(LeafPreset.Maple, new Vector2(20f, 20f), 1000);
            var wet = SingleTypeField(LeafPreset.WetClump, new Vector2(20f, 20f), 1000);
            Vector2 m0 = MeanPosition(maple), w0 = MeanPosition(wet);
            maple.SetWind(Vector3.right, 5f);
            wet.SetWind(Vector3.right, 5f);
            for (int i = 0; i < 60; i++)
            {
                maple.Step(1f / 30f);
                wet.Step(1f / 30f);
            }
            Assert.Greater(MeanPosition(maple).x - m0.x, (MeanPosition(wet).x - w0.x) * 3f);
        }

        [Test]
        public void CollectInRadius_RemovesLeavesAndUpdatesCounts()
        {
            var f = Field(new Vector2(4f, 4f), 0.5f, 1000);
            int reported = 0;
            f.OnLeavesCollected += n => reported += n;

            int inside = f.CountInRadius(Vector3.zero, 1f);
            int got = f.CollectInRadius(Vector3.zero, 1f);
            Assert.AreEqual(inside, got);
            Assert.AreEqual(got, reported);
            Assert.AreEqual(got, f.CollectedLeaves);
            Assert.AreEqual(1000 - got, f.RemainingLeaves);
            Assert.AreEqual(0, f.CountInRadius(Vector3.zero, 1f));

            // Collected leaves stay collected through simulation and pushes (neighbours may
            // drift a few cm into the hole, so check well inside it).
            f.Push(Vector3.zero, 3f, Vector3.right, 10f, 0.1f);
            f.Step(0.1f);
            Assert.AreEqual(0, f.CountInRadius(Vector3.zero, 0.5f));
            Assert.AreEqual(got, f.CollectedLeaves);
        }

        [Test]
        public void CollectInRadius_RespectsMaxLeaves()
        {
            var f = Field(new Vector2(4f, 4f), 0.5f, 1000);
            Assert.AreEqual(25, f.CollectInRadius(Vector3.zero, 3f, 25));
            Assert.AreEqual(25, f.CollectedLeaves);
        }

        [Test]
        public void Piles_SlumpOverTime()
        {
            // Everything starts in one 2 x 2 m corner of a 6 x 6 m yard: steep, so it slumps outward.
            var f = Field(new Vector2(6f, 6f), 0.4f, 2000);
            for (int i = 0; i < 40; i++)
            {
                f.Push(new Vector3(-2f, 0f, -2f), 6f, new Vector3(-3f, 0f, -3f), 20f, 1f / 30f);
                f.Step(1f / 30f);
            }
            int peakBefore = PeakCellCount(f);
            for (int i = 0; i < 120; i++) f.Step(1f / 30f);
            Assert.Less(PeakCellCount(f), peakBefore, "the tallest cell loses leaves to its neighbours");
        }

        static int PeakCellCount(LeafField f)
        {
            int peak = 0;
            for (int y = 0; y < f.GridHeight; y++)
            for (int x = 0; x < f.GridWidth; x++)
                peak = Mathf.Max(peak, f.CellCount(x, y));
            return peak;
        }

        [Test]
        public void Respawn_ReleasesOldDataAndStartsClean()
        {
            var f = Field(new Vector2(4f, 4f), 0.5f, 500);
            f.CollectInRadius(Vector3.zero, 5f);
            Assert.AreEqual(500, f.CollectedLeaves);
            f.leafCount = 700;
            f.Spawn(9);
            Assert.AreEqual(700, f.TotalLeaves);
            Assert.AreEqual(0, f.CollectedLeaves);
        }

        [Test]
        public void Query_ExposesTheActiveFieldThroughTheInterface()
        {
            var f = Field(new Vector2(4f, 4f), 0.5f, 200);
            ILeafQuery q = LeafField.Query;
            Assert.AreSame(f, q);
            Assert.AreEqual(200, q.RemainingLeaves);
        }
    }
}

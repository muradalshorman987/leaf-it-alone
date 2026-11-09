using FallenYard.Leaves;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace FallenYard.Tests.Leaves
{
    public class LeafPhysicsTests
    {
        static LeafTypeParams Oak => LeafPhysicsSettings.For(LeafPreset.Oak).ToParams();

        [Test]
        public void WindAcceleration_BelowThreshold_IsZero()
        {
            var oak = Oak;
            float2 a = LeafPhysics.WindAcceleration(new float2(1f, 0f), oak.windThreshold - 0.1f, oak, 1f, 1.5f);
            Assert.AreEqual(0f, math.length(a), 1e-6f);
        }

        [Test]
        public void WindAcceleration_AboveThreshold_PointsDownwindAndScalesWithExcess()
        {
            var oak = Oak;
            float2 dir = new float2(0f, 1f);
            float2 weak = LeafPhysics.WindAcceleration(dir, oak.windThreshold + 1f, oak, 1f, 1.5f);
            float2 strong = LeafPhysics.WindAcceleration(dir, oak.windThreshold + 2f, oak, 1f, 1.5f);
            Assert.Greater(weak.y, 0f);
            Assert.AreEqual(0f, weak.x, 1e-6f);
            Assert.AreEqual(weak.y * 2f, strong.y, 1e-4f);
        }

        [Test]
        public void WindAcceleration_HeavierLeafMovesLess()
        {
            var light = LeafPhysicsSettings.For(LeafPreset.Oak);
            var heavy = light;
            heavy.mass = light.mass * 2f;
            float2 dir = new float2(1f, 0f);
            float a = LeafPhysics.WindAcceleration(dir, 5f, light.ToParams(), 1f, 1f).x;
            float b = LeafPhysics.WindAcceleration(dir, 5f, heavy.ToParams(), 1f, 1f).x;
            Assert.AreEqual(a * 0.5f, b, 1e-4f);
        }

        [Test]
        public void WindExposure_LoneLeafFullyExposed_PileBottomSheltered()
        {
            Assert.AreEqual(1f, LeafPhysics.WindExposure(0f, 1, 14), 0.05f, "a lone leaf catches the wind");
            Assert.AreEqual(0f, LeafPhysics.WindExposure(0f, 200, 14), 1e-4f, "the bottom of a big pile is sheltered");
            Assert.AreEqual(1f, LeafPhysics.WindExposure(1f, 200, 14), 1e-4f, "the top of a big pile is exposed");
            Assert.Less(LeafPhysics.WindExposure(0.5f, 200, 14), 0.5f);
        }

        [Test]
        public void DampFactor_DecaysWithDragAndTime()
        {
            Assert.AreEqual(1f, LeafPhysics.DampFactor(5f, 1f, 0f), 1e-6f);
            float oneStep = LeafPhysics.DampFactor(5f, 1f, 0.1f);
            Assert.Less(oneStep, 1f);
            Assert.AreEqual(oneStep * oneStep, LeafPhysics.DampFactor(5f, 1f, 0.2f), 1e-5f);
            Assert.Less(LeafPhysics.DampFactor(5f, 2f, 0.1f), oneStep, "more drag slows faster");
        }

        [Test]
        public void SmoothedCellHeight_GrowsWithLeavesAndIsCapped()
        {
            float empty = LeafPhysics.SmoothedCellHeight(0, 0, 0, 0, 0, 0.012f, 1.1f);
            float some = LeafPhysics.SmoothedCellHeight(20, 10, 10, 10, 10, 0.012f, 1.1f);
            float lots = LeafPhysics.SmoothedCellHeight(5000, 5000, 5000, 5000, 5000, 0.012f, 1.1f);
            Assert.AreEqual(0.01f, empty, 1e-6f);
            Assert.AreEqual(0.01f + (20 * 0.5f + 40 * 0.125f) * 0.012f, some, 1e-5f);
            Assert.AreEqual(1.1f, lots, 1e-6f);
        }

        [Test]
        public void SampleGround_IsBilinearBetweenCorners()
        {
            var grid = LeafGrid.Create(Vector3.zero, new Vector2(2f, 2f), 1f); // 2 x 2 cells, 3 x 3 corners
            // Height = x coordinate in metres from the yard's left edge. Filled before wrapping:
            // a `using` variable's elements can't be assigned (CS1654).
            var heights = new float[9];
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                heights[y * 3 + x] = x;
            using var corners = new NativeArray<float>(heights, Allocator.Temp);

            Assert.AreEqual(0f, LeafPhysics.SampleGround(grid, corners, new float2(-1f, 0f)), 1e-5f);
            Assert.AreEqual(1f, LeafPhysics.SampleGround(grid, corners, new float2(0f, 0.3f)), 1e-5f);
            Assert.AreEqual(1.5f, LeafPhysics.SampleGround(grid, corners, new float2(0.5f, -0.7f)), 1e-5f);
            Assert.AreEqual(2f, LeafPhysics.SampleGround(grid, corners, new float2(5f, 5f)), 1e-5f, "clamped outside the yard");
        }

        [Test]
        public void LowestNeighbourDirection_PointsAtEmptiestNeighbour()
        {
            // 3 x 3 grid, centre cell full, right neighbour emptiest.
            using var counts = new NativeArray<int>(new[] { 5, 5, 5, 5, 40, 1, 5, 5, 5 }, Allocator.Temp);
            float2 dir = LeafPhysics.LowestNeighbourDirection(counts, 3, 3, 4, out int diff);
            Assert.AreEqual(new float2(1f, 0f), dir);
            Assert.AreEqual(39, diff);
        }

        [Test]
        public void LeafMatrix_MatchesUnityTrs()
        {
            var pos = new Vector3(1f, 2f, 3f);
            float yaw = 73f, tilt = -12f, scale = 0.25f;
            Matrix4x4 expected = Matrix4x4.TRS(pos, Quaternion.Euler(tilt, yaw, tilt * 0.5f), Vector3.one * scale);
            Matrix4x4 actual = LeafPhysics.LeafMatrix(pos, yaw, tilt, scale);
            for (int i = 0; i < 16; i++)
                Assert.AreEqual(expected[i], actual[i], 1e-4f, $"element {i}");
        }

        [Test]
        public void SphereInFrustum_RespectsPlanesAndRadius()
        {
            // One plane: keep everything with z >= 0.
            using var planes = new NativeArray<float4>(new[] { new float4(0f, 0f, 1f, 0f) }, Allocator.Temp);
            Assert.IsTrue(LeafPhysics.SphereInFrustum(planes, 1, new float3(0f, 0f, 1f), 0.1f));
            Assert.IsFalse(LeafPhysics.SphereInFrustum(planes, 1, new float3(0f, 0f, -1f), 0.1f));
            Assert.IsTrue(LeafPhysics.SphereInFrustum(planes, 1, new float3(0f, 0f, -0.05f), 0.1f), "touching counts");
            Assert.IsTrue(LeafPhysics.SphereInFrustum(planes, 0, new float3(0f, 0f, -100f), 0.1f), "no planes = no culling");
        }

        [Test]
        public void Morton2_InterleavesBits()
        {
            Assert.AreEqual(0u, LeafPhysics.Morton2(0, 0));
            Assert.AreEqual(1u, LeafPhysics.Morton2(1, 0));
            Assert.AreEqual(2u, LeafPhysics.Morton2(0, 1));
            Assert.AreEqual(3u, LeafPhysics.Morton2(1, 1));
            Assert.AreEqual(0b1010u, LeafPhysics.Morton2(0, 3));
        }

        [Test]
        public void Hash01_IsStableAndInRange()
        {
            for (uint i = 0; i < 1000; i++)
            {
                float h = LeafPhysics.Hash01(i);
                Assert.That(h, Is.InRange(0f, 1f));
                Assert.AreEqual(h, LeafPhysics.Hash01(i));
            }
        }
    }
}

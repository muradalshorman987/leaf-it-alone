using System.Linq;
using FallenYard.Levels;
using FallenYard.Tools;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Levels
{
    public class YardGreyboxTests
    {
        [Test]
        public void EveryYard_BuildsItsGreybox()
        {
            foreach (var def in YardLayouts.CreateAll())
            {
                var mats = YardMaterialSet.CreateRuntime(def.palette);
                var result = YardGreybox.Build(def, mats);
                try
                {
                    Assert.AreEqual(def.obstacles.Count, result.obstacleCount, def.yardId);
                    Assert.IsNotNull(result.ground, def.yardId);
                    Assert.IsNotNull(result.compostBin, def.yardId);
                    Assert.IsNotNull(result.mailbox, def.yardId);
                    Assert.IsNotNull(result.root.GetComponent<YardRestoration>(), def.yardId);
                    Assert.Greater(result.regrowth.Count, 0, def.yardId);
                    Assert.IsTrue(result.regrowth.All(r => !r.target.activeSelf), "flowers start hidden: " + def.yardId);

                    var bin = result.compostBin.transform.localPosition;
                    Assert.AreEqual(def.compostBinPosition.x, bin.x, 1e-4f);
                    Assert.AreEqual(def.compostBinPosition.y, bin.z, 1e-4f);
                    Assert.IsNotNull(result.compostBin.fillVisual);
                    Assert.IsNotNull(result.compostBin.readyIndicator);
                }
                finally
                {
                    Object.DestroyImmediate(result.root);
                    foreach (var m in mats.All()) Object.DestroyImmediate(m);
                    Object.DestroyImmediate(def);
                }
            }
        }

        [Test]
        public void FrontFence_LeavesAGapAtTheGate()
        {
            var def = YardLayouts.Create(YardLayouts.CornerLot);
            var result = YardGreybox.Build(def, new YardMaterialSet());
            try
            {
                float frontZ = -def.size.y * 0.5f - 0.3f;
                var gatePoint = new Vector3(def.gateX, 0.5f, frontZ);
                // Renderer bounds follow the transform at once (collider bounds wait for a physics sync).
                var segments = result.root.GetComponentsInChildren<Renderer>()
                    .Where(c => c.name == "FenceSegment").ToList();
                Assert.GreaterOrEqual(segments.Count, 5);
                foreach (var c in segments)
                    Assert.IsFalse(c.bounds.Contains(gatePoint), "fence blocks the gate");
                Assert.IsTrue(segments.Any(c => c.bounds.Contains(new Vector3(-def.size.x * 0.5f + 1f, 0.5f, frontZ))), "fence closes the rest of the front");
            }
            finally
            {
                Object.DestroyImmediate(result.root);
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void Paths_HaveNoCollider_HousesDo()
        {
            var def = YardLayouts.Create(YardLayouts.StarterLawn);
            var result = YardGreybox.Build(def, new YardMaterialSet());
            try
            {
                var props = result.root.transform.Find("Props");
                var path = props.Cast<Transform>().First(t => t.name.StartsWith("Path"));
                var house = props.Cast<Transform>().First(t => t.name.StartsWith("House"));
                Assert.AreEqual(0, path.GetComponentsInChildren<Collider>().Length);
                Assert.Greater(house.GetComponentsInChildren<Collider>().Length, 0);
            }
            finally
            {
                Object.DestroyImmediate(result.root);
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void FlowerClusters_ScaleWithYardSize()
        {
            Assert.AreEqual(6, YardGreybox.FlowerClusterCount(new Vector2(10f, 10f)));
            Assert.AreEqual(30, YardGreybox.FlowerClusterCount(new Vector2(100f, 100f)));
            Assert.Greater(YardGreybox.FlowerClusterCount(new Vector2(40f, 34f)), YardGreybox.FlowerClusterCount(new Vector2(24f, 20f)));
        }
    }
}

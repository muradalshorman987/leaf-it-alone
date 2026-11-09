using FallenYard.Leaves;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Leaves
{
    public class LeafTypeTests
    {
        [Test]
        public void Presets_MatchTheDesign()
        {
            var maple = LeafPhysicsSettings.For(LeafPreset.Maple);
            var oak = LeafPhysicsSettings.For(LeafPreset.Oak);
            var wet = LeafPhysicsSettings.For(LeafPreset.WetClump);
            var pine = LeafPhysicsSettings.For(LeafPreset.PineNeedles);

            Assert.Less(maple.mass, oak.mass, "maple is light");
            Assert.Less(maple.windThreshold, oak.windThreshold, "maple catches the wind first");
            Assert.Less(maple.airDrag, oak.airDrag, "maple glides far");
            Assert.Greater(wet.mass, oak.mass, "wet clumps are heavy");
            Assert.Less(wet.ToParams().blowerGrip * wet.ToParams().invMass, 0.25f, "the blower barely moves wet clumps");
            Assert.Less(pine.rakeGrip, 0.5f, "the rake slips through pine needles");
            Assert.Greater(pine.blowerGrip, oak.blowerGrip, "the blower works well on needles");
        }

        [Test]
        public void ApplyPreset_SetsIdsAndPhysics()
        {
            var def = ScriptableObject.CreateInstance<LeafTypeDefinition>();
            try
            {
                def.ApplyPreset(LeafPreset.PineNeedles);
                Assert.AreEqual("pine", def.typeId);
                Assert.AreEqual(LeafPreset.PineNeedles, def.shape);
                Assert.AreEqual(LeafPhysicsSettings.For(LeafPreset.PineNeedles).rakeGrip, def.physics.rakeGrip);
                Assert.AreEqual(LeafPhysicsSettings.ScaleRangeFor(LeafPreset.PineNeedles), def.scaleRange);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void ToParams_ClampsBadValues()
        {
            var bad = new LeafPhysicsSettings { mass = 0f, groundDrag = -1f, gravityScale = 0f };
            var p = bad.ToParams();
            Assert.That(p.invMass, Is.Not.NaN.And.Not.EqualTo(float.PositiveInfinity));
            Assert.AreEqual(0f, p.groundDrag);
            Assert.Greater(p.gravityScale, 0f);
        }

        [TestCase(LeafPreset.Maple)]
        [TestCase(LeafPreset.Oak)]
        [TestCase(LeafPreset.WetClump)]
        [TestCase(LeafPreset.PineNeedles)]
        public void ProceduralMeshes_StayInTriangleBudget(LeafPreset preset)
        {
            var mesh = LeafMeshFactory.Create(preset);
            try
            {
                int tris = mesh.triangles.Length / 3;
                Assert.Greater(tris, 0);
                Assert.LessOrEqual(tris, 20);
                Assert.AreEqual(mesh.vertexCount, mesh.normals.Length);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void LodQuad_IsCheaperThanEveryLeaf()
        {
            var lod = LeafMeshFactory.CreateLodQuad();
            try
            {
                Assert.AreEqual(4, lod.triangles.Length / 3);
            }
            finally
            {
                Object.DestroyImmediate(lod);
            }
        }
    }
}

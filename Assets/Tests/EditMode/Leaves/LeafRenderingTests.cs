using FallenYard.Leaves;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Leaves
{
    public class LeafRenderingTests
    {
        LeafField _field;
        Camera _cam;

        [TearDown]
        public void TearDown()
        {
            TestUtil.DestroyLeafField(_field);
            if (_cam != null) Object.DestroyImmediate(_cam.gameObject);
        }

        Camera MakeCamera(Vector3 pos, Vector3 lookAt)
        {
            _cam = new GameObject("Cull camera (test)").AddComponent<Camera>();
            _cam.transform.position = pos;
            _cam.transform.LookAt(lookAt);
            _cam.fieldOfView = 60f;
            _cam.aspect = 1f;
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 500f;
            return _cam;
        }

        [Test]
        public void Cull_WithoutPlanes_DrawsEveryLiveLeaf()
        {
            _field = TestUtil.CreateLeafField(new Vector2(20f, 20f), 0.5f, 5000);
            _field.lodDistance = 0f;
            _field.cullDistance = 0f;
            var stats = _field.CullAndBuildMatrices(Vector3.zero, null);
            Assert.AreEqual(5000, stats.visibleNear);
            Assert.AreEqual(0, stats.visibleFar);
        }

        [Test]
        public void Cull_SkipsCollectedLeaves()
        {
            _field = TestUtil.CreateLeafField(new Vector2(4f, 4f), 0.5f, 1000);
            _field.cullDistance = 0f;
            int got = _field.CollectInRadius(Vector3.zero, 1f);
            var stats = _field.CullAndBuildMatrices(Vector3.zero, null);
            Assert.AreEqual(1000 - got, stats.Visible);
        }

        [Test]
        public void Cull_FrustumDropsLeavesBehindTheCamera()
        {
            _field = TestUtil.CreateLeafField(new Vector2(20f, 20f), 0.5f, 5000);
            _field.cullDistance = 0f;
            // Standing at the yard's centre looking +X: roughly the half behind is culled.
            var cam = MakeCamera(new Vector3(0f, 1.6f, 0f), new Vector3(10f, 0f, 0f));
            var stats = _field.CullAndBuildMatrices(cam.transform.position, GeometryUtility.CalculateFrustumPlanes(cam));
            Assert.Greater(stats.Visible, 0);
            Assert.Less(stats.Visible, 5000 / 2);
        }

        [Test]
        public void Cull_SplitsNearAndFarByLodDistance()
        {
            _field = TestUtil.CreateLeafField(new Vector2(40f, 40f), 0.5f, 6000);
            _field.lodDistance = 10f;
            _field.cullDistance = 0f;
            var stats = _field.CullAndBuildMatrices(Vector3.zero, null);
            Assert.Greater(stats.visibleNear, 0);
            Assert.Greater(stats.visibleFar, stats.visibleNear, "most of a 40 m yard is past 10 m");
            Assert.AreEqual(6000, stats.Visible);
        }

        [Test]
        public void Cull_DropsLeavesPastCullDistance()
        {
            _field = TestUtil.CreateLeafField(new Vector2(40f, 40f), 0.5f, 6000);
            _field.lodDistance = 0f;
            _field.cullDistance = 5f;
            var stats = _field.CullAndBuildMatrices(Vector3.zero, null);
            Assert.Greater(stats.Visible, 0);
            Assert.Less(stats.Visible, 6000 / 10);
        }
    }
}

using FallenYard.Leaves;
using NUnit.Framework;
using UnityEngine;

namespace FallenYard.Tests.Leaves
{
    public class PileDetectorTests
    {
        LeafField _field;
        PileDetector _detector;

        [SetUp]
        public void SetUp()
        {
            _detector = new GameObject("PileDetector (test)").AddComponent<PileDetector>();
            _detector.denseCellThreshold = 10;
            _detector.minLeavesPerPile = 60;
        }

        [TearDown]
        public void TearDown()
        {
            TestUtil.Destroy(_detector);
            TestUtil.DestroyLeafField(_field);
        }

        [Test]
        public void Scan_WithNoLeafField_FindsNothing()
        {
            _detector.Scan();
            Assert.AreEqual(0, _detector.Piles.Count);
            Assert.AreEqual(0, _detector.LeavesInPiles);
        }

        [Test]
        public void Scan_DenseSmallYard_FindsOnePileHoldingEveryLeaf()
        {
            // 2 x 2 m yard, 0.5 m cells = 16 cells, ~62 leaves each: every cell is dense.
            _field = TestUtil.CreateLeafField(new Vector2(2f, 2f), 0.5f, 1000);
            _detector.Scan();

            Assert.AreEqual(1, _detector.Piles.Count);
            var pile = _detector.Piles[0];
            Assert.AreEqual(1000, pile.leafCount);
            Assert.AreEqual(1000, _detector.LeavesInPiles);
            Assert.AreEqual(16, pile.cellCount);
            Assert.Less(new Vector2(pile.center.x, pile.center.z).magnitude, 0.25f, "pile centre should be near the yard centre");
            Assert.Greater(pile.radius, 0f);
        }

        [Test]
        public void Scan_SparseYard_FindsNoPiles()
        {
            // 30 x 30 m, 0.5 m cells = 3600 cells for 300 leaves: nowhere near 10 per cell.
            _field = TestUtil.CreateLeafField(new Vector2(30f, 30f), 0.5f, 300);
            _detector.Scan();
            Assert.AreEqual(0, _detector.Piles.Count);
        }

        [Test]
        public void Scan_ClusterBelowMinimum_IsNotAPile()
        {
            _field = TestUtil.CreateLeafField(new Vector2(2f, 2f), 0.5f, 1000);
            _detector.minLeavesPerPile = 2000;
            _detector.Scan();
            Assert.AreEqual(0, _detector.Piles.Count);
            Assert.AreEqual(0, _detector.LeavesInPiles);
        }

        [Test]
        public void Scan_AfterCollectingEverything_FindsNoPiles()
        {
            _field = TestUtil.CreateLeafField(new Vector2(2f, 2f), 0.5f, 1000);
            int collected = _field.CollectInRadius(Vector3.zero, 5f);
            Assert.AreEqual(1000, collected);

            _detector.Scan();
            Assert.AreEqual(0, _detector.Piles.Count);
        }

        [Test]
        public void TryGetPileNear_FindsPileCloseByAndIgnoresFarPoints()
        {
            _field = TestUtil.CreateLeafField(new Vector2(2f, 2f), 0.5f, 1000);
            _detector.Scan();

            Assert.IsTrue(_detector.TryGetPileNear(Vector3.zero, 0.5f, out var pile));
            Assert.AreEqual(1000, pile.leafCount);
            Assert.IsFalse(_detector.TryGetPileNear(new Vector3(50f, 0f, 50f), 0.5f, out _));
        }
    }
}

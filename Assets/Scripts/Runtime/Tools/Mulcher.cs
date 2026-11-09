using FallenYard.Leaves;
using FallenYard.Progression;
using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>
    /// Aim at a pile and hold LMB to chew it up. When the meter fills the pile
    /// is shredded straight into compost: it pays like bagging, and each leaf
    /// counts extra toward the compost bin because mulch breaks down faster.
    /// </summary>
    public class Mulcher : LeafTool
    {
        [Header("Mulching")]
        [Tooltip("Seconds of holding LMB to shred a pile (shortened by Strength upgrades).")]
        public float secondsPerPile = 1.6f;
        [Tooltip("How fast the meter drains when you let go, per second.")]
        public float drainPerSecond = 1.5f;
        [Tooltip("Most leaves shredded per pile (scaled by Capacity upgrades).")]
        public int basePileCapacity = 250;
        [Tooltip("Coins paid per shredded leaf.")]
        public float coinsPerLeaf = 0.5f;
        [Tooltip("Each mulched leaf counts this many times toward the compost bin.")]
        public float compostMultiplier = 2f;
        [Tooltip("How close (m) to a pile's edge the aim point must be.")]
        public float pileSnapDistance = 0.6f;
        [Tooltip("Speed (m/s) leaves are drawn into the mulcher while it runs.")]
        public float pullSpeed = 1.2f;

        /// <summary>0..1 progress on the current pile, for the HUD.</summary>
        public float Progress => _meter.Value;
        public bool HasTarget { get; private set; }

        public override string StatusText => HasTarget
            ? (Progress > 0f ? $"Mulching... {Mathf.RoundToInt(Progress * 100f)}%" : "Hold LMB to mulch this pile")
            : "";

        /// <summary>Pile finder; defaults to PileDetector. Tests can replace it.</summary>
        [System.NonSerialized]
        public System.Func<Vector3, float, (bool found, Vector3 center, float radius)> findPile = DefaultFindPile;

        ChargeMeter _meter;
        readonly LeafPayout _payout = new LeafPayout();

        int PileCapacity => Mathf.Max(1, Mathf.RoundToInt(basePileCapacity * Mult(UpgradeStat.Capacity)));

        void Reset()
        {
            toolId = "mulcher";
            displayName = "Mulcher";
            baseRadius = 1f;
            baseStrength = 1f;
            baseRange = 3f;
        }

        public override void OnUnequip()
        {
            base.OnUnequip();
            _meter.Reset();
            HasTarget = false;
        }

        public override void Tick(Camera cam, bool primaryHeld, float dt)
        {
            var leaves = LeafTarget;
            HasTarget = false;
            if (leaves == null || !TryGetGroundPoint(cam, Range, out Vector3 aim))
            {
                _meter.Tick(false, dt, secondsPerPile, drainPerSecond);
                Throttle = 0f;
                return;
            }

            bool found = false;
            Vector3 center = default;
            float radius = 0f;
            if (findPile != null) (found, center, radius) = findPile(aim, pileSnapDistance);
            HasTarget = found;
            bool running = primaryHeld && found;
            Throttle = running ? 1f : 0f;

            if (running)
                leaves.Push(center, radius, FlatToward(center, aim) * pullSpeed, 4f, dt);

            float seconds = secondsPerPile / Mathf.Max(0.1f, Mult(UpgradeStat.Strength));
            if (!_meter.Tick(running, dt, seconds, drainPerSecond)) return;

            int shredded = leaves.CollectInRadius(center, radius, PileCapacity);
            if (shredded <= 0) return;
            _payout.Pay(shredded, coinsPerLeaf);
            // Bagging already adds 1x to the bin through PileBagged; add the mulch bonus on top.
            int bonus = Mathf.RoundToInt(shredded * Mathf.Max(0f, compostMultiplier - 1f));
            if (bonus > 0 && CompostBin.Instance != null) CompostBin.Instance.AddLeaves(bonus);
            if (PileDetector.Instance != null) PileDetector.Instance.Scan();
        }

        static (bool found, Vector3 center, float radius) DefaultFindPile(Vector3 point, float maxDistance)
        {
            var detector = PileDetector.Instance;
            if (detector != null && detector.TryGetPileNear(point, maxDistance, out var pile))
                return (true, pile.center, pile.radius);
            return (false, Vector3.zero, 0f);
        }
    }
}

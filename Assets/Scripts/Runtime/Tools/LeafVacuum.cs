using FallenYard.Progression;
using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>
    /// Hold LMB to suck up leaves around the nozzle into a hopper. Good for
    /// fine, scattered leaves that are hard to pile. When the hopper is full it
    /// stops sucking; empty it at the compost bin with E (or anywhere, if the
    /// yard has no bin) to get paid.
    /// </summary>
    public class LeafVacuum : LeafTool
    {
        [Header("Suction")]
        [Tooltip("Leaves per second the nozzle can swallow at full power (scaled by Strength upgrades).")]
        public float leavesPerSecond = 90f;
        [Tooltip("Leaves within this many nozzle radii drift toward you before being sucked in.")]
        public float pullRadiusMultiplier = 2.2f;
        [Tooltip("Drift speed (m/s) of leaves around the nozzle.")]
        public float pullSpeed = 2.5f;

        [Header("Hopper")]
        [Tooltip("Leaves the hopper holds before it must be emptied (scaled by Capacity upgrades).")]
        public int baseHopperCapacity = 120;
        [Tooltip("Coins paid per leaf when the hopper is emptied.")]
        public float coinsPerLeaf = 0.5f;

        [Header("Motor")]
        [Tooltip("Seconds from idle to full suction.")]
        public float spinUpSeconds = 0.35f;
        [Tooltip("Seconds from full suction back to idle.")]
        public float spinDownSeconds = 0.6f;

        public LeafHopper Hopper => _hopper ??= new LeafHopper(HopperCapacity);
        public int HopperCapacity => Mathf.Max(1, Mathf.RoundToInt(baseHopperCapacity * Mult(UpgradeStat.Capacity)));

        public override string StatusText => Hopper.IsFull
            ? $"Hopper full ({Hopper.Count}) - empty it at the compost bin"
            : $"Hopper {Hopper.Count}/{Hopper.Capacity}";

        LeafHopper _hopper;
        ThrottleRamp _motor;
        float _suckBudget;
        readonly LeafPayout _payout = new LeafPayout();

        void Reset()
        {
            toolId = "vacuum";
            displayName = "Leaf Vacuum";
            baseRadius = 0.55f;
            baseStrength = 1f;
            baseRange = 2.2f;
        }

        public override void OnUnequip()
        {
            base.OnUnequip();
            _motor.Reset();
            _suckBudget = 0f;
        }

        public override void Tick(Camera cam, bool primaryHeld, float dt)
        {
            Hopper.SetCapacity(HopperCapacity);
            // A full hopper chokes the motor.
            Throttle = _motor.Tick(primaryHeld && !Hopper.IsFull, dt, spinUpSeconds, spinDownSeconds);
            var leaves = LeafTarget;
            if (Throttle <= 0f || leaves == null || !TryGetGroundPoint(cam, Range, out Vector3 nozzle))
            {
                _suckBudget = 0f;
                return;
            }

            float power = _motor.Eased;
            leaves.Push(nozzle, Radius * pullRadiusMultiplier, FlatToward(nozzle, cam.transform.position) * pullSpeed,
                6f * power, dt);

            float rate = leavesPerSecond * Mult(UpgradeStat.Strength) * power;
            // Cap the budget so pointing at bare grass doesn't bank a burst for later.
            _suckBudget = Mathf.Min(_suckBudget + rate * dt, Mathf.Max(1f, rate * 0.1f));
            int want = Mathf.Min(Mathf.FloorToInt(_suckBudget), Hopper.Space);
            if (want <= 0) return;

            int got = leaves.CollectInRadius(nozzle, Radius, want);
            _suckBudget -= want;
            Hopper.Add(got);
        }

        /// <summary>Empties the hopper, pays for the leaves and returns how many there were.</summary>
        public int EmptyHopper()
        {
            int n = Hopper.Empty();
            if (n > 0) _payout.Pay(n, coinsPerLeaf);
            return n;
        }
    }
}

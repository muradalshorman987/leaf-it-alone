using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>
    /// The handful of leaf operations the tools need. Tools only talk to leaves
    /// through this, so they can be unit-tested with a fake and so WP2 can change
    /// the simulation (Burst/Jobs, leaf types) without touching tool code.
    /// <see cref="LeafFieldTarget"/> adapts the current LeafField; once WP2's
    /// ILeafQuery lands, that adapter wraps it instead.
    /// </summary>
    public interface ILeafTarget
    {
        /// <summary>Leaves inside radius blend their velocity toward targetVelocity.</summary>
        void Push(Vector3 center, float radius, Vector3 targetVelocity, float strength, float dt);

        /// <summary>Cone push away from origin along dir, with lift so leaves flutter.</summary>
        void BlowCone(Vector3 origin, Vector3 dir, float range, float halfAngleDeg, float force, float lift, float dt);

        int CountInRadius(Vector3 center, float radius);

        /// <summary>Removes up to maxLeaves leaves inside radius from the yard. Returns how many.</summary>
        int CollectInRadius(Vector3 center, float radius, int maxLeaves);

        float GroundHeightAt(Vector3 world);
    }

    /// <summary>Resolves the leaf target tools use: the live LeafField, or a test override.</summary>
    public static class LeafTargets
    {
        /// <summary>Set by tests (or a future yard loader) to replace the LeafField.</summary>
        public static ILeafTarget Override { get; set; }

        static readonly LeafFieldTarget FieldTarget = new LeafFieldTarget();

        /// <summary>The active target, or null when there are no leaves in the scene.</summary>
        public static ILeafTarget Current
        {
            get
            {
                if (Override != null) return Override;
                return Leaves.LeafField.Instance != null ? FieldTarget : null;
            }
        }
    }

    /// <summary>Adapter from <see cref="ILeafTarget"/> to LeafField.Instance.</summary>
    public sealed class LeafFieldTarget : ILeafTarget
    {
        static Leaves.LeafField F => Leaves.LeafField.Instance;

        public void Push(Vector3 center, float radius, Vector3 targetVelocity, float strength, float dt) =>
            F?.Push(center, radius, targetVelocity, strength, dt);

        public void BlowCone(Vector3 origin, Vector3 dir, float range, float halfAngleDeg, float force, float lift, float dt) =>
            F?.BlowCone(origin, dir, range, halfAngleDeg, force, lift, dt);

        public int CountInRadius(Vector3 center, float radius) => F != null ? F.CountInRadius(center, radius) : 0;

        public int CollectInRadius(Vector3 center, float radius, int maxLeaves) =>
            F != null ? F.CollectInRadius(center, radius, maxLeaves) : 0;

        public float GroundHeightAt(Vector3 world) => F != null ? F.GroundHeightAt(world) : 0f;
    }
}

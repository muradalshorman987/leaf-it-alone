using UnityEngine;

namespace FallenYard.Leaves
{
    /// <summary>
    /// What other packages (tools, progression, tutorial, audio) may ask of the leaves.
    /// Owned by WP2. <see cref="LeafField"/> implements it; reach it through
    /// <see cref="LeafField.Query"/> rather than the concrete class where you can.
    /// All positions are world space; only X and Z matter for radius tests.
    /// </summary>
    public interface ILeafQuery
    {
        int TotalLeaves { get; }
        int CollectedLeaves { get; }
        int RemainingLeaves { get; }
        /// <summary>Fraction of leaves bagged, 0..1.</summary>
        float CleanFraction { get; }
        /// <summary>Leaves sliding or airborne after the last step. Handy for rustle audio.</summary>
        int MovingLeaves { get; }

        float GroundHeightAt(Vector3 world);
        bool IsInsideYard(Vector3 world);

        int CountInRadius(Vector3 center, float radius);

        /// <summary>Rake-style push toward targetVelocity. Returns how many leaves were touched.</summary>
        int Push(Vector3 center, float radius, Vector3 targetVelocity, float strength, float dt);

        /// <summary>Blower cone from origin along dir; pushes and lifts. Returns how many leaves were touched.</summary>
        int BlowCone(Vector3 origin, Vector3 dir, float range, float halfAngleDeg, float force, float lift, float dt);

        /// <summary>Suction toward center (vacuum). Returns how many leaves were touched.</summary>
        int Attract(Vector3 center, float radius, float speed, float strength, float dt);

        /// <summary>Removes up to maxLeaves leaves inside the radius. Returns how many.</summary>
        int CollectInRadius(Vector3 center, float radius, int maxLeaves = int.MaxValue);
    }
}

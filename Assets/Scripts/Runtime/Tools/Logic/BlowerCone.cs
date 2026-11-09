using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>One cone pass handed to <see cref="ILeafTarget.BlowCone"/>.</summary>
    public struct ConeLayer
    {
        public float halfAngle;
        public float force;
        public float lift;
    }

    /// <summary>
    /// Shapes the blower's cone falloff. The leaf field already fades force
    /// linearly with distance and angle; layering a narrow, strong core over a
    /// wide, soft skirt makes the air stream feel focused in the middle while
    /// still nudging leaves at the edges.
    /// </summary>
    public static class BlowerCone
    {
        /// <param name="halfAngle">Outer cone half angle in degrees.</param>
        /// <param name="force">Total force on the centre line.</param>
        /// <param name="lift">Upward kick on the centre line.</param>
        /// <param name="coreAngleFraction">Core half angle as a fraction of the outer one (0..1).</param>
        /// <param name="coreShare">Share of the centre-line force carried by the core (0..1).</param>
        public static void Layers(float halfAngle, float force, float lift, float coreAngleFraction, float coreShare,
            out ConeLayer core, out ConeLayer skirt)
        {
            coreShare = Mathf.Clamp01(coreShare);
            coreAngleFraction = Mathf.Clamp(coreAngleFraction, 0.05f, 1f);
            core = new ConeLayer
            {
                halfAngle = halfAngle * coreAngleFraction,
                force = force * coreShare,
                lift = lift,
            };
            skirt = new ConeLayer
            {
                halfAngle = halfAngle,
                force = force * (1f - coreShare),
                lift = lift * 0.6f,
            };
        }

        /// <summary>
        /// Centre-line force of the combined layers at a given angle off-axis,
        /// using the same linear angular falloff as the leaf field. Handy for tests and gizmos.
        /// </summary>
        public static float ForceAtAngle(float angle, in ConeLayer core, in ConeLayer skirt)
        {
            float f = 0f;
            if (angle <= core.halfAngle && core.halfAngle > 0f) f += core.force * (1f - angle / core.halfAngle);
            if (angle <= skirt.halfAngle && skirt.halfAngle > 0f) f += skirt.force * (1f - angle / skirt.halfAngle);
            return f;
        }
    }
}

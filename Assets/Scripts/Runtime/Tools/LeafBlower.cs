using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>
    /// Hold LMB to blow leaves away in a cone where you are looking. The motor
    /// spins up and down, and the cone is a strong core inside a soft skirt.
    /// </summary>
    public class LeafBlower : LeafTool
    {
        [Header("Cone")]
        [Tooltip("Outer cone half angle in degrees (widened by Radius upgrades).")]
        public float coneHalfAngle = 22f;
        [Tooltip("Upward kick so leaves flutter.")]
        public float liftSpeed = 1.6f;
        [Tooltip("Core cone half angle as a fraction of the outer cone.")]
        [Range(0.1f, 1f)] public float coreAngleFraction = 0.4f;
        [Tooltip("Share of the centre-line force carried by the core. Higher = more focused stream.")]
        [Range(0f, 1f)] public float coreShare = 0.55f;

        [Header("Motor")]
        [Tooltip("Seconds from idle to full power.")]
        public float spinUpSeconds = 0.25f;
        [Tooltip("Seconds from full power back to idle after release.")]
        public float spinDownSeconds = 0.5f;
        [Tooltip("Nozzle offset from the eyes (right, up, forward), metres.")]
        public Vector3 nozzleOffset = new Vector3(0f, -0.5f, 0.4f);

        ThrottleRamp _motor;

        void Reset()
        {
            toolId = "blower";
            displayName = "Leaf Blower";
            baseRadius = 1f;
            baseStrength = 22f;
            baseRange = 5f;
        }

        public override void OnUnequip()
        {
            base.OnUnequip();
            _motor.Reset();
        }

        public override void Tick(Camera cam, bool primaryHeld, float dt)
        {
            Throttle = _motor.Tick(primaryHeld, dt, spinUpSeconds, spinDownSeconds);
            var leaves = LeafTarget;
            if (Throttle <= 0f || leaves == null) return;

            var t = cam.transform;
            Vector3 origin = t.position + t.right * nozzleOffset.x + t.up * nozzleOffset.y + t.forward * nozzleOffset.z;
            float halfAngle = coneHalfAngle * Mathf.Sqrt(Mult(Progression.UpgradeStat.Radius));
            float power = _motor.Eased;

            BlowerCone.Layers(halfAngle, Strength * power, liftSpeed * power, coreAngleFraction, coreShare,
                out var core, out var skirt);
            if (core.force > 0f) leaves.BlowCone(origin, t.forward, Range, core.halfAngle, core.force, core.lift, dt);
            if (skirt.force > 0f) leaves.BlowCone(origin, t.forward, Range, skirt.halfAngle, skirt.force, skirt.lift, dt);
        }
    }
}

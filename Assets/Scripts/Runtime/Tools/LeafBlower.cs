using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>Hold LMB to blow leaves away in a cone where you are looking.</summary>
    public class LeafBlower : LeafTool
    {
        public float coneHalfAngle = 22f;
        [Tooltip("Upward kick so leaves flutter.")]
        public float liftSpeed = 1.6f;

        void Reset()
        {
            toolId = "blower";
            displayName = "Leaf Blower";
            baseRadius = 1f;
            baseStrength = 22f;
            baseRange = 5f;
        }

        public override void Tick(Camera cam, bool primaryHeld, float dt)
        {
            if (!primaryHeld || Field == null) return;
            // Nozzle sits a little below and in front of the eyes.
            Vector3 origin = cam.transform.position - cam.transform.up * 0.5f + cam.transform.forward * 0.4f;
            float halfAngle = coneHalfAngle * Mathf.Sqrt(Mult(Progression.UpgradeStat.Radius));
            Field.BlowCone(origin, cam.transform.forward, Range, halfAngle, Strength, liftSpeed, dt);
        }
    }
}

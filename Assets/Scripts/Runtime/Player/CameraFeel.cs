using UnityEngine;

namespace FallenYard.Player
{
    /// <summary>Head bob and field-of-view maths for the first-person camera, kept testable.</summary>
    public static class CameraFeel
    {
        /// <summary>
        /// Vertical/side camera offset for a bob phase (radians). Height uses |sin| so
        /// each footstep is a dip; sideways sway runs at half the step rate.
        /// </summary>
        public static Vector3 BobOffset(float phase, float amplitude, float intensity)
        {
            intensity = Mathf.Clamp01(intensity);
            float y = -Mathf.Abs(Mathf.Sin(phase)) * amplitude;
            float x = Mathf.Cos(phase) * amplitude * 0.5f;
            return new Vector3(x, y, 0f) * intensity;
        }

        /// <summary>Advances a bob phase by distance walked. stepLength = metres per footstep.</summary>
        public static float AdvancePhase(float phase, float speed, float dt, float stepLength)
        {
            if (stepLength <= 0f) return phase;
            phase += speed * dt / stepLength * Mathf.PI;
            return phase % (Mathf.PI * 4f);
        }

        /// <summary>Target FOV: base plus a sprint kick scaled by how fast we move between walk and sprint.</summary>
        public static float TargetFov(float baseFov, float sprintKick, float speed, float walkSpeed, float sprintSpeed)
        {
            float t = sprintSpeed > walkSpeed ? Mathf.Clamp01((speed - walkSpeed) / (sprintSpeed - walkSpeed)) : 0f;
            return baseFov + sprintKick * t;
        }

        /// <summary>Frame-rate independent ease toward a target.</summary>
        public static float Damp(float current, float target, float sharpness, float dt) =>
            Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * dt));
    }
}

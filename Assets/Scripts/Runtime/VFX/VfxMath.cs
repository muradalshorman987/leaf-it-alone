using UnityEngine;

namespace FallenYard.VFX
{
    /// <summary>Small formulas behind the particle effects, kept here so they are testable.</summary>
    public static class VfxMath
    {
        /// <summary>
        /// Turns a rate (per second) into a whole number of particles this frame,
        /// carrying the fraction so low rates still emit at the right average.
        /// </summary>
        public static int Accumulate(ref float carry, float ratePerSecond, float dt)
        {
            if (ratePerSecond <= 0f || dt <= 0f)
            {
                carry = 0f;
                return 0;
            }
            carry += ratePerSecond * dt;
            int n = Mathf.FloorToInt(carry);
            carry -= n;
            return n;
        }

        /// <summary>Blower air streaks per second. Eased so feathering the trigger gives a wisp, not a jet.</summary>
        public static float BlowerRate(float throttle, float maxRate) =>
            Mathf.Max(0f, maxRate) * Mathf.Pow(Mathf.Clamp01(throttle), 1.5f);

        /// <summary>Dust puffs per second where a tool scuffs the ground.</summary>
        public static float DustRate(float throttle, float maxRate) =>
            Mathf.Max(0f, maxRate) * Mathf.Clamp01(throttle);

        /// <summary>Leaves thrown up by bagging a pile: grows with the pile but stays bounded.</summary>
        public static int PoofCount(int leavesBagged, int min, int max)
        {
            if (leavesBagged <= 0) return 0;
            int n = Mathf.RoundToInt(min + Mathf.Sqrt(leavesBagged) * 2.5f);
            return Mathf.Clamp(n, min, Mathf.Max(min, max));
        }

        /// <summary>
        /// Loose leaves drifting through the air per second, from wind strength (m/s),
        /// the weather's airborne amount (0..1) and the gust flash (0..1).
        /// </summary>
        public static float AirborneRate(float windStrength, float airborne01, float gustFlash01, float maxRate)
        {
            float wind = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 5f, windStrength));
            float amount = Mathf.Clamp01(airborne01) * (0.3f + 0.7f * wind) + Mathf.Clamp01(gustFlash01) * 0.7f;
            return Mathf.Max(0f, maxRate) * Mathf.Clamp01(amount);
        }

        /// <summary>A direction inside a cone around <paramref name="forward"/> (half angle in degrees), from two 0..1 randoms.</summary>
        public static Vector3 ConeDirection(Vector3 forward, float halfAngleDegrees, float u, float v)
        {
            if (forward.sqrMagnitude < 1e-8f) forward = Vector3.forward;
            forward.Normalize();
            float cosMax = Mathf.Cos(Mathf.Clamp(halfAngleDegrees, 0f, 179f) * Mathf.Deg2Rad);
            float cos = Mathf.Lerp(1f, cosMax, Mathf.Clamp01(u));
            float sin = Mathf.Sqrt(Mathf.Max(0f, 1f - cos * cos));
            float phi = Mathf.Clamp01(v) * Mathf.PI * 2f;
            Vector3 local = new Vector3(Mathf.Cos(phi) * sin, Mathf.Sin(phi) * sin, cos);
            return Quaternion.LookRotation(forward, Mathf.Abs(forward.y) > 0.99f ? Vector3.right : Vector3.up) * local;
        }
    }
}

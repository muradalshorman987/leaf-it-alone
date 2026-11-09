using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>
    /// Rake feel maths, kept free of MonoBehaviour so it can be tested.
    /// Slow, deliberate strokes grip the leaves; quick flicks slip over them.
    /// Pulling toward yourself gathers leaves harder than pushing them away.
    /// </summary>
    public static class RakeStroke
    {
        /// <summary>Default grip by normalized head speed: full grip until half speed, then slips to ~55%.</summary>
        public static AnimationCurve DefaultGripCurve() => new AnimationCurve(
            new Keyframe(0f, 1f, 0f, 0f),
            new Keyframe(0.5f, 1f, 0f, -0.6f),
            new Keyframe(1f, 0.55f, -1.2f, 0f));

        /// <summary>
        /// Target leaf velocity for this frame.
        /// </summary>
        /// <param name="headVelocity">Rake head velocity on the ground (y ignored).</param>
        /// <param name="toPlayer">Flat unit vector from the head toward the player.</param>
        /// <param name="grip">0..1 from the grip curve.</param>
        /// <param name="dragFollow">How much leaves follow the head motion.</param>
        /// <param name="pullSpeed">Base pull toward the player, m/s.</param>
        /// <param name="towardBonus">Extra follow (fraction) when the stroke points at the player.</param>
        public static Vector3 TargetVelocity(Vector3 headVelocity, Vector3 toPlayer, float grip,
            float dragFollow, float pullSpeed, float towardBonus)
        {
            headVelocity.y = 0f;
            toPlayer.y = 0f;
            float toward = 0f;
            if (headVelocity.sqrMagnitude > 1e-4f && toPlayer.sqrMagnitude > 1e-4f)
                toward = Mathf.Max(0f, Vector3.Dot(headVelocity.normalized, toPlayer.normalized));
            float follow = dragFollow * grip * (1f + towardBonus * toward);
            return headVelocity * follow + toPlayer * pullSpeed;
        }

        /// <summary>Effort 0..1 for audio: a resting rake still scrapes a little.</summary>
        public static float Throttle(float headSpeed, float maxHeadSpeed, float idle = 0.15f) =>
            Mathf.Lerp(idle, 1f, Mathf.Clamp01(headSpeed / Mathf.Max(0.01f, maxHeadSpeed)));
    }
}

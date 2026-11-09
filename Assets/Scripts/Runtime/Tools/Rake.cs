using FallenYard.Core;
using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>
    /// Hold LMB and sweep your view: leaves under the rake head follow the
    /// head's motion and are gently drawn toward you, which builds piles.
    /// Slow strokes grip, fast flicks slip, and strokes toward you gather harder.
    /// </summary>
    public class Rake : LeafTool
    {
        [Header("Rake feel")]
        [Tooltip("How much leaves follow the rake head's movement.")]
        public float dragFollow = 1.1f;
        [Tooltip("Speed (m/s) leaves are pulled toward the player while raking.")]
        public float pullSpeed = 1.4f;
        [Tooltip("Grip (y, 0..1) by head speed (x, 0..1 of Max Head Speed). Fast flicks slip over leaves.")]
        public AnimationCurve gripCurve = RakeStroke.DefaultGripCurve();
        [Tooltip("Head speed (m/s) treated as a full-speed stroke. Faster motion is clamped.")]
        public float maxHeadSpeed = 8f;
        [Tooltip("Extra follow when the stroke points toward you (0.5 = 50% more).")]
        public float towardBonus = 0.5f;
        [Tooltip("Seconds to smooth the head velocity over, so mouse jitter doesn't shake leaves.")]
        public float velocitySmoothing = 0.06f;

        Vector3 _lastHead;
        Vector3 _smoothedVelocity;
        bool _hasHead;

        void Reset()
        {
            toolId = "rake";
            displayName = "Rake";
            baseRadius = 0.9f;
            baseStrength = 10f;
            baseRange = 2.6f;
        }

        public override void OnUnequip()
        {
            base.OnUnequip();
            _hasHead = false;
        }

        public override void Tick(Camera cam, bool primaryHeld, float dt)
        {
            var leaves = LeafTarget;
            if (!primaryHeld || leaves == null || !TryGetGroundPoint(cam, Range, out Vector3 head))
            {
                _hasHead = false;
                _smoothedVelocity = Vector3.zero;
                Throttle = 0f;
                return;
            }

            Vector3 raw = _hasHead ? (head - _lastHead) / Mathf.Max(dt, 1e-4f) : Vector3.zero;
            raw.y = 0f;
            raw = Vector3.ClampMagnitude(raw, maxHeadSpeed);
            float k = velocitySmoothing <= 0f ? 1f : 1f - Mathf.Exp(-dt / velocitySmoothing);
            _smoothedVelocity = Vector3.Lerp(_smoothedVelocity, raw, k);
            _lastHead = head;
            _hasHead = true;

            float speed = _smoothedVelocity.magnitude;
            float grip = Mathf.Clamp01(gripCurve != null && gripCurve.length > 0
                ? gripCurve.Evaluate(speed / Mathf.Max(0.01f, maxHeadSpeed))
                : 1f);

            Vector3 target = RakeStroke.TargetVelocity(_smoothedVelocity, FlatToward(head, cam.transform.position),
                grip, dragFollow, pullSpeed, towardBonus);
            leaves.Push(head, Radius, target, Strength * Mathf.Lerp(0.6f, 1f, grip), dt);
            Throttle = RakeStroke.Throttle(speed, maxHeadSpeed);
        }
    }
}

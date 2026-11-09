using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>
    /// Hold LMB and sweep your view: leaves under the rake head follow the
    /// head's motion and are gently drawn toward you, which builds piles.
    /// </summary>
    public class Rake : LeafTool
    {
        [Tooltip("How much leaves follow the rake head's movement.")]
        public float dragFollow = 1.1f;
        [Tooltip("Speed (m/s) leaves are pulled toward the player while raking.")]
        public float pullSpeed = 1.4f;

        Vector3 _lastHead;
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
            if (!primaryHeld || Field == null || !TryGetGroundPoint(cam, Range, out Vector3 head))
            {
                _hasHead = false;
                return;
            }

            Vector3 headVelocity = _hasHead ? (head - _lastHead) / Mathf.Max(dt, 1e-4f) : Vector3.zero;
            headVelocity = Vector3.ClampMagnitude(headVelocity, 8f);
            _lastHead = head;
            _hasHead = true;

            Vector3 toPlayer = cam.transform.position - head;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 1e-4f) toPlayer.Normalize();

            Vector3 target = headVelocity * dragFollow + toPlayer * pullSpeed;
            Field.Push(head, Radius, target, Strength, dt);
        }
    }
}

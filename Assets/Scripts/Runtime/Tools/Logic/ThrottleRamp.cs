using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>
    /// Motor-style throttle: eases up to 1 while the trigger is held and coasts
    /// back to 0 when released, so engines spin up and wind down instead of snapping.
    /// </summary>
    public struct ThrottleRamp
    {
        public float Value { get; private set; }

        /// <param name="held">Trigger held this frame.</param>
        /// <param name="spinUpSeconds">Time from 0 to full power.</param>
        /// <param name="spinDownSeconds">Time from full power back to 0.</param>
        public float Tick(bool held, float dt, float spinUpSeconds, float spinDownSeconds)
        {
            if (held)
                Value = spinUpSeconds <= 0f ? 1f : Mathf.Min(1f, Value + dt / spinUpSeconds);
            else
                Value = spinDownSeconds <= 0f ? 0f : Mathf.Max(0f, Value - dt / spinDownSeconds);
            return Value;
        }

        /// <summary>Eased value for forces: slow start, firm finish (smoothstep).</summary>
        public float Eased => Value * Value * (3f - 2f * Value);

        public void Reset() => Value = 0f;
    }
}

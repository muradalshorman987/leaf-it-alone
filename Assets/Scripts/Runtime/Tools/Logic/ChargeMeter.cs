using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>
    /// Hold-to-finish progress (the Mulcher chewing through a pile). Fills while
    /// held, drains when released, and fires once when it reaches 1.
    /// </summary>
    public struct ChargeMeter
    {
        public float Value { get; private set; }

        /// <returns>True on the frame the meter completes; it then resets to 0.</returns>
        public bool Tick(bool held, float dt, float secondsToFill, float drainPerSecond)
        {
            if (held)
                Value += secondsToFill <= 0f ? 1f : dt / secondsToFill;
            else
                Value = Mathf.Max(0f, Value - drainPerSecond * dt);

            if (Value < 1f) return false;
            Value = 0f;
            return true;
        }

        public void Reset() => Value = 0f;
    }
}

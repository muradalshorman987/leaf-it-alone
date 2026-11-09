using UnityEngine;

namespace FallenYard.Audio
{
    /// <summary>
    /// Bagging piles in quick succession raises the coin chime a step each time (up a major
    /// scale), which makes a streak feel good without any UI. The streak resets after a pause.
    /// </summary>
    public sealed class RewardPitch
    {
        static readonly int[] Steps = { 0, 2, 4, 5, 7, 9, 11, 12 };

        public float Window { get; set; } = 3f;

        float _lastTime = float.NegativeInfinity;
        int _streak;

        public int Streak => _streak;

        /// <summary>Registers a reward at <paramref name="time"/> (seconds) and returns the pitch to play it at.</summary>
        public float Next(float time)
        {
            _streak = time - _lastTime <= Window ? Mathf.Min(_streak + 1, Steps.Length - 1) : 0;
            _lastTime = time;
            return Mathf.Pow(2f, Steps[_streak] / 12f);
        }

        public void Reset()
        {
            _streak = 0;
            _lastTime = float.NegativeInfinity;
        }
    }
}

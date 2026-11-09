using System;
using System.Collections.Generic;

namespace FallenYard.UI
{
    /// <summary>
    /// Short-lived notifications ("+12 coins", "Rake upgraded"). Pure C#: the view
    /// listens to <see cref="Added"/>/<see cref="Removed"/> and the owner calls
    /// <see cref="Tick"/> with unscaled time so toasts still fade while paused.
    /// </summary>
    public class ToastQueue
    {
        public class Toast
        {
            public string Text;
            public float Remaining;
            public float Duration;
            /// <summary>0..1, how far through its life the toast is.</summary>
            public float Progress => Duration <= 0f ? 1f : 1f - Remaining / Duration;
        }

        public const float DefaultDuration = 3f;

        readonly List<Toast> _active = new List<Toast>();

        /// <summary>At most this many toasts are visible; pushing more drops the oldest.</summary>
        public int MaxVisible { get; }

        public IReadOnlyList<Toast> Active => _active;

        public event Action<Toast> Added;
        public event Action<Toast> Removed;

        public ToastQueue(int maxVisible = 4)
        {
            MaxVisible = Math.Max(1, maxVisible);
        }

        public Toast Push(string text, float duration = DefaultDuration)
        {
            if (string.IsNullOrEmpty(text)) return null;
            while (_active.Count >= MaxVisible) RemoveAt(0);
            var toast = new Toast { Text = text, Duration = duration, Remaining = duration };
            _active.Add(toast);
            Added?.Invoke(toast);
            return toast;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                _active[i].Remaining -= deltaTime;
                if (_active[i].Remaining <= 0f) RemoveAt(i);
            }
        }

        public void Clear()
        {
            for (int i = _active.Count - 1; i >= 0; i--) RemoveAt(i);
        }

        void RemoveAt(int index)
        {
            var toast = _active[index];
            _active.RemoveAt(index);
            Removed?.Invoke(toast);
        }
    }
}

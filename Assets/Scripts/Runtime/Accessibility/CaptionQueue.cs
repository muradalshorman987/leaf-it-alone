using System;
using System.Collections.Generic;

namespace FallenYard.Accessibility
{
    /// <summary>
    /// Lines of sound captions on screen. Pure C#: a cue that is already showing is refreshed in
    /// place (a running blower doesn't stack "[Leaf blower roaring]" ten times), the oldest line is
    /// dropped when full, and the owner calls <see cref="Tick"/> with unscaled time.
    /// </summary>
    public class CaptionQueue
    {
        public class Line
        {
            public string Id;
            public string Text;
            public float Remaining;
            public float Duration;
        }

        readonly List<Line> _lines = new List<Line>();

        public int MaxLines { get; }
        public IReadOnlyList<Line> Lines => _lines;

        /// <summary>A new line appeared.</summary>
        public event Action<Line> Added;
        /// <summary>A showing line got new text or more time.</summary>
        public event Action<Line> Updated;
        public event Action<Line> Removed;

        public CaptionQueue(int maxLines = 3)
        {
            MaxLines = Math.Max(1, maxLines);
        }

        public Line Find(string id)
        {
            foreach (var l in _lines)
                if (l.Id == id) return l;
            return null;
        }

        /// <summary>Shows or refreshes a caption. <paramref name="id"/> defaults to the text.</summary>
        public Line Show(string id, string text, float seconds)
        {
            if (string.IsNullOrEmpty(text) || seconds <= 0f) return null;
            id = string.IsNullOrEmpty(id) ? text : id;

            var existing = Find(id);
            if (existing != null)
            {
                existing.Remaining = Math.Max(existing.Remaining, seconds);
                existing.Duration = Math.Max(existing.Duration, seconds);
                existing.Text = text;
                Updated?.Invoke(existing);
                return existing;
            }

            while (_lines.Count >= MaxLines) RemoveAt(0);
            var line = new Line { Id = id, Text = text, Remaining = seconds, Duration = seconds };
            _lines.Add(line);
            Added?.Invoke(line);
            return line;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            for (int i = _lines.Count - 1; i >= 0; i--)
            {
                _lines[i].Remaining -= deltaTime;
                if (_lines[i].Remaining <= 0f) RemoveAt(i);
            }
        }

        public void Clear()
        {
            for (int i = _lines.Count - 1; i >= 0; i--) RemoveAt(i);
        }

        void RemoveAt(int index)
        {
            var line = _lines[index];
            _lines.RemoveAt(index);
            Removed?.Invoke(line);
        }
    }
}

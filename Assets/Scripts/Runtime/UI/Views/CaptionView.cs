using System.Collections.Generic;
using FallenYard.Accessibility;
using FallenYard.Settings;
using UnityEngine.UIElements;

namespace FallenYard.UI
{
    /// <summary>Sound captions at the bottom centre of the screen, one label per <see cref="CaptionQueue"/> line.</summary>
    public class CaptionView
    {
        public VisualElement Root { get; }

        readonly Dictionary<CaptionQueue.Line, Label> _labels = new Dictionary<CaptionQueue.Line, Label>();
        CaptionQueue _queue;

        public int Count => _labels.Count;

        public CaptionView()
        {
            Root = new VisualElement().With("captions", "fy-captions", "fy-captions--boxed");
            Root.pickingMode = PickingMode.Ignore;
        }

        public void Bind(CaptionQueue queue)
        {
            Unbind();
            _queue = queue;
            if (_queue == null) return;
            _queue.Added += OnAdded;
            _queue.Updated += OnUpdated;
            _queue.Removed += OnRemoved;
            foreach (var l in _queue.Lines) OnAdded(l);
        }

        public void Unbind()
        {
            if (_queue != null)
            {
                _queue.Added -= OnAdded;
                _queue.Updated -= OnUpdated;
                _queue.Removed -= OnRemoved;
            }
            _queue = null;
            Root.Clear();
            _labels.Clear();
        }

        /// <summary>Text size and background box from the player's settings.</summary>
        public void ApplySettings(GameSettings s)
        {
            if (s == null) return;
            Root.EnableInClassList("fy-captions--small", s.captionSize == CaptionSize.Small);
            Root.EnableInClassList("fy-captions--large", s.captionSize == CaptionSize.Large);
            Root.EnableInClassList("fy-captions--boxed", s.captionBackground);
        }

        public Label LabelFor(CaptionQueue.Line line) => line != null && _labels.TryGetValue(line, out var l) ? l : null;

        void OnAdded(CaptionQueue.Line line)
        {
            var label = UIViewUtil.MakeLabel(null, line.Text, "fy-caption");
            label.pickingMode = PickingMode.Ignore;
            _labels[line] = label;
            Root.Add(label);
        }

        void OnUpdated(CaptionQueue.Line line)
        {
            if (_labels.TryGetValue(line, out var label)) label.text = line.Text;
        }

        void OnRemoved(CaptionQueue.Line line)
        {
            if (!_labels.TryGetValue(line, out var label)) return;
            _labels.Remove(line);
            label.RemoveFromHierarchy();
        }

        /// <summary>Fades lines in their last half second. Call after CaptionQueue.Tick.</summary>
        public void UpdateFade()
        {
            foreach (var pair in _labels)
            {
                float remaining = pair.Key.Remaining;
                pair.Value.style.opacity = remaining < 0.5f ? remaining / 0.5f : 1f;
            }
        }
    }
}

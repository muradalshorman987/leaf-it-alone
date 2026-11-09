using System.Collections.Generic;
using UnityEngine.UIElements;

namespace FallenYard.UI
{
    /// <summary>Shows the entries of a <see cref="ToastQueue"/> as a stack of labels.</summary>
    public class ToastView
    {
        public VisualElement Root { get; }

        readonly Dictionary<ToastQueue.Toast, Label> _labels = new Dictionary<ToastQueue.Toast, Label>();
        ToastQueue _queue;

        public int Count => _labels.Count;

        public ToastView()
        {
            Root = new VisualElement().With("toasts", "fy-toasts");
            Root.pickingMode = PickingMode.Ignore;
        }

        public void Bind(ToastQueue queue)
        {
            Unbind();
            _queue = queue;
            if (_queue == null) return;
            _queue.Added += OnAdded;
            _queue.Removed += OnRemoved;
            foreach (var t in _queue.Active) OnAdded(t);
        }

        public void Unbind()
        {
            if (_queue != null)
            {
                _queue.Added -= OnAdded;
                _queue.Removed -= OnRemoved;
            }
            _queue = null;
            Root.Clear();
            _labels.Clear();
        }

        void OnAdded(ToastQueue.Toast toast)
        {
            var label = UIViewUtil.MakeLabel(null, toast.Text, "fy-panel", "fy-toast");
            label.pickingMode = PickingMode.Ignore;
            _labels[toast] = label;
            Root.Add(label);
        }

        void OnRemoved(ToastQueue.Toast toast)
        {
            if (!_labels.TryGetValue(toast, out var label)) return;
            _labels.Remove(toast);
            label.RemoveFromHierarchy();
        }

        /// <summary>Fades toasts near the end of their life. Call after ToastQueue.Tick.</summary>
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

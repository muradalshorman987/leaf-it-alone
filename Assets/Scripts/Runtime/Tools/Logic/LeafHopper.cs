using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>The Leaf Vacuum's container. Fills as it sucks, empties into the compost bin.</summary>
    public class LeafHopper
    {
        public int Capacity { get; private set; }
        public int Count { get; private set; }
        public int Space => Mathf.Max(0, Capacity - Count);
        public bool IsFull => Count >= Capacity;
        public bool IsEmpty => Count <= 0;
        public float Fill01 => Capacity <= 0 ? 1f : Mathf.Clamp01((float)Count / Capacity);

        public LeafHopper(int capacity) => Capacity = Mathf.Max(1, capacity);

        /// <summary>Changes capacity (e.g. after an upgrade). Never drops leaves already inside.</summary>
        public void SetCapacity(int capacity) => Capacity = Mathf.Max(1, capacity);

        /// <summary>Adds up to amount leaves, returns how many fit.</summary>
        public int Add(int amount)
        {
            int accepted = Mathf.Clamp(amount, 0, Space);
            Count += accepted;
            return accepted;
        }

        /// <summary>Empties the hopper and returns how many leaves were in it.</summary>
        public int Empty()
        {
            int n = Count;
            Count = 0;
            return n;
        }
    }
}

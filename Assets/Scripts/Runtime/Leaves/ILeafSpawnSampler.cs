using UnityEngine;

namespace FallenYard.Leaves
{
    /// <summary>
    /// Chooses where each leaf starts when a <see cref="LeafField"/> spawns. Assign one to
    /// <see cref="LeafField.SpawnSampler"/>. Positions are yard-local X/Z offsets from the
    /// field's centre; anything outside the yard is clamped to its edge.
    /// Use only <paramref name="rng"/> for randomness so the same seed gives the same yard.
    /// </summary>
    public interface ILeafSpawnSampler
    {
        Vector2 SamplePosition(System.Random rng);
    }
}

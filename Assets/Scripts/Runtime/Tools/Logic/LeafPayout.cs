using FallenYard.Core;
using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>
    /// Turns leaves removed from the yard into coins. Shared by the bag, vacuum
    /// and mulcher so they pay the same way, carry fractional coins between
    /// payouts, and all raise <see cref="GameEvents.PileBagged"/> (the compost bin
    /// listens to that to fill up).
    /// </summary>
    public class LeafPayout
    {
        float _remainder;

        /// <summary>Fractional coins carried over to the next payout.</summary>
        public float Remainder => _remainder;

        /// <summary>Pays for leaves and raises PileBagged. Returns whole coins paid.</summary>
        public int Pay(int leaves, float coinsPerLeaf)
        {
            if (leaves <= 0) return 0;
            float coins = leaves * Mathf.Max(0f, coinsPerLeaf) + _remainder;
            int whole = Mathf.FloorToInt(coins);
            _remainder = coins - whole;

            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.AddCoins(whole);
                gm.RecordLeavesBagged(leaves);
            }
            GameEvents.RaisePileBagged(leaves, whole);
            return whole;
        }
    }
}

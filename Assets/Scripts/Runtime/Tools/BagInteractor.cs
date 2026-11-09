using FallenYard.Core;
using FallenYard.Leaves;
using FallenYard.Progression;
using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>
    /// Look at a pile and press E to bag it. Bags have a capacity, so huge piles
    /// take several bags. Each leaf pays coinsPerLeaf.
    /// </summary>
    public class BagInteractor : MonoBehaviour
    {
        public Camera playerCamera;
        public string toolId = "bag";
        public float reach = 3.5f;
        public int baseBagCapacity = 150;
        public float coinsPerLeaf = 0.5f;
        public LayerMask groundMask = ~0;

        /// <summary>Hint for the HUD, empty when nothing to say.</summary>
        public string Prompt { get; private set; } = "";

        int Capacity => Mathf.RoundToInt(baseBagCapacity *
            (UpgradeManager.Instance != null ? UpgradeManager.Instance.GetMultiplier(toolId, UpgradeStat.Capacity) : 1f));

        float _coinRemainder;

        void Awake()
        {
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        }

        void Update()
        {
            Prompt = "";
            if (GameState.InputBlocked || playerCamera == null || PileDetector.Instance == null || LeafField.Instance == null)
                return;

            var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, reach, groundMask, QueryTriggerInteraction.Ignore)) return;
            if (!PileDetector.Instance.TryGetPileNear(hit.point, 0.6f, out var pile)) return;

            Prompt = $"[E] Bag pile ({pile.leafCount} leaves)";
            if (!GameInput.InteractPressed) return;

            int bagged = LeafField.Instance.CollectInRadius(pile.center, pile.radius, Capacity);
            if (bagged <= 0) return;

            float coins = bagged * coinsPerLeaf + _coinRemainder;
            int whole = Mathf.FloorToInt(coins);
            _coinRemainder = coins - whole;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddCoins(whole);
                GameManager.Instance.RecordLeavesBagged(bagged);
            }
            GameEvents.RaisePileBagged(bagged, whole);
            PileDetector.Instance.Scan();
        }
    }
}

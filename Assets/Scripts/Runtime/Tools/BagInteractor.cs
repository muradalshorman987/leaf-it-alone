using FallenYard.Core;
using FallenYard.Leaves;
using FallenYard.Progression;
using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>
    /// The player's E key. In order of priority:
    /// 1. Looking at the compost bin: empty the vacuum hopper into it, or spread ready fertilizer.
    /// 2. Looking at a pile: bag it. Bags have a capacity, so huge piles take several bags.
    /// 3. Vacuum in hand, no bin in the yard: empty the hopper on the spot.
    /// <see cref="Prompt"/> also shows the current tool's status when there's nothing to press.
    /// </summary>
    public class BagInteractor : MonoBehaviour
    {
        public Camera playerCamera;
        [Tooltip("Upgrade id for bag capacity.")]
        public string toolId = "bag";
        [Tooltip("How far (m) you can reach to bag a pile or use the bin.")]
        public float reach = 3.5f;
        [Tooltip("Leaves per bag before upgrades.")]
        public int baseBagCapacity = 150;
        [Tooltip("Coins paid per bagged leaf.")]
        public float coinsPerLeaf = 0.5f;
        [Tooltip("Layers the interaction ray can hit.")]
        public LayerMask groundMask = ~0;

        [Tooltip("Optional; found on this object or its children.")]
        public ToolController toolController;
        [Tooltip("Optional; found on this object or its children.")]
        public LeafVacuum vacuum;

        /// <summary>Hint for the HUD, empty when nothing to say.</summary>
        public string Prompt { get; private set; } = "";

        int Capacity => Mathf.RoundToInt(baseBagCapacity *
            (UpgradeManager.Instance != null ? UpgradeManager.Instance.GetMultiplier(toolId, UpgradeStat.Capacity) : 1f));

        readonly LeafPayout _payout = new LeafPayout();

        void Awake()
        {
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
            if (toolController == null) toolController = GetComponentInChildren<ToolController>();
            if (vacuum == null) vacuum = GetComponentInChildren<LeafVacuum>(true);
        }

        void Update()
        {
            Prompt = "";
            if (GameState.InputBlocked || playerCamera == null) return;

            HandleInteraction(GameInput.InteractPressed);

            if (string.IsNullOrEmpty(Prompt) && toolController != null && toolController.Current != null)
                Prompt = toolController.Current.StatusText;
        }

        void HandleInteraction(bool pressed)
        {
            var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            bool hasHit = Physics.Raycast(ray, out var hit, reach, groundMask, QueryTriggerInteraction.Ignore);

            // 1. Compost bin
            var bin = hasHit ? hit.collider.GetComponentInParent<CompostBin>() : null;
            if (bin != null)
            {
                bool hopperHasLeaves = vacuum != null && vacuum.IsUnlocked && !vacuum.Hopper.IsEmpty;
                Prompt = hopperHasLeaves ? $"[E] Empty hopper into bin ({vacuum.Hopper.Count} leaves)" : bin.Prompt;
                if (!pressed) return;
                if (hopperHasLeaves) vacuum.EmptyHopper();
                else bin.TrySpread();
                return;
            }

            // 2. Pile
            if (hasHit && PileDetector.Instance != null && LeafField.Instance != null &&
                PileDetector.Instance.TryGetPileNear(hit.point, 0.6f, out var pile))
            {
                Prompt = $"[E] Bag pile ({pile.leafCount} leaves)";
                if (!pressed) return;
                var leaves = LeafTargets.Current;
                int bagged = leaves != null ? leaves.CollectInRadius(pile.center, pile.radius, Capacity) : 0;
                if (bagged <= 0) return;
                _payout.Pay(bagged, coinsPerLeaf);
                GameEvents.RaiseLeavesBaggedAt(pile.center, bagged);
                PileDetector.Instance.Scan();
                return;
            }

            // 3. No bin in this yard: let the vacuum dump anywhere so it is never stuck full.
            if (CompostBin.Instance == null && vacuum != null && toolController != null &&
                ReferenceEquals(toolController.Current, vacuum) && !vacuum.Hopper.IsEmpty)
            {
                Prompt = $"[E] Empty hopper ({vacuum.Hopper.Count} leaves)";
                if (pressed) vacuum.EmptyHopper();
            }
        }
    }
}

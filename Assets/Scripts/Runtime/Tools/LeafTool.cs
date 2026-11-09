using FallenYard.Progression;
using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>
    /// Base class for anything that moves leaves. Stats run through the
    /// UpgradeManager so every tool is upgradeable without extra code.
    /// Leaves are reached only through <see cref="ILeafTarget"/>.
    /// </summary>
    public abstract class LeafTool : MonoBehaviour, ITool
    {
        [Tooltip("Matches a ToolUpgradeDefinition.toolId.")]
        public string toolId = "tool";
        [Tooltip("Name shown in the HUD and shop.")]
        public string displayName = "Tool";

        [Header("Base stats (before upgrades)")]
        [Tooltip("Radius of the area the tool affects, in metres.")]
        public float baseRadius = 1f;
        [Tooltip("How hard the tool moves leaves.")]
        public float baseStrength = 8f;
        [Tooltip("How far from the camera the tool reaches.")]
        public float baseRange = 3f;

        [Header("Aiming")]
        [Tooltip("Layers the aim ray can hit (ground, props).")]
        public LayerMask groundMask = ~0;

        [Tooltip("Optional first-person model, shown only while equipped.")]
        public GameObject viewModel;

        public string ToolId => toolId;
        public string DisplayName => displayName;
        public bool IsUnlocked => UpgradeManager.Instance == null || UpgradeManager.Instance.IsUnlocked(toolId);
        public bool IsEquipped { get; private set; }

        /// <inheritdoc/>
        public float Throttle { get; protected set; }

        /// <inheritdoc/>
        public virtual string StatusText => "";

        protected float Radius => baseRadius * Mult(UpgradeStat.Radius);
        protected float Strength => baseStrength * Mult(UpgradeStat.Strength);
        protected float Range => baseRange * Mult(UpgradeStat.Range);
        protected static ILeafTarget LeafTarget => LeafTargets.Current;

        protected float Mult(UpgradeStat stat) =>
            UpgradeManager.Instance != null ? UpgradeManager.Instance.GetMultiplier(toolId, stat) : 1f;

        public virtual void OnEquip()
        {
            IsEquipped = true;
            if (viewModel != null) viewModel.SetActive(true);
        }

        public virtual void OnUnequip()
        {
            IsEquipped = false;
            Throttle = 0f;
            if (viewModel != null) viewModel.SetActive(false);
        }

        /// <summary>Called every frame while equipped.</summary>
        public abstract void Tick(Camera cam, bool primaryHeld, float dt);

        /// <summary>Where the camera is pointing on the ground, clamped to the tool's range.</summary>
        protected bool TryGetGroundPoint(Camera cam, float maxRange, out Vector3 point)
        {
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out var hit, maxRange + 1.5f, groundMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }

            // Looking too far ahead: use a point at max range on the ground.
            var leaves = LeafTarget;
            if (leaves == null)
            {
                point = default;
                return false;
            }
            Vector3 flat = cam.transform.forward;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-4f)
            {
                point = default;
                return false;
            }
            point = cam.transform.position + flat.normalized * maxRange;
            point.y = leaves.GroundHeightAt(point);
            return true;
        }

        /// <summary>Flat unit vector from a point toward the camera (zero if directly below).</summary>
        protected static Vector3 FlatToward(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0f;
            return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.zero;
        }
    }
}

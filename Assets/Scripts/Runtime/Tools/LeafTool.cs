using FallenYard.Leaves;
using FallenYard.Progression;
using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>
    /// Base class for anything that moves leaves. Stats run through the
    /// UpgradeManager so every tool is upgradeable without extra code.
    /// </summary>
    public abstract class LeafTool : MonoBehaviour
    {
        [Tooltip("Matches a ToolUpgradeDefinition.toolId.")]
        public string toolId = "tool";
        public string displayName = "Tool";

        [Header("Base stats (before upgrades)")]
        public float baseRadius = 1f;
        public float baseStrength = 8f;
        [Tooltip("How far from the camera the tool reaches.")]
        public float baseRange = 3f;

        [Header("Aiming")]
        public LayerMask groundMask = ~0;

        [Tooltip("Optional first-person model, shown only while equipped.")]
        public GameObject viewModel;

        public bool IsUnlocked => UpgradeManager.Instance == null || UpgradeManager.Instance.IsUnlocked(toolId);

        protected float Radius => baseRadius * Mult(UpgradeStat.Radius);
        protected float Strength => baseStrength * Mult(UpgradeStat.Strength);
        protected float Range => baseRange * Mult(UpgradeStat.Range);
        protected static LeafField Field => LeafField.Instance;

        protected float Mult(UpgradeStat stat) =>
            UpgradeManager.Instance != null ? UpgradeManager.Instance.GetMultiplier(toolId, stat) : 1f;

        public virtual void OnEquip()
        {
            if (viewModel != null) viewModel.SetActive(true);
        }

        public virtual void OnUnequip()
        {
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
            if (Field == null)
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
            point.y = Field.GroundHeightAt(point);
            return true;
        }
    }
}

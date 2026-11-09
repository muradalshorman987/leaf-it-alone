using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>
    /// Contract for anything the player holds and uses with the primary button
    /// (see docs/PLAN.md section 5; owned by WP3). UI, audio and tutorial code
    /// should depend on this, not on the concrete tool classes.
    /// </summary>
    public interface ITool
    {
        /// <summary>Matches a ToolUpgradeDefinition.toolId.</summary>
        string ToolId { get; }
        string DisplayName { get; }
        bool IsUnlocked { get; }

        /// <summary>Current effort 0..1 (blower throttle, rake stroke speed). Drives audio/VFX.</summary>
        float Throttle { get; }

        /// <summary>Short HUD status such as "Hopper 40/120", or empty.</summary>
        string StatusText { get; }

        void OnEquip();
        void OnUnequip();

        /// <summary>Called every frame while equipped.</summary>
        void Tick(Camera cam, bool primaryHeld, float dt);
    }
}

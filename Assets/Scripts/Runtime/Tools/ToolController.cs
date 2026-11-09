using System.Collections.Generic;
using FallenYard.Core;
using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>Owns the player's tools: switching (1-4) and driving the active one.</summary>
    public class ToolController : MonoBehaviour
    {
        public Camera playerCamera;
        [Tooltip("Leave empty to collect every LeafTool in children.")]
        public List<LeafTool> tools = new List<LeafTool>();

        public LeafTool Current => _index >= 0 && _index < tools.Count ? tools[_index] : null;
        int _index = -1;

        void Awake()
        {
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
            if (tools.Count == 0) tools.AddRange(GetComponentsInChildren<LeafTool>(true));
            foreach (var t in tools) t.OnUnequip();
        }

        void Start() => Equip(0);

        public void Equip(int index)
        {
            if (index < 0 || index >= tools.Count || index == _index) return;
            if (!tools[index].IsUnlocked) return;
            Current?.OnUnequip();
            _index = index;
            Current.OnEquip();
            GameEvents.RaiseToolEquipped(Current.toolId);
        }

        void Update()
        {
            if (GameState.InputBlocked) return;
            int slot = GameInput.ToolSlotPressed;
            if (slot >= 0) Equip(slot);
            if (Current != null && playerCamera != null)
                Current.Tick(playerCamera, GameInput.PrimaryHeld, Time.deltaTime);
        }
    }
}

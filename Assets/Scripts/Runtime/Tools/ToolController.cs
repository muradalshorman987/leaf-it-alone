using System.Collections.Generic;
using FallenYard.Core;
using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>
    /// Owns the player's tools: switching (keys 1-4), driving the active one and
    /// broadcasting its throttle for audio/VFX.
    /// </summary>
    public class ToolController : MonoBehaviour
    {
        public Camera playerCamera;
        [Tooltip("Leave empty to collect every LeafTool in children. Order = number keys.")]
        public List<LeafTool> tools = new List<LeafTool>();
        [Tooltip("Throttle changes smaller than this are not re-broadcast.")]
        public float throttleEpsilon = 0.02f;

        public LeafTool Current => _index >= 0 && _index < tools.Count ? tools[_index] : null;
        public int CurrentIndex => _index;

        int _index = -1;
        float _lastThrottle;

        void Awake()
        {
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
            if (tools.Count == 0) tools.AddRange(GetComponentsInChildren<LeafTool>(true));
            foreach (var t in tools) t.OnUnequip();
        }

        void Start() => Equip(0);

        /// <summary>Equips a tool by slot. Returns false if the slot is empty, already equipped or locked.</summary>
        public bool Equip(int index)
        {
            if (index < 0 || index >= tools.Count || index == _index) return false;
            if (tools[index] == null || !tools[index].IsUnlocked) return false;
            Current?.OnUnequip();
            _index = index;
            Current.OnEquip();
            GameEvents.RaiseToolEquipped(Current.toolId);
            BroadcastThrottle(0f);
            return true;
        }

        void Update()
        {
            if (GameState.InputBlocked)
            {
                // Menus open: release the trigger so engines wind down.
                Step(false, Time.deltaTime);
                return;
            }
            int slot = GameInput.ToolSlotPressed;
            if (slot >= 0) Equip(slot);
            Step(GameInput.PrimaryHeld, Time.deltaTime);
        }

        /// <summary>Ticks the active tool and broadcasts its throttle. Public for tests.</summary>
        public void Step(bool primaryHeld, float dt)
        {
            if (Current == null || playerCamera == null) return;
            Current.Tick(playerCamera, primaryHeld, dt);
            float throttle = Current.Throttle;
            if (Mathf.Abs(throttle - _lastThrottle) >= throttleEpsilon || (throttle <= 0f && _lastThrottle > 0f))
                BroadcastThrottle(throttle);
        }

        void BroadcastThrottle(float throttle)
        {
            _lastThrottle = throttle;
            GameEvents.RaiseToolThrottle(throttle);
        }
    }
}

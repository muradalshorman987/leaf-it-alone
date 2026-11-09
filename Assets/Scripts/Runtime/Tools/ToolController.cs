using System.Collections.Generic;
using FallenYard.Core;
using FallenYard.Settings;
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
        [Tooltip("Accessibility: press the use button once to start, again to stop. Follows the player's settings when a SettingsManager exists.")]
        public bool toggleToolUse;

        public LeafTool Current => _index >= 0 && _index < tools.Count ? tools[_index] : null;
        public int CurrentIndex => _index;

        int _index = -1;
        float _lastThrottle;
        readonly HoldToggle _use = new HoldToggle();

        /// <summary>True while the use button is held, or toggled on in toggle mode.</summary>
        public bool UseActive => _use.Active;

        void Awake()
        {
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
            if (tools.Count == 0) tools.AddRange(GetComponentsInChildren<LeafTool>(true));
            foreach (var t in tools) t.OnUnequip();
        }

        void OnEnable() => SettingsManager.Changed += ApplySettings;
        void OnDisable() => SettingsManager.Changed -= ApplySettings;

        void Start()
        {
            if (SettingsManager.Instance != null) ApplySettings(SettingsManager.Instance.Current);
            Equip(0);
        }

        public void ApplySettings(GameSettings s)
        {
            if (s == null) return;
            toggleToolUse = s.toggleToolUse;
            if (!toggleToolUse) _use.Reset();
        }

        /// <summary>Equips a tool by slot. Returns false if the slot is empty, already equipped or locked.</summary>
        public bool Equip(int index)
        {
            if (index < 0 || index >= tools.Count || index == _index) return false;
            if (tools[index] == null || !tools[index].IsUnlocked) return false;
            Current?.OnUnequip();
            _use.Reset();
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
                _use.Reset();
                Step(false, Time.deltaTime);
                return;
            }
            int slot = GameInput.ToolSlotPressed;
            if (slot >= 0) Equip(slot);
            Step(UpdateUse(GameInput.PrimaryHeld, GameInput.PrimaryPressed), Time.deltaTime);
        }

        /// <summary>Feeds the use button through hold/toggle mode. Public for tests.</summary>
        public bool UpdateUse(bool held, bool pressedThisFrame)
        {
            _use.ToggleMode = toggleToolUse;
            return _use.Update(held, pressedThisFrame);
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

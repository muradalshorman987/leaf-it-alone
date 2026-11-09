using System;
using UnityEngine;

namespace FallenYard.Leaves
{
    /// <summary>The four leaf kinds from the design (docs/PLAN.md, "Leaf types").</summary>
    public enum LeafPreset
    {
        /// <summary>Light, catches the wind and flies far.</summary>
        Maple,
        /// <summary>The everyday leaf. Rakes and blows normally.</summary>
        Oak,
        /// <summary>Heavy and soggy. The blower barely moves it; use the rake.</summary>
        WetClump,
        /// <summary>Fine needles. The rake slips through them; use the blower or vacuum.</summary>
        PineNeedles,
    }

    /// <summary>How one leaf type moves. All values are relative to an oak leaf (1).</summary>
    [Serializable]
    public struct LeafPhysicsSettings
    {
        [Tooltip("Relative mass. Wind and blower forces are divided by it.")]
        [Min(0.05f)] public float mass;
        [Tooltip("Multiplier on the field's damping while the leaf is on the ground.")]
        [Min(0f)] public float groundDrag;
        [Tooltip("Multiplier on the field's damping while the leaf is airborne. Low = glides far.")]
        [Min(0f)] public float airDrag;
        [Tooltip("Multiplier on lift gravity. Low = falls slowly and flutters.")]
        [Min(0.05f)] public float gravityScale;
        [Tooltip("Wind strength (m/s) needed before this leaf moves at all. Keeps piles safe in a light breeze.")]
        [Min(0f)] public float windThreshold;
        [Tooltip("How well the rake catches this leaf (1 = normal).")]
        [Min(0f)] public float rakeGrip;
        [Tooltip("How well blower and vacuum air catches this leaf (1 = normal), before dividing by mass.")]
        [Min(0f)] public float blowerGrip;

        public static LeafPhysicsSettings For(LeafPreset preset)
        {
            switch (preset)
            {
                case LeafPreset.Maple:
                    return new LeafPhysicsSettings { mass = 0.6f, groundDrag = 0.7f, airDrag = 0.25f, gravityScale = 0.55f, windThreshold = 0.5f, rakeGrip = 0.9f, blowerGrip = 1.1f };
                case LeafPreset.WetClump:
                    return new LeafPhysicsSettings { mass = 3f, groundDrag = 2.2f, airDrag = 0.8f, gravityScale = 1.5f, windThreshold = 4.5f, rakeGrip = 1f, blowerGrip = 0.35f };
                case LeafPreset.PineNeedles:
                    return new LeafPhysicsSettings { mass = 0.8f, groundDrag = 1.3f, airDrag = 0.5f, gravityScale = 0.9f, windThreshold = 1.1f, rakeGrip = 0.35f, blowerGrip = 1.5f };
                default:
                    return new LeafPhysicsSettings { mass = 1f, groundDrag = 1f, airDrag = 0.4f, gravityScale = 0.8f, windThreshold = 1.3f, rakeGrip = 1f, blowerGrip = 1f };
            }
        }

        public static Vector2 ScaleRangeFor(LeafPreset preset)
        {
            switch (preset)
            {
                case LeafPreset.Maple: return new Vector2(0.2f, 0.34f);
                case LeafPreset.WetClump: return new Vector2(0.22f, 0.36f);
                case LeafPreset.PineNeedles: return new Vector2(0.12f, 0.2f);
                default: return new Vector2(0.16f, 0.28f);
            }
        }

        /// <summary>Default spawn mix for a yard that doesn't set its own types.</summary>
        public static float SpawnWeightFor(LeafPreset preset)
        {
            switch (preset)
            {
                case LeafPreset.Maple: return 0.3f;
                case LeafPreset.WetClump: return 0.1f;
                case LeafPreset.PineNeedles: return 0.15f;
                default: return 0.45f;
            }
        }

        /// <summary>Blittable copy for jobs, with mass pre-inverted.</summary>
        public LeafTypeParams ToParams() => new LeafTypeParams
        {
            invMass = 1f / Mathf.Max(0.05f, mass),
            groundDrag = Mathf.Max(0f, groundDrag),
            airDrag = Mathf.Max(0f, airDrag),
            gravityScale = Mathf.Max(0.05f, gravityScale),
            windThreshold = Mathf.Max(0f, windThreshold),
            rakeGrip = Mathf.Max(0f, rakeGrip),
            blowerGrip = Mathf.Max(0f, blowerGrip),
        };
    }

    /// <summary>Per-type physics as the jobs see it. Built from <see cref="LeafPhysicsSettings"/>.</summary>
    public struct LeafTypeParams
    {
        public float invMass;
        public float groundDrag;
        public float airDrag;
        public float gravityScale;
        public float windThreshold;
        public float rakeGrip;
        public float blowerGrip;
    }

    /// <summary>
    /// A leaf type a yard can spawn: look (mesh, colour materials) and physics.
    /// Create with Assets > Create > Fallen Yard > Leaf Type, then pick a preset
    /// from the component's context menu or tune the numbers by hand.
    /// </summary>
    [CreateAssetMenu(menuName = "Fallen Yard/Leaf Type", fileName = "LeafType")]
    public class LeafTypeDefinition : ScriptableObject
    {
        public string typeId = "oak";
        public string displayName = "Oak";

        [Header("Spawn")]
        [Tooltip("Relative share of this type in the yard's leaf mix.")]
        [Min(0f)] public float spawnWeight = 1f;
        public Vector2 scaleRange = new Vector2(0.16f, 0.28f);

        [Header("Look")]
        [Tooltip("Optional. A procedural mesh for the closest preset shape is used if empty.")]
        public Mesh mesh;
        [Tooltip("Optional cheap mesh drawn past the LeafField's LOD distance. A quad is used if empty.")]
        public Mesh lodMesh;
        [Tooltip("Colour variants, picked at random per leaf. Empty = use the LeafField's Leaf Materials.")]
        public Material[] materials;
        [Tooltip("Procedural shape used when Mesh is empty.")]
        public LeafPreset shape = LeafPreset.Oak;

        [Header("Physics")]
        public LeafPhysicsSettings physics = LeafPhysicsSettings.For(LeafPreset.Oak);

        public void ApplyPreset(LeafPreset preset)
        {
            typeId = PresetId(preset);
            displayName = PresetName(preset);
            shape = preset;
            physics = LeafPhysicsSettings.For(preset);
            scaleRange = LeafPhysicsSettings.ScaleRangeFor(preset);
            spawnWeight = LeafPhysicsSettings.SpawnWeightFor(preset);
        }

        public static string PresetId(LeafPreset preset)
        {
            switch (preset)
            {
                case LeafPreset.Maple: return "maple";
                case LeafPreset.WetClump: return "wet_clump";
                case LeafPreset.PineNeedles: return "pine";
                default: return "oak";
            }
        }

        public static string PresetName(LeafPreset preset)
        {
            switch (preset)
            {
                case LeafPreset.Maple: return "Maple";
                case LeafPreset.WetClump: return "Wet clump";
                case LeafPreset.PineNeedles: return "Pine needles";
                default: return "Oak";
            }
        }

        [ContextMenu("Preset/Maple")] void PresetMaple() => ApplyPreset(LeafPreset.Maple);
        [ContextMenu("Preset/Oak")] void PresetOak() => ApplyPreset(LeafPreset.Oak);
        [ContextMenu("Preset/Wet clump")] void PresetWet() => ApplyPreset(LeafPreset.WetClump);
        [ContextMenu("Preset/Pine needles")] void PresetPine() => ApplyPreset(LeafPreset.PineNeedles);
    }
}

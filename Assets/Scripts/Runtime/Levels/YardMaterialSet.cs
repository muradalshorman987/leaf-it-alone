using System;
using UnityEngine;

namespace FallenYard.Levels
{
    /// <summary>
    /// Materials a greybox yard is built from. The editor builder fills it with saved material
    /// assets; <see cref="CreateRuntime"/> makes throwaway ones (for tests or a yard built in play).
    /// </summary>
    [Serializable]
    public class YardMaterialSet
    {
        public Material grass, wood, walls, roof, foliage, hedge, stone, path, flowers, soil, compost, fertilizer;
        public Material[] leaves = Array.Empty<Material>();

        /// <summary>Plain URP Lit (or Standard) materials coloured from the palette. Caller destroys them.</summary>
        public static YardMaterialSet CreateRuntime(YardPalette p)
        {
            p ??= new YardPalette();
            var set = new YardMaterialSet
            {
                grass = Make("Grass", p.grass),
                wood = Make("Wood", p.wood),
                walls = Make("Walls", p.houseWalls),
                roof = Make("Roof", p.roof),
                foliage = Make("Foliage", p.foliage),
                hedge = Make("Hedge", p.hedge),
                stone = Make("Stone", p.stone),
                path = Make("Path", p.path),
                flowers = Make("Flowers", p.flowers),
                soil = Make("Soil", new Color(0.30f, 0.21f, 0.13f)),
                compost = Make("Compost", new Color(0.28f, 0.19f, 0.11f)),
                fertilizer = Make("Fertilizer", new Color(0.55f, 0.78f, 0.30f)),
            };
            var colors = p.leafColors ?? Array.Empty<Color>();
            set.leaves = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                set.leaves[i] = Make("Leaf_" + i, colors[i]);
                if (set.leaves[i] != null) set.leaves[i].enableInstancing = true;
            }
            return set;
        }

        /// <summary>Every material in the set (skipping empty slots).</summary>
        public Material[] All()
        {
            var list = new System.Collections.Generic.List<Material> { grass, wood, walls, roof, foliage, hedge, stone, path, flowers, soil, compost, fertilizer };
            if (leaves != null) list.AddRange(leaves);
            list.RemoveAll(m => m == null);
            return list.ToArray();
        }

        public static Shader LitShader() => Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        /// <summary>Sets a material's base colour on both URP (_BaseColor) and built-in (_Color) properties.</summary>
        public static void SetColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.1f);
        }

        static Material Make(string name, Color c)
        {
            var shader = LitShader();
            if (shader == null) return null;
            var m = new Material(shader) { name = "Yard_" + name };
            SetColor(m, c);
            return m;
        }
    }
}

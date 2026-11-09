using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FallenYard.EditorTools.Art
{
    /// <summary>
    /// Menu: Fallen Yard > Art > Set Up Materials and Skyboxes.
    /// Creates the shared URP materials the Blender files expect, one skybox per
    /// season, and points every model in Assets/Art at the shared materials.
    /// Safe to run again: it updates existing assets instead of duplicating them.
    /// </summary>
    public static class ArtMaterialSetup
    {
        public const string MaterialsFolder = "Assets/Art/Materials";
        const string SkyFolder = "Assets/Art/Materials/Sky";
        const string PalettePath = "Assets/Art/Textures/FY_Palette.png";
        const string EnvPalettePath = "Assets/Art/Textures/FY_EnvPalette.png";
        const string SkyTextureFolder = "Assets/Art/Textures/Sky";

        /// <summary>Material names used inside the FBX files.</summary>
        public static readonly string[] SharedMaterialNames = { "FY_Palette", "FY_EnvPalette", "FY_Foliage" };

        /// <summary>FY_Foliage season variants: U offset into row 7 of FY_EnvPalette.</summary>
        public static readonly (string suffix, float offset)[] FoliageSeasons =
        {
            ("Gold", 0f), ("Orange", 0.25f), ("Red", 0.5f), ("Green", 0.75f),
        };

        public static readonly string[] SkySeasons = { "EarlyAutumn", "PeakAutumn", "LateAutumn", "FirstFrost" };

        [MenuItem("Fallen Yard/Art/Set Up Materials and Skyboxes")]
        public static void Run()
        {
            EnsureFolder(MaterialsFolder);
            EnsureFolder(SkyFolder);

            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath);
            var env = AssetDatabase.LoadAssetAtPath<Texture2D>(EnvPalettePath);
            if (palette == null || env == null)
            {
                Debug.LogError("[Art] Palette textures are missing under Assets/Art/Textures. Pull the LFS files first.");
                return;
            }

            MakeLit("FY_Palette", palette, 0f);
            MakeLit("FY_EnvPalette", env, 0f);
            MakeLit("FY_Foliage", env, 0f);
            foreach (var (suffix, offset) in FoliageSeasons)
                MakeLit("FY_Foliage_" + suffix, env, offset);

            foreach (var season in SkySeasons)
                MakeSky(season);

            AssetDatabase.SaveAssets();
            var models = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art" })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            int changed = RemapModels(models);
            Debug.Log($"[Art] Materials and skyboxes ready in {MaterialsFolder}. Remapped {changed} model(s).");
        }

        public static bool SharedMaterialsExist() =>
            SharedMaterialNames.All(n => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{n}.mat") != null);

        /// <summary>Points embedded FY_ materials at the shared ones. Returns how many models changed.</summary>
        public static int RemapModels(IEnumerable<string> paths)
        {
            var shared = SharedMaterialNames
                .Select(n => (name: n, mat: AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{n}.mat")))
                .Where(x => x.mat != null).ToArray();
            int changed = 0;
            foreach (var path in paths)
            {
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;
                var existing = importer.GetExternalObjectMap();
                var embedded = new HashSet<string>(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Select(m => m.name));
                bool dirty = false;
                foreach (var (name, mat) in shared)
                {
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                    if (!embedded.Contains(name) || (existing.TryGetValue(id, out var current) && current == mat)) continue;
                    importer.AddRemap(id, mat);
                    dirty = true;
                }
                if (!dirty) continue;
                importer.SaveAndReimport();
                changed++;
            }
            return changed;
        }

        static void MakeLit(string name, Texture2D texture, float uOffset)
        {
            var path = $"{MaterialsFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            mat.SetTexture("_BaseMap", texture);
            mat.SetTextureOffset("_BaseMap", new Vector2(uOffset, 0f));
            mat.SetTextureScale("_BaseMap", Vector2.one);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.1f);
            mat.SetFloat("_Metallic", 0f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
        }

        static void MakeSky(string season)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{SkyTextureFolder}/FY_Sky_{season}.png");
            if (texture == null) return;
            var path = $"{SkyFolder}/FY_Sky_{season}.mat";
            var shader = Shader.Find("Skybox/Panoramic");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = "FY_Sky_" + season };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetTexture("_MainTex", texture);
            mat.SetFloat("_Mapping", 1f);   // Latitude-Longitude layout
            mat.SetFloat("_ImageType", 0f); // 360 degrees
            mat.SetFloat("_Exposure", 1f);
            EditorUtility.SetDirty(mat);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}

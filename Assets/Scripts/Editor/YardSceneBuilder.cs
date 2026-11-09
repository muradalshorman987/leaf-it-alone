using System.Collections.Generic;
using FallenYard.Leaves;
using FallenYard.Levels;
using FallenYard.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace FallenYard.EditorTools
{
    /// <summary>
    /// Menu: Fallen Yard > Build All Yards.
    /// Turns the five Hollow Creek layouts (<see cref="YardLayouts"/>) into YardDefinition
    /// assets, a catalog, an unlock table, per-yard materials and one playable scene per yard,
    /// then puts the yard scenes first in Build Settings (the Starter Lawn opens the game).
    ///
    /// Yard assets that already exist are kept, so Inspector tweaks survive a rebuild; use
    /// Fallen Yard > Yards > Reset Yard Data From Code to throw them away.
    /// </summary>
    public static class YardSceneBuilder
    {
        const string DataDir = "Assets/FallenYard/Data/Yards";
        const string MatRoot = "Assets/FallenYard/Materials/Yards";
        const string SceneDir = "Assets/Scenes/Yards";
        const string CatalogPath = DataDir + "/YardCatalog.asset";
        const string UnlockTablePath = DataDir + "/HollowCreekUnlockTable.asset";

        [MenuItem("Fallen Yard/Build All Yards", priority = 10)]
        public static void BuildAll() => BuildAll(resetData: false);

        [MenuItem("Fallen Yard/Yards/Reset Yard Data From Code")]
        public static void ResetAndBuildAll()
        {
            if (!EditorUtility.DisplayDialog("Reset yard data",
                    "Overwrite every yard definition and the unlock table with the layouts in YardLayouts.cs, then rebuild the yard scenes?",
                    "Reset and rebuild", "Cancel")) return;
            BuildAll(resetData: true);
        }

        [MenuItem("Fallen Yard/Yards/Validate Yards")]
        public static void ValidateMenu()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<YardCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning("[FallenYard] No yard catalog yet. Run Fallen Yard > Build All Yards.");
                return;
            }
            Report(catalog.Validate());
        }

        static void BuildAll(bool resetData)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            TestYardBuilder.EnsureUrp();
            TestYardBuilder.EnsureFolder(DataDir);
            TestYardBuilder.EnsureFolder(MatRoot);
            TestYardBuilder.EnsureFolder(SceneDir);

            var table = BuildUnlockTable(resetData);
            var catalog = LoadOrCreate<YardCatalog>(CatalogPath);
            catalog.unlockTable = table;
            catalog.yards.Clear();
            foreach (var fresh in YardLayouts.CreateAll())
            {
                catalog.yards.Add(SaveDefinition(fresh, resetData));
                Object.DestroyImmediate(fresh);
            }
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            var problems = catalog.Validate();
            var scenePaths = new List<string>();
            try
            {
                for (int i = 0; i < catalog.yards.Count; i++)
                {
                    var def = catalog.yards[i];
                    EditorUtility.DisplayProgressBar("Building yards", def.displayName, (float)i / catalog.yards.Count);
                    scenePaths.Add(BuildScene(def, catalog));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            for (int i = scenePaths.Count - 1; i >= 0; i--)
                TestYardBuilder.AddSceneToBuild(scenePaths[i], first: true);
            AssetDatabase.SaveAssets();

            if (scenePaths.Count > 0) EditorSceneManager.OpenScene(scenePaths[0]);
            Report(problems);
            Debug.Log($"[FallenYard] Built {scenePaths.Count} yards in {SceneDir}. The Starter Lawn opens the game; press Play!");
        }

        // ------------------------------------------------------------------ data

        static YardUnlockTable BuildUnlockTable(bool reset)
        {
            var table = LoadOrCreate<YardUnlockTable>(UnlockTablePath);
            if (reset || table.yards.Count == 0)
            {
                table.yards = YardLayouts.CreateUnlockRules();
                EditorUtility.SetDirty(table);
            }
            return table;
        }

        static YardDefinition SaveDefinition(YardDefinition fresh, bool reset)
        {
            string path = $"{DataDir}/{fresh.yardId}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<YardDefinition>(path);
            if (existing == null)
            {
                var copy = Object.Instantiate(fresh);
                copy.name = fresh.yardId;
                AssetDatabase.CreateAsset(copy, path);
                return copy;
            }
            if (reset)
            {
                EditorUtility.CopySerialized(fresh, existing);
                existing.name = fresh.yardId;
                EditorUtility.SetDirty(existing);
            }
            return existing;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static YardMaterialSet BuildMaterials(YardDefinition def)
        {
            string dir = $"{MatRoot}/{def.yardId}";
            TestYardBuilder.EnsureFolder(dir);
            var p = def.palette;
            var set = new YardMaterialSet
            {
                grass = Mat(dir, "Grass", p.grass),
                wood = Mat(dir, "Wood", p.wood),
                walls = Mat(dir, "Walls", p.houseWalls),
                roof = Mat(dir, "Roof", p.roof),
                foliage = Mat(dir, "Foliage", p.foliage),
                hedge = Mat(dir, "Hedge", p.hedge),
                stone = Mat(dir, "Stone", p.stone),
                path = Mat(dir, "Path", p.path),
                flowers = Mat(dir, "Flowers", p.flowers),
                soil = Mat(dir, "Soil", new Color(0.30f, 0.21f, 0.13f)),
                compost = Mat(dir, "Compost", new Color(0.28f, 0.19f, 0.11f)),
                fertilizer = Mat(dir, "Fertilizer", new Color(0.55f, 0.78f, 0.30f)),
            };
            var colors = p.leafColors ?? new Color[0];
            set.leaves = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++) set.leaves[i] = Mat(dir, "Leaf_" + i, colors[i], instanced: true);
            return set;
        }

        static Material Mat(string dir, string name, Color color, bool instanced = false)
        {
            string path = $"{dir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(YardMaterialSet.LitShader());
                AssetDatabase.CreateAsset(mat, path);
            }
            YardMaterialSet.SetColor(mat, color);
            mat.enableInstancing = instanced || mat.enableInstancing;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ scene

        static string BuildScene(YardDefinition def, YardCatalog catalog)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mats = BuildMaterials(def);
            var p = def.palette;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = p.sunColor;
            sun.intensity = p.sunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(p.sunAngles.x, p.sunAngles.y, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.skyAmbient;
            RenderSettings.ambientEquatorColor = Color.Lerp(p.skyAmbient, p.groundAmbient, 0.5f);
            RenderSettings.ambientGroundColor = p.groundAmbient;

            YardGreybox.Build(def, mats);

            var field = new GameObject("LeafField").AddComponent<LeafField>();
            field.yardSize = def.size;
            field.leafCount = def.leafCount;
            field.seed = def.seed;
            field.leafMaterials = mats.leaves;

            var spawn = new Vector3(def.playerSpawn.x, 0.05f, def.playerSpawn.y);
            var shared = SharedMaterials();
            var rig = TestYardBuilder.BuildRig(spawn, catalog.unlockTable, shared.wood, shared.metal, shared.blower, shared.vacuum, shared.mulcher);
            rig.player.transform.rotation = Quaternion.Euler(0f, def.playerYaw, 0f);
            rig.managers.AddComponent<LevelFlow>().catalog = catalog;

            var loader = new GameObject("YardLoader").AddComponent<YardLoader>();
            loader.definition = def;

            // Time of day, weather, particles and post-processing (WP10). Its rig drives the sun
            // from here on; the palette's sun settings are the yard's starting light.
            LightingSetup.AddRigToScene();

            string path = $"{SceneDir}/{def.sceneName}.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        static (Material wood, Material metal, Material blower, Material vacuum, Material mulcher) SharedMaterials() => (
            TestYardBuilder.MakeMaterial("Wood", new Color(0.45f, 0.30f, 0.18f)),
            TestYardBuilder.MakeMaterial("ToolMetal", new Color(0.35f, 0.38f, 0.42f)),
            TestYardBuilder.MakeMaterial("BlowerBody", new Color(0.20f, 0.45f, 0.62f)),
            TestYardBuilder.MakeMaterial("VacuumBody", new Color(0.55f, 0.62f, 0.30f)),
            TestYardBuilder.MakeMaterial("MulcherBody", new Color(0.70f, 0.22f, 0.16f)));

        static void Report(List<string> problems)
        {
            if (problems.Count == 0)
            {
                Debug.Log("[FallenYard] Yards look good: no problems found.");
                return;
            }
            foreach (var p in problems) Debug.LogWarning("[FallenYard] Yard problem: " + p);
        }
    }
}

using System.IO;
using FallenYard.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FallenYard.EditorTools
{
    /// <summary>
    /// Menu: Fallen Yard > Add Lighting, Weather and VFX.
    /// Creates the WP10 assets (particle materials, autumn lighting preset, yard grade
    /// table), adds a baseline look to the project's DefaultVolumeProfile, and drops the
    /// rig into the open scene. Safe to run again: it reuses what already exists.
    /// </summary>
    public static class LightingSetup
    {
        const string Root = "Assets/VFX";
        const string MatDir = Root + "/Materials";
        const string SettingsDir = Root + "/Settings";
        const string DefaultProfilePath = "Assets/DefaultVolumeProfile.asset";
        const string RigName = "Lighting, Weather & VFX";

        [MenuItem("Fallen Yard/Add Lighting, Weather and VFX")]
        public static void AddToOpenScene()
        {
            var rig = AddRigToScene();
            EditorSceneManager.MarkSceneDirty(rig.scene);
            Selection.activeGameObject = rig;
            Debug.Log("[FallenYard] Lighting, weather and VFX rig added. Save the scene to keep it.");
        }

        /// <summary>Adds (or updates) the rig in the active scene. Used by the test yard builder too.</summary>
        public static GameObject AddRigToScene()
        {
            var assets = EnsureAssets();
            AddBaselineToDefaultProfile();

            var rig = GameObject.Find(RigName) ?? new GameObject(RigName);
            var weather = GetOrAdd<WeatherSystem>(rig);
            var lighting = GetOrAdd<LightingRig>(rig);
            lighting.preset = assets.preset;
            lighting.weather = weather;
            lighting.sun = FindSun();

            var vfx = GetOrAdd<LeafVfx>(rig);
            vfx.dustMaterial = assets.dust;
            vfx.leafMaterial = assets.leaf;

            var post = GetOrAdd<PostProcessDriver>(rig);
            post.grades = assets.grades;

            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = true;
            }

            if (lighting.sun != null)
            {
                RenderSettings.sun = lighting.sun;
                lighting.Apply(); // preview the start hour in the editor
            }
            return rig;
        }

        struct RigAssets
        {
            public Material dust, leaf;
            public LightingPreset preset;
            public YardGradeTable grades;
        }

        static RigAssets EnsureAssets()
        {
            EnsureFolder(MatDir);
            EnsureFolder(SettingsDir);
            var a = new RigAssets
            {
                dust = LoadOrCreate<Material>(MatDir + "/VFX_Dust.mat", () =>
                {
                    var m = ParticleFactory.CreateDustMaterial();
                    ParticleFactory.SetTexture(m, null); // swapped for the saved PNG below
                    return m;
                }),
                leaf = LoadOrCreate<Material>(MatDir + "/VFX_Leaf.mat", () => ParticleFactory.CreateLeafMaterial()),
                preset = LoadOrCreate<LightingPreset>(SettingsDir + "/LightingPreset_Autumn.asset", () => LightingPreset.CreateDefault()),
                grades = LoadOrCreate<YardGradeTable>(SettingsDir + "/YardGradeTable.asset", () => ScriptableObject.CreateInstance<YardGradeTable>()),
            };

            // Save the soft dot as a real texture so builds don't depend on runtime generation.
            string texPath = MatDir + "/VFX_SoftDot.png";
            if (!File.Exists(texPath))
            {
                const int size = 64;
                var readable = new Texture2D(size, size, TextureFormat.RGBA32, false);
                readable.SetPixels32(ParticleFactory.SoftDotPixels(size));
                readable.Apply();
                File.WriteAllBytes(texPath, readable.EncodeToPNG());
                Object.DestroyImmediate(readable);
                AssetDatabase.ImportAsset(texPath);
                if (AssetImporter.GetAtPath(texPath) is TextureImporter importer)
                {
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.SaveAndReimport();
                }
            }
            var dot = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (dot != null)
            {
                ParticleFactory.SetTexture(a.dust, dot);
                EditorUtility.SetDirty(a.dust);
            }
            AssetDatabase.SaveAssets();
            return a;
        }

        /// <summary>
        /// Adds a gentle baseline (neutral tonemapping, soft bloom, light vignette) to the
        /// project's default volume profile, only for overrides it doesn't have yet,
        /// so anything set by hand in Unity is kept.
        /// </summary>
        public static void AddBaselineToDefaultProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(DefaultProfilePath);
            if (profile == null) return;
            bool changed = false;

            changed |= AddIfMissing<Tonemapping>(profile, t =>
            {
                t.mode.Override(TonemappingMode.Neutral);
            });
            changed |= AddIfMissing<Bloom>(profile, b =>
            {
                b.threshold.Override(1.05f);
                b.intensity.Override(0.25f);
                b.scatter.Override(0.65f);
                b.tint.Override(new Color(1f, 0.93f, 0.82f));
            });
            changed |= AddIfMissing<Vignette>(profile, v =>
            {
                v.intensity.Override(0.2f);
                v.smoothness.Override(0.45f);
            });
            changed |= AddIfMissing<ColorAdjustments>(profile, c =>
            {
                c.contrast.Override(6f);
                c.saturation.Override(4f);
            });

            if (!changed) return;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        static bool AddIfMissing<T>(VolumeProfile profile, System.Action<T> configure) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing))
            {
                // URP may have filled the default profile with every component at default values.
                // Only fill one in if nobody has overridden anything on it yet.
                foreach (var p in existing.parameters)
                    if (p.overrideState) return false;
                configure(existing);
                existing.active = true;
                EditorUtility.SetDirty(existing);
                return true;
            }
            var c = profile.Add<T>(false);
            configure(c);
            c.name = typeof(T).Name;
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, profile);
            return true;
        }

        static Light FindSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) return l;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            return sun;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component =>
            go.TryGetComponent(out T c) ? c : go.AddComponent<T>();

        static T LoadOrCreate<T>(string path, System.Func<T> create) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = create();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

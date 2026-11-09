using System.IO;
using FallenYard.Core;
using FallenYard.Leaves;
using FallenYard.Player;
using FallenYard.Progression;
using FallenYard.Save;
using FallenYard.Settings;
using FallenYard.Tools;
using FallenYard.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FallenYard.EditorTools
{
    /// <summary>
    /// Menu: Fallen Yard > Build Test Yard.
    /// Generates materials, upgrade assets and a playable greybox scene so the
    /// repo works without shipping hand-made scene files.
    /// </summary>
    public static class TestYardBuilder
    {
        const string Root = "Assets/FallenYard";
        const string MatDir = Root + "/Materials";
        const string DataDir = Root + "/Data";
        const string SettingsDir = Root + "/Settings";
        const string ScenePath = "Assets/Scenes/TestYard.unity";
        const float YardSize = 30f;

        [MenuItem("Fallen Yard/Set Up URP If Missing")]
        public static void EnsureUrp()
        {
            if (GraphicsSettings.defaultRenderPipeline != null) return;
            EnsureFolder(SettingsDir);
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, SettingsDir + "/FallenYard_Renderer.asset");
            var urp = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(urp, SettingsDir + "/FallenYard_URP.asset");
            AssetDatabase.SaveAssets();
            GraphicsSettings.defaultRenderPipeline = urp;
            QualitySettings.renderPipeline = urp;
            Debug.Log("[FallenYard] Created and assigned a URP asset. If materials look pink, see README > Troubleshooting.");
        }

        [MenuItem("Fallen Yard/Build Test Yard")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureUrp();
            EnsureFolder(MatDir);
            EnsureFolder(DataDir);
            EnsureFolder("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Materials (cozy autumn palette)
            var grass = MakeMaterial("Grass", new Color(0.42f, 0.58f, 0.27f));
            var wood = MakeMaterial("Wood", new Color(0.45f, 0.30f, 0.18f));
            var canopy = MakeMaterial("Canopy", new Color(0.86f, 0.45f, 0.14f));
            var metal = MakeMaterial("ToolMetal", new Color(0.35f, 0.38f, 0.42f));
            var blowerBody = MakeMaterial("BlowerBody", new Color(0.20f, 0.45f, 0.62f));
            Material[] leafMats =
            {
                MakeMaterial("Leaf_Orange", new Color(0.88f, 0.42f, 0.10f), true),
                MakeMaterial("Leaf_Red", new Color(0.72f, 0.18f, 0.09f), true),
                MakeMaterial("Leaf_Yellow", new Color(0.94f, 0.70f, 0.18f), true),
                MakeMaterial("Leaf_Brown", new Color(0.55f, 0.32f, 0.13f), true),
            };

            // Lighting
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.9f, 0.75f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.75f, 0.78f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.55f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.25f, 0.2f);

            // Ground + fence
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3((YardSize + 10f) / 10f, 1f, (YardSize + 10f) / 10f);
            ground.GetComponent<Renderer>().sharedMaterial = grass;

            var fence = new GameObject("Fence");
            float half = YardSize / 2f + 0.3f;
            MakeFence(fence.transform, new Vector3(0, 0.5f, half), new Vector3(YardSize + 0.6f, 1f, 0.15f), wood);
            MakeFence(fence.transform, new Vector3(0, 0.5f, -half), new Vector3(YardSize + 0.6f, 1f, 0.15f), wood);
            MakeFence(fence.transform, new Vector3(half, 0.5f, 0), new Vector3(0.15f, 1f, YardSize + 0.6f), wood);
            MakeFence(fence.transform, new Vector3(-half, 0.5f, 0), new Vector3(0.15f, 1f, YardSize + 0.6f), wood);

            // A few greybox trees
            var trees = new GameObject("Trees");
            MakeTree(trees.transform, new Vector3(-9f, 0, 8f), wood, canopy);
            MakeTree(trees.transform, new Vector3(10f, 0, 10f), wood, canopy);
            MakeTree(trees.transform, new Vector3(7f, 0, -9f), wood, canopy);

            // Leaves
            var fieldGo = new GameObject("LeafField");
            var field = fieldGo.AddComponent<LeafField>();
            field.yardSize = new Vector2(YardSize, YardSize);
            field.leafCount = 12000;
            field.leafMaterials = leafMats;

            // Upgrade data
            var rakeDef = MakeUpgrade("Upgrade_Rake", "rake", "Rake", true, 0,
                L(60, 1.3f, 1.15f), L(150, 1.6f, 1.3f), L(350, 2.0f, 1.5f));
            var blowerDef = MakeUpgrade("Upgrade_Blower", "blower", "Leaf Blower", false, 200,
                L(180, 1.2f, 1.3f, 1.2f), L(400, 1.4f, 1.6f, 1.4f), L(800, 1.6f, 2f, 1.7f));
            var bagDef = MakeUpgrade("Upgrade_Bag", "bag", "Bigger Bags", true, 0,
                L(80, capacity: 1.6f), L(220, capacity: 2.5f), L(500, capacity: 4f));

            // Managers
            var managers = new GameObject("Managers");
            managers.AddComponent<GameManager>();
            managers.AddComponent<UpgradeManager>().definitions = new[] { rakeDef, blowerDef, bagDef };
            managers.AddComponent<PileDetector>();
            managers.AddComponent<YardProgress>();
            managers.AddComponent<SettingsManager>();
            managers.AddComponent<Autosaver>();
            managers.AddComponent<MetaProgressionService>().unlockTable = MakeYardUnlockTable();
            var hud = managers.AddComponent<DebugHUD>();

            // Player
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0.05f, -YardSize / 2f + 2f);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            var camGo = new GameObject("PlayerCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 70f;
            camGo.AddComponent<AudioListener>();

            var fpc = player.AddComponent<FirstPersonController>();
            fpc.cameraPivot = camGo.transform;

            var rake = player.AddComponent<Rake>();
            rake.toolId = "rake"; rake.displayName = "Rake";
            rake.baseRadius = 0.9f; rake.baseStrength = 10f; rake.baseRange = 2.6f;
            rake.viewModel = MakeRakeModel(camGo.transform, wood, metal);

            var blower = player.AddComponent<LeafBlower>();
            blower.toolId = "blower"; blower.displayName = "Leaf Blower";
            blower.baseRadius = 1f; blower.baseStrength = 22f; blower.baseRange = 5f;
            blower.viewModel = MakeBlowerModel(camGo.transform, blowerBody, metal);

            var tools = player.AddComponent<ToolController>();
            tools.playerCamera = cam;
            tools.tools.Add(rake);
            tools.tools.Add(blower);

            var bag = player.AddComponent<BagInteractor>();
            bag.playerCamera = cam;

            hud.toolController = tools;
            hud.bagInteractor = bag;
            hud.yardProgress = managers.GetComponent<YardProgress>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            var buildScenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorBuildSettings.scenes = buildScenes;
            AssetDatabase.SaveAssets();
            Debug.Log("[FallenYard] Test yard built at " + ScenePath + ". Press Play!");
        }

        // ------------------------------------------------------------------ helpers

        static ToolUpgradeDefinition.UpgradeLevel L(int cost, float radius = 1f, float strength = 1f, float range = 1f, float capacity = 1f) =>
            new ToolUpgradeDefinition.UpgradeLevel
            {
                cost = cost,
                radiusMultiplier = radius,
                strengthMultiplier = strength,
                rangeMultiplier = range,
                capacityMultiplier = capacity,
            };

        /// <summary>Placeholder unlock table until WP7 adds the real yards. The test yard is "yard_01".</summary>
        static YardUnlockTable MakeYardUnlockTable()
        {
            string path = $"{DataDir}/YardUnlockTable.asset";
            var table = AssetDatabase.LoadAssetAtPath<YardUnlockTable>(path);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<YardUnlockTable>();
                table.yards.Add(new YardUnlockRule { yardId = "yard_01", displayName = "Test Yard", unlockedByDefault = true, rewardToolIds = new[] { "blower" } });
                AssetDatabase.CreateAsset(table, path);
            }
            return table;
        }

        static ToolUpgradeDefinition MakeUpgrade(string file, string id, string display, bool unlocked, int unlockCost,
            params ToolUpgradeDefinition.UpgradeLevel[] levels)
        {
            string path = $"{DataDir}/{file}.asset";
            var def = AssetDatabase.LoadAssetAtPath<ToolUpgradeDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<ToolUpgradeDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }
            def.toolId = id;
            def.displayName = display;
            def.unlockedByDefault = unlocked;
            def.unlockCost = unlockCost;
            def.levels = levels;
            EditorUtility.SetDirty(def);
            return def;
        }

        static Material MakeMaterial(string name, Color color, bool instanced = false)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.1f);
            mat.enableInstancing = instanced || mat.enableInstancing;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void MakeFence(Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "FenceSegment";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static void MakeTree(Transform parent, Vector3 pos, Material trunkMat, Material canopyMat)
        {
            var tree = new GameObject("Tree");
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = pos;

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localPosition = new Vector3(0, 1.5f, 0);
            trunk.transform.localScale = new Vector3(0.45f, 1.5f, 0.45f);
            trunk.GetComponent<Renderer>().sharedMaterial = trunkMat;

            var top = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            top.transform.SetParent(tree.transform, false);
            top.transform.localPosition = new Vector3(0, 4f, 0);
            top.transform.localScale = new Vector3(3.2f, 2.6f, 3.2f);
            top.GetComponent<Renderer>().sharedMaterial = canopyMat;
            Object.DestroyImmediate(top.GetComponent<Collider>());
        }

        static GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static GameObject MakeRakeModel(Transform cam, Material wood, Material metal)
        {
            var root = new GameObject("RakeViewModel");
            root.transform.SetParent(cam, false);
            root.transform.localPosition = new Vector3(0.35f, -0.45f, 0.6f);
            root.transform.localEulerAngles = new Vector3(55f, -10f, 0f);
            Part(PrimitiveType.Cylinder, root.transform, Vector3.zero, Vector3.zero, new Vector3(0.03f, 0.8f, 0.03f), wood);
            Part(PrimitiveType.Cube, root.transform, new Vector3(0, -0.8f, 0), Vector3.zero, new Vector3(0.45f, 0.03f, 0.06f), metal);
            return root;
        }

        static GameObject MakeBlowerModel(Transform cam, Material body, Material metal)
        {
            var root = new GameObject("BlowerViewModel");
            root.transform.SetParent(cam, false);
            root.transform.localPosition = new Vector3(0.3f, -0.35f, 0.5f);
            Part(PrimitiveType.Cube, root.transform, Vector3.zero, Vector3.zero, new Vector3(0.18f, 0.2f, 0.3f), body);
            Part(PrimitiveType.Cylinder, root.transform, new Vector3(0, -0.02f, 0.35f), new Vector3(90f, 0, 0), new Vector3(0.07f, 0.25f, 0.07f), metal);
            root.SetActive(false);
            return root;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

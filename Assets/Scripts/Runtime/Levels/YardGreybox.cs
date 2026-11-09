using System.Collections.Generic;
using FallenYard.Tools;
using UnityEngine;
using UnityEngine.Rendering;

namespace FallenYard.Levels
{
    /// <summary>What <see cref="YardGreybox.Build"/> made, for the loader and the restoration hook.</summary>
    public class YardBuildResult
    {
        public GameObject root;
        public Renderer ground;
        public CompostBin compostBin;
        public Transform mailbox;
        public readonly List<YardRestoration.RegrowthItem> regrowth = new List<YardRestoration.RegrowthItem>();
        public int obstacleCount;
    }

    /// <summary>
    /// Builds a yard's greybox from its <see cref="YardDefinition"/> out of Unity primitives:
    /// ground, fence with a gate, houses, trees, hedges, props, mailbox, compost bin and the
    /// hidden flowers that grow back as the yard is restored. Works in the editor (the scene
    /// builder bakes it into each yard scene) and at runtime (tests). WP9 art swaps the
    /// primitives for real meshes later; names and layout stay the same.
    /// </summary>
    public static class YardGreybox
    {
        const float FenceHeight = 1f;
        const float GateWidth = 2f;

        public static YardBuildResult Build(YardDefinition def, YardMaterialSet mats, Transform parent = null)
        {
            mats ??= new YardMaterialSet();
            var result = new YardBuildResult();
            var root = new GameObject("Yard_" + def.yardId);
            if (parent != null) root.transform.SetParent(parent, false);
            result.root = root;

            // Ground: a little bigger than the fence so the edge doesn't show.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform, false);
            ground.transform.localScale = new Vector3((def.size.x + 16f) / 10f, 1f, (def.size.y + 16f) / 10f);
            result.ground = ground.GetComponent<Renderer>();
            result.ground.sharedMaterial = mats.grass;

            BuildFence(def, root.transform, mats.wood);

            var props = new GameObject("Props").transform;
            props.SetParent(root.transform, false);
            for (int i = 0; i < def.obstacles.Count; i++)
            {
                var o = def.obstacles[i];
                if (o == null) continue;
                var go = BuildObstacle(o, mats, props, result);
                go.name = $"{o.kind}_{i:00}";
                result.obstacleCount++;
            }

            result.mailbox = BuildMailbox(def.mailboxPosition, mats, root.transform).transform;
            result.compostBin = BuildCompostBin(def.compostBinPosition, mats, root.transform);
            BuildFlowerClusters(def, mats, root.transform, result);

            var restoration = root.AddComponent<YardRestoration>();
            restoration.grassRenderers = new[] { result.ground };
            restoration.grassColor = def.palette.grass;
            restoration.restoredGrassColor = def.palette.grassRestored;
            restoration.regrowth = new List<YardRestoration.RegrowthItem>(result.regrowth);
            restoration.ApplyImmediately(0f);
            return result;
        }

        // ------------------------------------------------------------------ fence

        static void BuildFence(YardDefinition def, Transform parent, Material wood)
        {
            var fence = new GameObject("Fence").transform;
            fence.SetParent(parent, false);
            float hx = def.size.x * 0.5f + 0.3f, hz = def.size.y * 0.5f + 0.3f;
            float y = FenceHeight * 0.5f;
            const float t = 0.15f;

            Segment(fence, new Vector3(0f, y, hz), new Vector3(hx * 2f, FenceHeight, t), wood);
            Segment(fence, new Vector3(hx, y, 0f), new Vector3(t, FenceHeight, hz * 2f), wood);
            Segment(fence, new Vector3(-hx, y, 0f), new Vector3(t, FenceHeight, hz * 2f), wood);

            // Front fence with a gate gap.
            float gate = Mathf.Clamp(def.gateX, -hx + GateWidth, hx - GateWidth);
            float leftEnd = gate - GateWidth * 0.5f, rightStart = gate + GateWidth * 0.5f;
            float leftLen = leftEnd + hx, rightLen = hx - rightStart;
            if (leftLen > 0.05f) Segment(fence, new Vector3(-hx + leftLen * 0.5f, y, -hz), new Vector3(leftLen, FenceHeight, t), wood);
            if (rightLen > 0.05f) Segment(fence, new Vector3(rightStart + rightLen * 0.5f, y, -hz), new Vector3(rightLen, FenceHeight, t), wood);
            Segment(fence, new Vector3(leftEnd, 0.7f, -hz), new Vector3(0.22f, 1.4f, 0.22f), wood).name = "GatePost";
            Segment(fence, new Vector3(rightStart, 0.7f, -hz), new Vector3(0.22f, 1.4f, 0.22f), wood).name = "GatePost";
        }

        static GameObject Segment(Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var go = Part(PrimitiveType.Cube, parent, pos, Vector3.zero, size, mat, collider: true);
            go.name = "FenceSegment";
            return go;
        }

        // ------------------------------------------------------------------ obstacles

        static GameObject BuildObstacle(YardObstacle o, YardMaterialSet m, Transform parent, YardBuildResult result)
        {
            var root = new GameObject(o.kind.ToString());
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(o.position.x, 0f, o.position.y);
            root.transform.localRotation = Quaternion.Euler(0f, o.rotation, 0f);
            var t = root.transform;
            Vector3 s = o.size;

            switch (o.kind)
            {
                case ObstacleKind.House:
                case ObstacleKind.Shed:
                    BuildHouse(t, s, m, o.kind == ObstacleKind.Shed);
                    break;
                case ObstacleKind.Tree:
                    Part(PrimitiveType.Cylinder, t, new Vector3(0f, s.y * 0.25f, 0f), Vector3.zero,
                        new Vector3(YardRules.TrunkRadius(o) * 2f, s.y * 0.25f, YardRules.TrunkRadius(o) * 2f), m.wood, collider: true);
                    Part(PrimitiveType.Sphere, t, new Vector3(0f, s.y * 0.7f, 0f), Vector3.zero, new Vector3(s.x, s.y * 0.55f, s.z), m.foliage);
                    break;
                case ObstacleKind.FruitTree:
                    Part(PrimitiveType.Cylinder, t, new Vector3(0f, s.y * 0.2f, 0f), Vector3.zero,
                        new Vector3(YardRules.TrunkRadius(o) * 2f, s.y * 0.2f, YardRules.TrunkRadius(o) * 2f), m.wood, collider: true);
                    Part(PrimitiveType.Sphere, t, new Vector3(0f, s.y * 0.65f, 0f), Vector3.zero, new Vector3(s.x, s.y * 0.6f, s.z), m.hedge);
                    Part(PrimitiveType.Sphere, t, new Vector3(s.x * 0.3f, s.y * 0.55f, s.z * 0.25f), Vector3.zero, Vector3.one * 0.6f, m.foliage);
                    Part(PrimitiveType.Sphere, t, new Vector3(-s.x * 0.25f, s.y * 0.7f, -s.z * 0.3f), Vector3.zero, Vector3.one * 0.55f, m.foliage);
                    break;
                case ObstacleKind.PineTree:
                    Part(PrimitiveType.Cylinder, t, new Vector3(0f, s.y * 0.15f, 0f), Vector3.zero,
                        new Vector3(YardRules.TrunkRadius(o) * 2f, s.y * 0.15f, YardRules.TrunkRadius(o) * 2f), m.wood, collider: true);
                    for (int i = 0; i < 3; i++)
                    {
                        float k = 1f - i * 0.3f;
                        Part(PrimitiveType.Cylinder, t, new Vector3(0f, s.y * (0.38f + i * 0.2f), 0f), Vector3.zero,
                            new Vector3(s.x * k, s.y * 0.1f, s.z * k), m.hedge);
                    }
                    break;
                case ObstacleKind.Hedge:
                    Part(PrimitiveType.Cube, t, new Vector3(0f, s.y * 0.5f, 0f), Vector3.zero, s, m.hedge, collider: true);
                    break;
                case ObstacleKind.Bush:
                    Part(PrimitiveType.Sphere, t, new Vector3(0f, s.y * 0.45f, 0f), Vector3.zero, s, m.hedge, collider: true);
                    break;
                case ObstacleKind.Rock:
                    Part(PrimitiveType.Sphere, t, new Vector3(0f, s.y * 0.35f, 0f), new Vector3(8f, 0f, 12f), s, m.stone, collider: true);
                    break;
                case ObstacleKind.Bench:
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 0.45f, 0f), Vector3.zero, new Vector3(s.x, 0.08f, s.z), m.wood, collider: true);
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 0.75f, -s.z * 0.45f), Vector3.zero, new Vector3(s.x, 0.5f, 0.06f), m.wood);
                    Part(PrimitiveType.Cube, t, new Vector3(-s.x * 0.42f, 0.22f, 0f), Vector3.zero, new Vector3(0.08f, 0.44f, s.z * 0.9f), m.wood);
                    Part(PrimitiveType.Cube, t, new Vector3(s.x * 0.42f, 0.22f, 0f), Vector3.zero, new Vector3(0.08f, 0.44f, s.z * 0.9f), m.wood);
                    break;
                case ObstacleKind.Wall:
                case ObstacleKind.Gravestone:
                    Part(PrimitiveType.Cube, t, new Vector3(0f, s.y * 0.5f, 0f), Vector3.zero, s, m.stone, collider: true);
                    break;
                case ObstacleKind.Path:
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 0.005f, 0f), Vector3.zero, new Vector3(s.x, 0.01f, s.z), m.path)
                        .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    break;
                case ObstacleKind.Flowerbed:
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 0.04f, 0f), Vector3.zero, new Vector3(s.x, 0.08f, s.z), m.soil);
                    var bloom = new GameObject("Blooms");
                    bloom.transform.SetParent(t, false);
                    int n = Mathf.Clamp(Mathf.RoundToInt(s.x * s.z * 3f), 3, 24);
                    for (int i = 0; i < n; i++)
                    {
                        float u = (i + 0.5f) / n;
                        var p = new Vector3((u - 0.5f) * s.x * 0.9f, 0.18f, ((i * 7) % 5 / 4f - 0.5f) * s.z * 0.7f);
                        Part(PrimitiveType.Sphere, bloom.transform, p, Vector3.zero, Vector3.one * 0.18f, m.flowers);
                    }
                    result.regrowth.Add(new YardRestoration.RegrowthItem { target = bloom, showAt = 0.3f });
                    break;
                case ObstacleKind.Fountain:
                    Part(PrimitiveType.Cylinder, t, new Vector3(0f, s.y * 0.25f, 0f), Vector3.zero, new Vector3(s.x, s.y * 0.25f, s.z), m.stone, collider: true);
                    Part(PrimitiveType.Cylinder, t, new Vector3(0f, s.y * 0.8f, 0f), Vector3.zero, new Vector3(s.x * 0.2f, s.y * 0.35f, s.z * 0.2f), m.stone);
                    break;
            }
            return root;
        }

        static void BuildHouse(Transform t, Vector3 s, YardMaterialSet m, bool shed)
        {
            float wallH = s.y * (shed ? 0.7f : 0.62f);
            Part(PrimitiveType.Cube, t, new Vector3(0f, wallH * 0.5f, 0f), Vector3.zero, new Vector3(s.x, wallH, s.z), shed ? m.wood : m.walls, collider: true);

            // Gable roof from two tilted slabs.
            const float pitch = 32f;
            float halfD = s.z * 0.5f + 0.3f;
            float len = halfD / Mathf.Cos(pitch * Mathf.Deg2Rad);
            float rise = halfD * Mathf.Tan(pitch * Mathf.Deg2Rad);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(PrimitiveType.Cube, t, new Vector3(0f, wallH + rise * 0.5f, side * halfD * 0.5f), new Vector3(side * pitch, 0f, 0f),
                    new Vector3(s.x + 0.4f, 0.18f, len), m.roof);
            }

            // Door on the street side (-Z) so every house faces the player.
            Part(PrimitiveType.Cube, t, new Vector3(0f, 1f, -s.z * 0.5f - 0.03f), Vector3.zero, new Vector3(1f, 2f, 0.08f), m.wood);
            if (!shed)
            {
                Part(PrimitiveType.Cube, t, new Vector3(-s.x * 0.28f, wallH * 0.6f, -s.z * 0.5f - 0.03f), Vector3.zero, new Vector3(1.1f, 0.9f, 0.06f), m.path);
                Part(PrimitiveType.Cube, t, new Vector3(s.x * 0.28f, wallH * 0.6f, -s.z * 0.5f - 0.03f), Vector3.zero, new Vector3(1.1f, 0.9f, 0.06f), m.path);
            }
        }

        static GameObject BuildMailbox(Vector2 pos, YardMaterialSet m, Transform parent)
        {
            var root = new GameObject("Mailbox");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(pos.x, 0f, pos.y);
            Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(0.1f, 1.1f, 0.1f), m.wood, collider: true);
            Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 1.2f, 0f), Vector3.zero, new Vector3(0.3f, 0.25f, 0.5f), m.roof);
            Part(PrimitiveType.Cube, root.transform, new Vector3(0.17f, 1.32f, 0.1f), Vector3.zero, new Vector3(0.03f, 0.2f, 0.12f), m.flowers).name = "Flag";
            return root;
        }

        static CompostBin BuildCompostBin(Vector2 pos, YardMaterialSet m, Transform parent)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "CompostBin";
            root.SetActive(false); // so CompostBin.Awake runs after its visuals are assigned
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(pos.x, 0.5f, pos.y);
            root.transform.localScale = new Vector3(YardRules.CompostBinSize, 1f, YardRules.CompostBinSize);
            root.GetComponent<Renderer>().sharedMaterial = m.wood;
            var bin = root.AddComponent<CompostBin>();

            var fillPivot = new GameObject("FillPivot").transform;
            fillPivot.SetParent(root.transform, false);
            fillPivot.localPosition = new Vector3(0f, 0.5f, 0f);
            Part(PrimitiveType.Cube, fillPivot, new Vector3(0f, 0.25f, 0f), Vector3.zero, new Vector3(0.9f, 0.5f, 0.9f), m.compost);
            fillPivot.localScale = new Vector3(1f, 0.3f, 1f);
            bin.fillVisual = fillPivot;

            var ready = Part(PrimitiveType.Sphere, root.transform, new Vector3(0f, 1.1f, 0f), Vector3.zero, new Vector3(0.3f, 0.3f, 0.3f), m.fertilizer);
            ready.name = "FertilizerReady";
            ready.SetActive(false);
            bin.readyIndicator = ready;
            root.SetActive(true);
            return bin;
        }

        /// <summary>Wildflowers scattered over open grass; they appear one by one as restoration rises.</summary>
        static void BuildFlowerClusters(YardDefinition def, YardMaterialSet m, Transform parent, YardBuildResult result)
        {
            var group = new GameObject("Regrowth").transform;
            group.SetParent(parent, false);
            int count = FlowerClusterCount(def.size);
            var rng = new System.Random(def.seed + 17);
            for (int i = 0; i < count; i++)
            {
                Vector2 p = Vector2.zero;
                bool found = false;
                for (int attempt = 0; attempt < 12 && !found; attempt++)
                {
                    p = new Vector2(((float)rng.NextDouble() - 0.5f) * (def.size.x - 2f), ((float)rng.NextDouble() - 0.5f) * (def.size.y - 2f));
                    found = YardRules.IsOpenGround(def, p, 0.6f) && !OnPath(def, p);
                }
                if (!found) continue;

                var cluster = new GameObject($"Wildflowers_{i:00}");
                cluster.transform.SetParent(group, false);
                cluster.transform.localPosition = new Vector3(p.x, 0f, p.y);
                for (int f = 0; f < 3; f++)
                {
                    float a = f * 2.1f + i;
                    Part(PrimitiveType.Sphere, cluster.transform, new Vector3(Mathf.Cos(a) * 0.25f, 0.12f, Mathf.Sin(a) * 0.25f), Vector3.zero,
                        Vector3.one * 0.16f, m.flowers);
                }
                result.regrowth.Add(new YardRestoration.RegrowthItem { target = cluster, showAt = 0.15f + 0.85f * (i + 1f) / count });
            }
        }

        public static int FlowerClusterCount(Vector2 size) => Mathf.Clamp(Mathf.RoundToInt(size.x * size.y / 40f), 6, 30);

        static bool OnPath(YardDefinition def, Vector2 p)
        {
            foreach (var o in def.obstacles)
                if (o != null && (o.kind == ObstacleKind.Path || o.kind == ObstacleKind.Flowerbed) && YardRules.Contains(o, p, 0.3f)) return true;
            return false;
        }

        // ------------------------------------------------------------------ primitives

        static GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material mat, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            return go;
        }
    }
}

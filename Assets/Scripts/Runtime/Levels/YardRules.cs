using System.Collections.Generic;
using UnityEngine;

namespace FallenYard.Levels
{
    /// <summary>Geometry and validation rules for yards. Pure functions, so they're easy to test.</summary>
    public static class YardRules
    {
        /// <summary>Size of the compost bin's footprint (metres, square).</summary>
        public const float CompostBinSize = 1.4f;
        /// <summary>Smallest yard side the fence and the tools are tuned for.</summary>
        public const float MinYardSide = 8f;

        public static bool BlocksLeaves(ObstacleKind kind)
        {
            switch (kind)
            {
                case ObstacleKind.Bench:
                case ObstacleKind.Path:
                case ObstacleKind.Flowerbed:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>True for things the player walks into (they get a collider).</summary>
        public static bool IsSolid(ObstacleKind kind) => kind != ObstacleKind.Path && kind != ObstacleKind.Flowerbed;

        public static bool IsTree(ObstacleKind kind) =>
            kind == ObstacleKind.Tree || kind == ObstacleKind.PineTree || kind == ObstacleKind.FruitTree;

        /// <summary>Round footprint (trees, bushes, rocks, fountains) instead of a rotated box.</summary>
        public static bool IsRound(ObstacleKind kind) =>
            IsTree(kind) || kind == ObstacleKind.Bush || kind == ObstacleKind.Rock || kind == ObstacleKind.Fountain;

        /// <summary>Radius of a tree's trunk; leaves pile under the canopy but not inside the trunk.</summary>
        public static float TrunkRadius(YardObstacle o) => Mathf.Max(0.25f, o.size.x * 0.08f);

        /// <summary>Radius of a round obstacle's footprint.</summary>
        public static float FootprintRadius(YardObstacle o) =>
            IsTree(o.kind) ? TrunkRadius(o) : Mathf.Max(o.size.x, o.size.z) * 0.5f;

        /// <summary>Is the yard-local point inside the obstacle's footprint (grown by <paramref name="margin"/>)?</summary>
        public static bool Contains(YardObstacle o, Vector2 p, float margin = 0f)
        {
            if (o == null) return false;
            Vector2 d = p - o.position;
            if (IsRound(o.kind))
            {
                float r = FootprintRadius(o) + margin;
                return d.sqrMagnitude <= r * r;
            }
            float a = o.rotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            // Undo the obstacle's Y rotation (Quaternion.Euler(0, rotation, 0)) to get the offset in its own frame.
            var local = new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos);
            return Mathf.Abs(local.x) <= o.size.x * 0.5f + margin && Mathf.Abs(local.y) <= o.size.z * 0.5f + margin;
        }

        public static bool InsideYard(Vector2 size, Vector2 p, float margin = 0f) =>
            Mathf.Abs(p.x) <= size.x * 0.5f - margin && Mathf.Abs(p.y) <= size.y * 0.5f - margin;

        public static bool InsideCompostBin(YardDefinition def, Vector2 p, float margin = 0f)
        {
            Vector2 d = p - def.compostBinPosition;
            float h = CompostBinSize * 0.5f + margin;
            return Mathf.Abs(d.x) <= h && Mathf.Abs(d.y) <= h;
        }

        /// <summary>True if a leaf (or the player) may stand at this yard-local point.</summary>
        public static bool IsOpenGround(YardDefinition def, Vector2 p, float margin = 0f)
        {
            if (!InsideYard(def.size, p)) return false;
            if (InsideCompostBin(def, p, margin)) return false;
            foreach (var o in def.obstacles)
                if (o != null && o.BlocksLeaves && Contains(o, p, margin)) return false;
            return true;
        }

        public static List<string> Validate(YardDefinition def)
        {
            var problems = new List<string>();
            if (def == null)
            {
                problems.Add("Yard definition is missing.");
                return problems;
            }
            string id = string.IsNullOrEmpty(def.yardId) ? "(no id)" : def.yardId;
            if (string.IsNullOrEmpty(def.yardId)) problems.Add("Yard has no id.");
            if (string.IsNullOrEmpty(def.displayName)) problems.Add($"'{id}' has no display name.");
            if (string.IsNullOrEmpty(def.sceneName)) problems.Add($"'{id}' has no scene name.");
            if (def.size.x < MinYardSide || def.size.y < MinYardSide) problems.Add($"'{id}' is smaller than {MinYardSide} m on a side.");
            if (def.leafCount <= 0) problems.Add($"'{id}' has no leaves.");
            if (def.completionBonus < 0) problems.Add($"'{id}' has a negative completion bonus.");

            float mix = 0f;
            foreach (var m in def.leafMix) if (m != null) mix += Mathf.Max(0f, m.weight);
            if (def.leafMix.Count > 0 && mix <= 0f) problems.Add($"'{id}' has a leaf mix with no weight.");

            float zoneWeight = def.backgroundWeight;
            for (int i = 0; i < def.spawnZones.Count; i++)
            {
                var z = def.spawnZones[i];
                if (z == null) continue;
                zoneWeight += Mathf.Max(0f, z.weight);
                if (z.weight < 0f) problems.Add($"'{id}' zone '{z.label}' has a negative weight.");
                if (!InsideYard(def.size, z.center)) problems.Add($"'{id}' zone '{z.label}' is centred outside the yard.");
            }
            if (zoneWeight <= 0f) problems.Add($"'{id}' gives leaves nowhere to spawn (all weights are 0).");

            for (int i = 0; i < def.obstacles.Count; i++)
            {
                var o = def.obstacles[i];
                if (o == null) continue;
                if (!InsideYard(def.size, o.position)) problems.Add($"'{id}' {o.kind} #{i} stands outside the yard.");
            }

            if (!IsOpenGround(def, def.playerSpawn, 0.4f)) problems.Add($"'{id}' player spawn is blocked or outside the yard.");
            if (!InsideYard(def.size, def.compostBinPosition, CompostBinSize * 0.5f)) problems.Add($"'{id}' compost bin is outside the yard.");
            foreach (var o in def.obstacles)
                if (o != null && o.BlocksLeaves && Contains(o, def.compostBinPosition, CompostBinSize * 0.5f))
                {
                    problems.Add($"'{id}' compost bin overlaps a {o.kind}.");
                    break;
                }
            return problems;
        }
    }
}

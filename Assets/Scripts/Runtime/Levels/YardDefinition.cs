using System;
using System.Collections.Generic;
using FallenYard.Leaves;
using UnityEngine;

namespace FallenYard.Levels
{
    /// <summary>What a piece of yard furniture is. Drives the greybox shape, its collider and its leaf footprint.</summary>
    public enum ObstacleKind
    {
        House,
        Shed,
        Tree,
        PineTree,
        FruitTree,
        Hedge,
        Bush,
        Rock,
        Bench,
        Wall,
        Path,
        Flowerbed,
        Gravestone,
        Fountain,
    }

    public enum ZoneShape
    {
        Rectangle,
        Circle,
    }

    /// <summary>A share of the yard's leaves: under a tree, along a hedge, in a corner the wind fills.</summary>
    [Serializable]
    public class LeafSpawnZone
    {
        public string label = "Zone";
        public ZoneShape shape = ZoneShape.Circle;
        [Tooltip("Centre in yard-local metres (X, Z). (0, 0) is the middle of the yard.")]
        public Vector2 center;
        [Tooltip("Rectangle: full width and depth. Circle: X is the radius.")]
        public Vector2 size = new Vector2(4f, 4f);
        [Tooltip("Relative share of the yard's leaves that land here.")]
        [Min(0f)] public float weight = 1f;

        public float Area => shape == ZoneShape.Circle ? Mathf.PI * size.x * size.x : size.x * size.y;
    }

    /// <summary>One thing standing in the yard.</summary>
    [Serializable]
    public class YardObstacle
    {
        public ObstacleKind kind = ObstacleKind.Tree;
        [Tooltip("Centre in yard-local metres (X, Z).")]
        public Vector2 position;
        [Tooltip("Width (X), height (Y) and depth (Z) in metres. Trees use X as the canopy diameter.")]
        public Vector3 size = new Vector3(1f, 1f, 1f);
        [Tooltip("Turn around Y in degrees.")]
        public float rotation;

        /// <summary>True when leaves must not spawn inside its footprint.</summary>
        public bool BlocksLeaves => YardRules.BlocksLeaves(kind);
    }

    [Serializable]
    public class LeafMixEntry
    {
        public LeafPreset preset = LeafPreset.Oak;
        [Min(0f)] public float weight = 1f;
    }

    /// <summary>The yard's colours. Builders turn these into materials; restoration lerps the grass.</summary>
    [Serializable]
    public class YardPalette
    {
        public Color grass = new Color(0.42f, 0.55f, 0.27f);
        [Tooltip("Grass once the compost bin has fully restored the yard.")]
        public Color grassRestored = new Color(0.36f, 0.68f, 0.30f);
        public Color wood = new Color(0.45f, 0.30f, 0.18f);
        public Color houseWalls = new Color(0.86f, 0.78f, 0.64f);
        public Color roof = new Color(0.55f, 0.24f, 0.18f);
        public Color foliage = new Color(0.86f, 0.45f, 0.14f);
        public Color hedge = new Color(0.30f, 0.42f, 0.22f);
        public Color stone = new Color(0.62f, 0.60f, 0.56f);
        public Color path = new Color(0.70f, 0.62f, 0.50f);
        public Color flowers = new Color(0.93f, 0.62f, 0.72f);
        [Tooltip("Leaf colour variants, picked at random per leaf.")]
        public Color[] leafColors =
        {
            new Color(0.88f, 0.42f, 0.10f),
            new Color(0.72f, 0.18f, 0.09f),
            new Color(0.94f, 0.70f, 0.18f),
            new Color(0.55f, 0.32f, 0.13f),
        };
        public Color sunColor = new Color(1f, 0.9f, 0.75f);
        [Range(0f, 3f)] public float sunIntensity = 1.2f;
        [Tooltip("Sun pitch (X) and heading (Y) in degrees.")]
        public Vector2 sunAngles = new Vector2(38f, -35f);
        public Color skyAmbient = new Color(0.75f, 0.78f, 0.85f);
        public Color groundAmbient = new Color(0.30f, 0.25f, 0.20f);
    }

    /// <summary>Wind for this yard; copied onto the WindSystem's <see cref="GustSettings"/>.</summary>
    [Serializable]
    public class YardWeather
    {
        [Tooltip("Degrees, 0 = towards +Z, 90 = towards +X.")]
        public float prevailingDirection = 30f;
        [Min(0f)] public float breezeStrength = 0.7f;
        public bool gustsEnabled = true;
        [Min(0f)] public float firstGustDelay = 25f;
        public Vector2 gustInterval = new Vector2(30f, 60f);
        public Vector2 gustStrength = new Vector2(3f, 5.5f);

        public void ApplyTo(GustSettings s)
        {
            if (s == null) return;
            s.prevailingDirection = prevailingDirection;
            s.breezeStrength = breezeStrength;
            s.gustsEnabled = gustsEnabled;
            s.firstGustDelay = firstGustDelay;
            s.gustInterval = gustInterval;
            s.gustStrength = gustStrength;
        }
    }

    /// <summary>
    /// Everything that makes one yard: size, leaves, layout, neighbour, colours, weather and goals.
    /// The five Hollow Creek yards are generated from <see cref="YardLayouts"/> by
    /// Fallen Yard > Build All Yards; tune the assets afterwards in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Fallen Yard/Yard Definition", fileName = "Yard")]
    public class YardDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id used by saves and the unlock table. Never change it after release.")]
        public string yardId = "starter_lawn";
        public string displayName = "Starter Lawn";
        [Tooltip("Scene that holds this yard (must be in Build Settings).")]
        public string sceneName = "Yard_StarterLawn";
        [Tooltip("Who lives here.")]
        public string neighbourName = "";
        [Tooltip("The note on the mailbox, shown when you arrive.")]
        [TextArea(2, 5)] public string neighbourNote = "";

        [Header("Size and leaves")]
        [Tooltip("Raked area in metres (X by Z). The fence runs around it.")]
        public Vector2 size = new Vector2(24f, 20f);
        [Min(0)] public int leafCount = 6000;
        public int seed = 1;
        public List<LeafMixEntry> leafMix = new List<LeafMixEntry>();
        [Tooltip("Share of leaves scattered evenly over the whole yard, compared with the zone weights.")]
        [Min(0f)] public float backgroundWeight = 1f;
        public List<LeafSpawnZone> spawnZones = new List<LeafSpawnZone>();

        [Header("Layout")]
        public List<YardObstacle> obstacles = new List<YardObstacle>();
        [Tooltip("Player start (yard-local X, Z) and facing in degrees.")]
        public Vector2 playerSpawn = new Vector2(0f, -8f);
        public float playerYaw;
        public Vector2 compostBinPosition = new Vector2(-9f, -7f);
        public Vector2 mailboxPosition = new Vector2(2f, -9.5f);
        [Tooltip("Where the gate gap in the front fence is (yard-local X). The fence is open for 2 m around it.")]
        public float gateX;

        [Header("Look and weather")]
        public YardPalette palette = new YardPalette();
        public YardWeather weather = new YardWeather();

        [Header("Goal")]
        [Range(0.5f, 1f)] public float completeAtFraction = 0.95f;
        [Min(0)] public int completionBonus = 250;

        /// <summary>Problems a designer should fix. Empty means the yard is good to build.</summary>
        public List<string> Validate() => YardRules.Validate(this);
    }
}

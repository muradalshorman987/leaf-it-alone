using System.Collections.Generic;
using FallenYard.Leaves;
using FallenYard.Progression;
using UnityEngine;

namespace FallenYard.Levels
{
    /// <summary>
    /// The five Hollow Creek yards, in play order, as code. Fallen Yard > Build All Yards turns
    /// them into <see cref="YardDefinition"/> assets and scenes; tests check every layout.
    /// Coordinates are yard-local metres: (0, 0) is the middle, -Z is the street and gate,
    /// +Z is the back where the house stands.
    /// </summary>
    public static class YardLayouts
    {
        public const string StarterLawn = "starter_lawn";
        public const string CornerLot = "corner_lot";
        public const string Orchard = "orchard";
        public const string ChurchGreen = "church_green";
        public const string HilltopManor = "hilltop_manor";

        /// <summary>Yard ids in play order.</summary>
        public static readonly string[] Order = { StarterLawn, CornerLot, Orchard, ChurchGreen, HilltopManor };

        /// <summary>Fresh definitions for every yard, in play order. The caller owns the instances.</summary>
        public static List<YardDefinition> CreateAll() => new List<YardDefinition>
        {
            CreateStarterLawn(),
            CreateCornerLot(),
            CreateOrchard(),
            CreateChurchGreen(),
            CreateHilltopManor(),
        };

        public static YardDefinition Create(string yardId)
        {
            switch (yardId)
            {
                case StarterLawn: return CreateStarterLawn();
                case CornerLot: return CreateCornerLot();
                case Orchard: return CreateOrchard();
                case ChurchGreen: return CreateChurchGreen();
                case HilltopManor: return CreateHilltopManor();
                default: return null;
            }
        }

        /// <summary>Unlock rules for the five yards: each needs the one before, later ones cost coins and give tools.</summary>
        public static List<YardUnlockRule> CreateUnlockRules() => new List<YardUnlockRule>
        {
            new YardUnlockRule { yardId = StarterLawn, displayName = "Starter Lawn", unlockedByDefault = true, rewardToolIds = new[] { "blower" } },
            new YardUnlockRule { yardId = CornerLot, displayName = "Corner Lot", requiredYards = new[] { StarterLawn } },
            new YardUnlockRule { yardId = Orchard, displayName = "Orchard", requiredYards = new[] { CornerLot }, unlockCost = 300, rewardToolIds = new[] { "vacuum" } },
            new YardUnlockRule { yardId = ChurchGreen, displayName = "Church Green", requiredYards = new[] { Orchard }, unlockCost = 600, rewardToolIds = new[] { "mulcher" } },
            new YardUnlockRule { yardId = HilltopManor, displayName = "Hilltop Manor", requiredYards = new[] { ChurchGreen }, unlockCost = 1000 },
        };

        // ------------------------------------------------------------------ yards

        static YardDefinition CreateStarterLawn()
        {
            var y = New(StarterLawn, "Starter Lawn", "Yard_StarterLawn", new Vector2(24f, 20f), 6000, 101);
            y.neighbourName = "Gran Odile";
            y.neighbourNote = "Dear helper, my knees and this lawn are no longer on speaking terms. " +
                              "The big oak drops everything at once. Bag what you can; there's cocoa on the porch. - Odile";
            Mix(y, (LeafPreset.Oak, 0.6f), (LeafPreset.Maple, 0.4f));

            Add(y, ObstacleKind.House, 0f, 7.5f, 10f, 4f, 4f);
            Add(y, ObstacleKind.Path, 0f, -2.25f, 1.4f, 0.02f, 11.5f);
            Add(y, ObstacleKind.Tree, -6f, 1f, 6f, 6f, 6f);
            Add(y, ObstacleKind.Tree, 6.5f, -2f, 4.5f, 5f, 4.5f);
            Add(y, ObstacleKind.Bush, -10f, 5f, 1.6f, 1.2f, 1.6f);
            Add(y, ObstacleKind.Bush, 10f, 5f, 1.6f, 1.2f, 1.6f);
            Add(y, ObstacleKind.Flowerbed, 4f, 4.5f, 3f, 0.1f, 1f);

            y.backgroundWeight = 1.5f;
            Circle(y, "Under the oak", -6f, 1f, 3.2f, 3f);
            Circle(y, "Under the little maple", 6.5f, -2f, 2.4f, 1.5f);

            y.playerSpawn = new Vector2(0f, -8.5f);
            y.compostBinPosition = new Vector2(-10f, -8f);
            y.mailboxPosition = new Vector2(1.6f, -9.4f);

            Palette(y, new Color(0.45f, 0.60f, 0.29f), new Color(0.36f, 0.70f, 0.31f),
                new Color(0.95f, 0.62f, 0.16f), new Color(0.90f, 0.45f, 0.12f), new Color(0.80f, 0.30f, 0.10f), new Color(0.62f, 0.40f, 0.16f));
            y.palette.foliage = new Color(0.93f, 0.58f, 0.18f);
            y.palette.houseWalls = new Color(0.92f, 0.84f, 0.70f);
            y.palette.roof = new Color(0.45f, 0.32f, 0.40f);
            Weather(y, 30f, 0.6f, 45f, new Vector2(50f, 80f), new Vector2(2.5f, 4f));
            y.completionBonus = 150;
            return y;
        }

        static YardDefinition CreateCornerLot()
        {
            var y = New(CornerLot, "Corner Lot", "Yard_CornerLot", new Vector2(32f, 26f), 10000, 202);
            y.neighbourName = "The Okafor twins";
            y.neighbourNote = "Mum says if the leaves reach the porch again we have to rake them ourselves. Please make it so we don't. " +
                              "The wind always piles them in the far corner, sorry. - Ada & Ben";
            Mix(y, (LeafPreset.Maple, 0.5f), (LeafPreset.Oak, 0.4f), (LeafPreset.WetClump, 0.1f));

            Add(y, ObstacleKind.House, 9f, 7f, 12f, 5f, 9f);
            Add(y, ObstacleKind.Path, 8.5f, -5.25f, 1.4f, 0.02f, 15.5f);
            Add(y, ObstacleKind.Hedge, -8f, 11.8f, 14f, 1.6f, 1.2f);
            Add(y, ObstacleKind.Shed, -12.5f, -10f, 3f, 2.6f, 3f);
            Add(y, ObstacleKind.Tree, -9f, 4f, 6f, 6.5f, 6f);
            Add(y, ObstacleKind.Tree, -2f, -3f, 5f, 6f, 5f);
            Add(y, ObstacleKind.Tree, 12f, -6f, 5f, 5.5f, 5f);
            Add(y, ObstacleKind.Bench, 2f, 0.5f, 2f, 0.9f, 0.6f, 20f);

            y.backgroundWeight = 1f;
            Rect(y, "The windy corner", -13.5f, 10f, 5f, 5f, 2.5f);
            Rect(y, "Along the hedge", -8f, 10.4f, 14f, 1.4f, 1f);
            Circle(y, "Under the big maple", -9f, 4f, 3.2f, 2f);
            Circle(y, "Under the middle maple", -2f, -3f, 2.7f, 1.6f);
            Circle(y, "Under the street tree", 12f, -6f, 2.7f, 1.4f);

            y.gateX = 8.5f;
            y.playerSpawn = new Vector2(8.5f, -11.5f);
            y.compostBinPosition = new Vector2(-13.5f, -5f);
            y.mailboxPosition = new Vector2(10.4f, -12.4f);

            Palette(y, new Color(0.43f, 0.56f, 0.27f), new Color(0.35f, 0.68f, 0.30f),
                new Color(0.84f, 0.22f, 0.10f), new Color(0.92f, 0.38f, 0.10f), new Color(0.70f, 0.14f, 0.12f), new Color(0.96f, 0.66f, 0.20f));
            y.palette.foliage = new Color(0.82f, 0.26f, 0.12f);
            y.palette.houseWalls = new Color(0.80f, 0.86f, 0.88f);
            y.palette.roof = new Color(0.30f, 0.34f, 0.42f);
            Weather(y, 315f, 1f, 30f, new Vector2(35f, 60f), new Vector2(3.5f, 6f));
            y.completionBonus = 250;
            return y;
        }

        static YardDefinition CreateOrchard()
        {
            var y = New(Orchard, "Orchard", "Yard_Orchard", new Vector2(40f, 34f), 15000, 303);
            y.neighbourName = "Mr. Halloran";
            y.neighbourNote = "Forty years of apples and I still forget the leaves come with them. " +
                              "The low rows stay soggy, so take the rake to those. Help yourself to the windfalls. - T. Halloran";
            Mix(y, (LeafPreset.Oak, 0.45f), (LeafPreset.WetClump, 0.3f), (LeafPreset.Maple, 0.25f));

            Add(y, ObstacleKind.House, -12f, 14f, 12f, 5f, 5f);
            Add(y, ObstacleKind.Shed, 15f, 14f, 4f, 3f, 4f);
            Add(y, ObstacleKind.Bench, 0f, -12f, 2f, 0.9f, 0.6f);
            Add(y, ObstacleKind.Path, 0f, -14.5f, 1.4f, 0.02f, 5f);
            y.backgroundWeight = 1f;
            foreach (float x in new[] { -12f, -4f, 4f, 12f })
                foreach (float z in new[] { -8f, 0f, 8f })
                {
                    Add(y, ObstacleKind.FruitTree, x, z, 5f, 4.5f, 5f);
                    Circle(y, "Under an apple tree", x, z, 2.6f, 0.8f);
                }
            Rect(y, "The low, soggy rows", 0f, -8f, 34f, 4f, 2f);

            y.playerSpawn = new Vector2(0f, -15.5f);
            y.compostBinPosition = new Vector2(-17f, -14f);
            y.mailboxPosition = new Vector2(1.8f, -16.4f);

            Palette(y, new Color(0.47f, 0.55f, 0.28f), new Color(0.38f, 0.66f, 0.30f),
                new Color(0.93f, 0.74f, 0.22f), new Color(0.78f, 0.54f, 0.16f), new Color(0.58f, 0.38f, 0.16f), new Color(0.86f, 0.40f, 0.12f));
            y.palette.foliage = new Color(0.90f, 0.70f, 0.22f);
            y.palette.houseWalls = new Color(0.78f, 0.52f, 0.36f);
            y.palette.roof = new Color(0.36f, 0.26f, 0.18f);
            y.palette.sunColor = new Color(1f, 0.86f, 0.66f);
            Weather(y, 60f, 0.8f, 35f, new Vector2(40f, 70f), new Vector2(3f, 5f));
            y.completionBonus = 400;
            return y;
        }

        static YardDefinition CreateChurchGreen()
        {
            var y = New(ChurchGreen, "Church Green", "Yard_ChurchGreen", new Vector2(46f, 40f), 20000, 404);
            y.neighbourName = "Verger Pell";
            y.neighbourNote = "The bells ring at five. If the green is clear by then, the choir will sing your name (they will, I've asked). " +
                              "Mind the yews: they drop needles all year. - Pell";
            Mix(y, (LeafPreset.PineNeedles, 0.35f), (LeafPreset.Oak, 0.35f), (LeafPreset.Maple, 0.2f), (LeafPreset.WetClump, 0.1f));

            Add(y, ObstacleKind.House, 0f, 15f, 14f, 8f, 8f);
            Add(y, ObstacleKind.Path, 0f, -11.75f, 2f, 0.02f, 16.5f);
            Add(y, ObstacleKind.Path, 0f, 6f, 2f, 0.02f, 10f);
            Add(y, ObstacleKind.Fountain, 0f, -2f, 3f, 1f, 3f);
            Add(y, ObstacleKind.Bench, 5f, -6f, 2f, 0.9f, 0.6f, 90f);
            Add(y, ObstacleKind.Bench, -5f, -6f, 2f, 0.9f, 0.6f, 90f);
            Add(y, ObstacleKind.Hedge, -13f, -18.6f, 18f, 1.4f, 1.2f);
            Add(y, ObstacleKind.Hedge, 13f, -18.6f, 18f, 1.4f, 1.2f);
            foreach (float x in new[] { -16f, -12f, -8f })
                foreach (float z in new[] { 6f, 2f })
                    Add(y, ObstacleKind.Gravestone, x, z, 0.8f, 1f, 0.3f);
            Add(y, ObstacleKind.PineTree, -18f, 14f, 4f, 7f, 4f);
            Add(y, ObstacleKind.PineTree, 18f, 14f, 4f, 7f, 4f);
            Add(y, ObstacleKind.PineTree, -18f, -6f, 4f, 7f, 4f);
            Add(y, ObstacleKind.PineTree, 18f, -6f, 4f, 7f, 4f);
            Add(y, ObstacleKind.Tree, 12f, 2f, 7f, 7f, 7f);

            y.backgroundWeight = 1.2f;
            Circle(y, "Under a yew", -18f, 14f, 2.6f, 1.4f);
            Circle(y, "Under a yew", 18f, 14f, 2.6f, 1.4f);
            Circle(y, "Under a yew", -18f, -6f, 2.6f, 1.4f);
            Circle(y, "Under a yew", 18f, -6f, 2.6f, 1.4f);
            Circle(y, "Under the old chestnut", 12f, 2f, 3.8f, 3f);
            Rect(y, "Along the hedges", 0f, -16.8f, 42f, 2f, 1f);

            y.playerSpawn = new Vector2(0f, -18f);
            y.compostBinPosition = new Vector2(20f, -15.5f);
            y.mailboxPosition = new Vector2(2.6f, -19.4f);

            Palette(y, new Color(0.42f, 0.50f, 0.30f), new Color(0.34f, 0.62f, 0.32f),
                new Color(0.66f, 0.40f, 0.18f), new Color(0.54f, 0.30f, 0.14f), new Color(0.80f, 0.56f, 0.20f), new Color(0.46f, 0.36f, 0.20f));
            y.palette.foliage = new Color(0.70f, 0.42f, 0.16f);
            y.palette.houseWalls = new Color(0.70f, 0.68f, 0.62f);
            y.palette.roof = new Color(0.32f, 0.33f, 0.36f);
            y.palette.sunColor = new Color(0.95f, 0.88f, 0.80f);
            y.palette.sunIntensity = 1.05f;
            y.palette.skyAmbient = new Color(0.70f, 0.74f, 0.80f);
            Weather(y, 20f, 0.9f, 30f, new Vector2(35f, 55f), new Vector2(3.5f, 5.5f));
            y.completionBonus = 600;
            return y;
        }

        static YardDefinition CreateHilltopManor()
        {
            var y = New(HilltopManor, "Hilltop Manor", "Yard_HilltopManor", new Vector2(56f, 48f), 28000, 505);
            y.neighbourName = "Mrs. Ashgrove";
            y.neighbourNote = "The manor has seen ninety autumns and I have raked none of them. " +
                              "Do start before the wind does; up here, it always does. Tea is at four. - W. Ashgrove";
            Mix(y, (LeafPreset.Oak, 0.35f), (LeafPreset.Maple, 0.3f), (LeafPreset.WetClump, 0.2f), (LeafPreset.PineNeedles, 0.15f));

            Add(y, ObstacleKind.House, 0f, 17f, 24f, 9f, 10f);
            Add(y, ObstacleKind.Shed, 22f, 18f, 5f, 3f, 5f);
            Add(y, ObstacleKind.Path, 0f, -13.5f, 2.5f, 0.02f, 21f);
            Add(y, ObstacleKind.Path, 0f, 7f, 2.5f, 0.02f, 10f);
            Add(y, ObstacleKind.Fountain, 0f, -1f, 4f, 1.2f, 4f);
            Add(y, ObstacleKind.Hedge, -14f, 4f, 12f, 1.4f, 1.2f);
            Add(y, ObstacleKind.Hedge, 14f, 4f, 12f, 1.4f, 1.2f);
            Add(y, ObstacleKind.Hedge, -14f, -6f, 12f, 1.4f, 1.2f);
            Add(y, ObstacleKind.Hedge, 14f, -6f, 12f, 1.4f, 1.2f);
            Add(y, ObstacleKind.Flowerbed, -6f, 9f, 6f, 0.1f, 1.2f);
            Add(y, ObstacleKind.Flowerbed, 6f, 9f, 6f, 0.1f, 1.2f);
            Add(y, ObstacleKind.Tree, -20f, 10f, 7f, 8f, 7f);
            Add(y, ObstacleKind.Tree, 20f, 8f, 7f, 8f, 7f);
            Add(y, ObstacleKind.Tree, -22f, -14f, 6f, 7f, 6f);
            Add(y, ObstacleKind.Tree, 22f, -15f, 6f, 7f, 6f);
            Add(y, ObstacleKind.PineTree, -24f, 20f, 4f, 8f, 4f);
            Add(y, ObstacleKind.PineTree, 24f, -2f, 4f, 8f, 4f);
            Add(y, ObstacleKind.FruitTree, -6f, -14f, 5f, 4.5f, 5f);
            Add(y, ObstacleKind.FruitTree, 6f, -14f, 5f, 4.5f, 5f);
            Add(y, ObstacleKind.Rock, -10f, -19f, 2f, 1f, 1.6f, 25f);
            Add(y, ObstacleKind.Bench, 0f, -8f, 2.2f, 0.9f, 0.6f);

            y.backgroundWeight = 1.5f;
            Circle(y, "Under the west oak", -20f, 10f, 3.5f, 2f);
            Circle(y, "Under the east oak", 20f, 8f, 3.5f, 2f);
            Circle(y, "Under the south-west oak", -22f, -14f, 3f, 1.6f);
            Circle(y, "Under the south-east oak", 22f, -15f, 3f, 1.6f);
            Circle(y, "Under the pines", -24f, 20f, 2.4f, 1f);
            Circle(y, "Under the pines", 24f, -2f, 2.4f, 1f);
            Rect(y, "Against the hedges", -14f, 2.8f, 12f, 1.6f, 0.8f);
            Rect(y, "Against the hedges", 14f, 2.8f, 12f, 1.6f, 0.8f);
            Rect(y, "Against the hedges", -14f, -7.2f, 12f, 1.6f, 0.8f);
            Rect(y, "Against the hedges", 14f, -7.2f, 12f, 1.6f, 0.8f);
            Rect(y, "Where the wind drops them", -24f, -21f, 7f, 5f, 2f);

            y.playerSpawn = new Vector2(0f, -22f);
            y.compostBinPosition = new Vector2(25f, -21f);
            y.mailboxPosition = new Vector2(3.2f, -23.4f);

            Palette(y, new Color(0.40f, 0.48f, 0.27f), new Color(0.33f, 0.60f, 0.30f),
                new Color(0.62f, 0.14f, 0.16f), new Color(0.78f, 0.30f, 0.10f), new Color(0.48f, 0.20f, 0.26f), new Color(0.88f, 0.58f, 0.16f));
            y.palette.foliage = new Color(0.66f, 0.18f, 0.16f);
            y.palette.houseWalls = new Color(0.84f, 0.80f, 0.74f);
            y.palette.roof = new Color(0.24f, 0.26f, 0.30f);
            y.palette.sunColor = new Color(1f, 0.78f, 0.58f);
            y.palette.sunIntensity = 1.1f;
            y.palette.sunAngles = new Vector2(22f, -60f);
            y.palette.skyAmbient = new Color(0.72f, 0.70f, 0.78f);
            Weather(y, 200f, 1.2f, 20f, new Vector2(25f, 45f), new Vector2(4.5f, 7f));
            y.completeAtFraction = 0.93f;
            y.completionBonus = 1000;
            return y;
        }

        // ------------------------------------------------------------------ helpers

        static YardDefinition New(string id, string name, string scene, Vector2 size, int leaves, int seed)
        {
            var y = ScriptableObject.CreateInstance<YardDefinition>();
            y.name = name;
            y.yardId = id;
            y.displayName = name;
            y.sceneName = scene;
            y.size = size;
            y.leafCount = leaves;
            y.seed = seed;
            y.leafMix.Clear();
            y.spawnZones.Clear();
            y.obstacles.Clear();
            y.playerYaw = 0f;
            return y;
        }

        static void Mix(YardDefinition y, params (LeafPreset preset, float weight)[] entries)
        {
            foreach (var e in entries) y.leafMix.Add(new LeafMixEntry { preset = e.preset, weight = e.weight });
        }

        static void Add(YardDefinition y, ObstacleKind kind, float x, float z, float w, float h, float d, float rotation = 0f) =>
            y.obstacles.Add(new YardObstacle { kind = kind, position = new Vector2(x, z), size = new Vector3(w, h, d), rotation = rotation });

        static void Circle(YardDefinition y, string label, float x, float z, float radius, float weight) =>
            y.spawnZones.Add(new LeafSpawnZone { label = label, shape = ZoneShape.Circle, center = new Vector2(x, z), size = new Vector2(radius, radius), weight = weight });

        static void Rect(YardDefinition y, string label, float x, float z, float w, float d, float weight) =>
            y.spawnZones.Add(new LeafSpawnZone { label = label, shape = ZoneShape.Rectangle, center = new Vector2(x, z), size = new Vector2(w, d), weight = weight });

        static void Palette(YardDefinition y, Color grass, Color restored, params Color[] leaves)
        {
            y.palette.grass = grass;
            y.palette.grassRestored = restored;
            y.palette.leafColors = leaves;
        }

        static void Weather(YardDefinition y, float direction, float breeze, float firstGust, Vector2 interval, Vector2 strength)
        {
            y.weather.prevailingDirection = direction;
            y.weather.breezeStrength = breeze;
            y.weather.firstGustDelay = firstGust;
            y.weather.gustInterval = interval;
            y.weather.gustStrength = strength;
        }
    }
}

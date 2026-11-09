using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace FallenYard.Leaves
{
    /// <summary>One leaf's simulation state. Blittable so Burst jobs can own it.</summary>
    public struct LeafState
    {
        /// <summary>Position on the ground plane (world X, world Z).</summary>
        public float2 pos;
        public float2 vel;
        /// <summary>World Y the leaf is drawn at (ground + pile stack + lift).</summary>
        public float height;
        /// <summary>Ground height under the leaf.</summary>
        public float ground;
        /// <summary>Height above its rest point while airborne (blower, wind).</summary>
        public float lift;
        public float liftVel;
        /// <summary>Degrees.</summary>
        public float yaw;
        /// <summary>Degrees.</summary>
        public float tilt;
        public float scale;
        /// <summary>0 = bottom of the pile, 1 = top. Fixed per leaf.</summary>
        public float stackFrac;
        /// <summary>Grid cell, rebuilt every frame.</summary>
        public int cell;
        /// <summary>Index into the field's leaf types.</summary>
        public int type;
        /// <summary>1 once bagged/collected.</summary>
        public byte collected;

        public bool IsCollected => collected != 0;
        public bool IsMoving => math.lengthsq(vel) > 0f || lift > 0f || liftVel > 0f;
    }

    /// <summary>Uniform grid over the yard. Cells are square; corners hold baked ground heights.</summary>
    public struct LeafGrid
    {
        public float2 origin;
        public float2 size;
        public float cellSize;
        public int width;
        public int height;

        public int CellCount => width * height;

        public static LeafGrid Create(Vector3 center, Vector2 yardSize, float cellSize)
        {
            cellSize = Mathf.Max(0.05f, cellSize);
            return new LeafGrid
            {
                origin = new float2(center.x - yardSize.x * 0.5f, center.z - yardSize.y * 0.5f),
                size = new float2(yardSize.x, yardSize.y),
                cellSize = cellSize,
                width = Mathf.Max(1, Mathf.CeilToInt(yardSize.x / cellSize)),
                height = Mathf.Max(1, Mathf.CeilToInt(yardSize.y / cellSize)),
            };
        }

        public int2 CellXY(float2 p)
        {
            int x = math.clamp((int)math.floor((p.x - origin.x) / cellSize), 0, width - 1);
            int y = math.clamp((int)math.floor((p.y - origin.y) / cellSize), 0, height - 1);
            return new int2(x, y);
        }

        public int CellIndex(float2 p)
        {
            int2 c = CellXY(p);
            return c.y * width + c.x;
        }

        public float2 CellCenter(int x, int y) => origin + new float2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);

        /// <summary>Inclusive cell rectangle covering a circle (clamped to the grid).</summary>
        public void CellRect(float2 center, float radius, out int x0, out int y0, out int x1, out int y1)
        {
            int2 a = CellXY(center - radius);
            int2 b = CellXY(center + radius);
            x0 = a.x; y0 = a.y; x1 = b.x; y1 = b.y;
        }

        public float2 Clamp(float2 p) => math.clamp(p, origin, origin + size);
    }

    /// <summary>Field-wide tuning shared by every leaf, copied into the jobs each frame.</summary>
    public struct LeafSimSettings
    {
        public float damping;
        public float maxSpeed;
        public int reposeDifference;
        public float slumpStrength;
        public float liftGravity;
        public float maxLift;
        public float pileHeightPerLeaf;
        public float maxPileHeight;
        public float windForce;
        public float windLiftChance;
    }

    /// <summary>
    /// Pure, Burst-friendly maths shared by the leaf jobs. Kept separate so it can be
    /// unit-tested without a scene.
    /// </summary>
    public static class LeafPhysics
    {
        /// <summary>
        /// How exposed a leaf is to the wind. A lone leaf is fully exposed; inside a pile
        /// only the top layers are, so gusts strip piles from the top instead of moving them whole.
        /// </summary>
        public static float WindExposure(float stackFrac, int leavesInCell, int reposeDifference)
        {
            float pileFactor = math.saturate(leavesInCell / math.max(1f, reposeDifference * 2f));
            return math.lerp(1f, stackFrac * stackFrac, pileFactor);
        }

        /// <summary>
        /// Horizontal acceleration (m/s²) the wind gives a leaf. Zero until the wind beats
        /// the type's threshold, then grows with the excess, divided by mass.
        /// </summary>
        public static float2 WindAcceleration(float2 windDir, float windStrength, in LeafTypeParams type, float exposure, float windForce)
        {
            float excess = windStrength - type.windThreshold;
            if (excess <= 0f || exposure <= 0f) return float2.zero;
            return windDir * (excess * windForce * exposure * type.invMass);
        }

        /// <summary>Exponential velocity decay factor for one step.</summary>
        public static float DampFactor(float damping, float drag, float dt) => math.exp(-damping * drag * dt);

        /// <summary>Stack height of a cell from its own count and its 4 neighbours (smoothed for rounder mounds).</summary>
        public static float SmoothedCellHeight(int own, int left, int right, int down, int up, float heightPerLeaf, float maxHeight)
        {
            float sum = own * 0.5f + (left + right + down + up) * 0.125f;
            return math.min(maxHeight, 0.01f + sum * heightPerLeaf);
        }

        /// <summary>Bilinear sample of ground heights baked at the grid's cell corners ((width+1) x (height+1)).</summary>
        public static float SampleGround(in LeafGrid grid, NativeArray<float> corners, float2 p)
        {
            float fx = math.clamp((p.x - grid.origin.x) / grid.cellSize, 0f, grid.width);
            float fy = math.clamp((p.y - grid.origin.y) / grid.cellSize, 0f, grid.height);
            int x0 = math.min((int)fx, grid.width - 1);
            int y0 = math.min((int)fy, grid.height - 1);
            float tx = fx - x0, ty = fy - y0;
            int stride = grid.width + 1;
            int i = y0 * stride + x0;
            float a = math.lerp(corners[i], corners[i + 1], tx);
            float b = math.lerp(corners[i + stride], corners[i + stride + 1], tx);
            return math.lerp(a, b, ty);
        }

        /// <summary>Direction (unit, grid axis) to the neighbour with the fewest leaves, and how many fewer it has.</summary>
        public static float2 LowestNeighbourDirection(NativeArray<int> cellCount, int width, int height, int cell, out int diff)
        {
            int x = cell % width, y = cell / width;
            int own = cellCount[cell];
            int best = own;
            float2 dir = float2.zero;
            if (x > 0 && cellCount[cell - 1] < best) { best = cellCount[cell - 1]; dir = new float2(-1f, 0f); }
            if (x < width - 1 && cellCount[cell + 1] < best) { best = cellCount[cell + 1]; dir = new float2(1f, 0f); }
            if (y > 0 && cellCount[cell - width] < best) { best = cellCount[cell - width]; dir = new float2(0f, -1f); }
            if (y < height - 1 && cellCount[cell + width] < best) { best = cellCount[cell + width]; dir = new float2(0f, 1f); }
            diff = own - best;
            return dir;
        }

        /// <summary>
        /// Leaf transform. Rotation matches UnityEngine.Quaternion.Euler(tilt, yaw, tilt * 0.5f)
        /// (Unity applies Z, then X, then Y).
        /// </summary>
        public static float4x4 LeafMatrix(float3 position, float yawDeg, float tiltDeg, float scale)
        {
            quaternion rot = quaternion.EulerZXY(math.radians(new float3(tiltDeg, yawDeg, tiltDeg * 0.5f)));
            return float4x4.TRS(position, rot, new float3(scale));
        }

        /// <summary>Tilt wobble for airborne leaves so they flutter instead of sliding through the air.</summary>
        public static float FlutterTilt(float baseTilt, float lift, float time, int index)
        {
            if (lift <= 0f) return baseTilt;
            return baseTilt + 35f * math.sin(time * 7f + index * 1.7f) * math.saturate(lift * 4f);
        }

        /// <summary>Stable per-leaf value in [0, 1), used to vary wind response between leaves.</summary>
        public static float Hash01(uint index) => (math.hash(new uint2(index, 0x9E3779B9u)) & 0xFFFFFF) / 16777216f;

        /// <summary>True if a sphere is inside (or touching) every plane. Planes are (normal.xyz, distance), as Unity's GeometryUtility returns them.</summary>
        public static bool SphereInFrustum(NativeArray<float4> planes, int planeCount, float3 center, float radius)
        {
            for (int p = 0; p < planeCount; p++)
            {
                float4 pl = planes[p];
                if (math.dot(pl.xyz, center) + pl.w < -radius) return false;
            }
            return true;
        }

        /// <summary>Interleaves the low 16 bits of x and y (Z-order). Used to keep render slices spatially compact.</summary>
        public static uint Morton2(uint x, uint y) => Part1By1(x) | (Part1By1(y) << 1);

        static uint Part1By1(uint v)
        {
            v &= 0x0000FFFF;
            v = (v | (v << 8)) & 0x00FF00FF;
            v = (v | (v << 4)) & 0x0F0F0F0F;
            v = (v | (v << 2)) & 0x33333333;
            v = (v | (v << 1)) & 0x55555555;
            return v;
        }
    }
}

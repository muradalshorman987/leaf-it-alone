using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace FallenYard.Leaves
{
    /// <summary>
    /// Moves every leaf one step: pile slumping, wind, velocity, lift and settling.
    /// Reads last frame's grid; <see cref="LeafGridJob"/> runs right after it.
    /// </summary>
    [BurstCompile]
    public struct LeafSimulateJob : IJobParallelFor
    {
        public NativeArray<LeafState> leaves;
        [ReadOnly] public NativeArray<LeafTypeParams> types;
        [ReadOnly] public NativeArray<int> cellCount;
        [ReadOnly] public NativeArray<float> cellHeight;
        [ReadOnly] public NativeArray<float> groundCorners;
        public LeafGrid grid;
        public LeafSimSettings settings;
        public float2 windDir;
        public float windStrength;
        public float dt;
        public uint frameSeed;

        public void Execute(int i)
        {
            LeafState s = leaves[i];
            if (s.collected != 0) return;
            LeafTypeParams type = types[s.type];
            int cell = s.cell;
            int inCell = cellCount[cell];
            bool airborne = s.lift > 0f || s.liftVel > 0f;

            // Angle of repose: top-of-pile leaves slide toward the lowest neighbour.
            if (s.stackFrac > 0.4f && inCell > settings.reposeDifference)
            {
                float2 slide = LeafPhysics.LowestNeighbourDirection(cellCount, grid.width, grid.height, cell, out int diff);
                if (diff > settings.reposeDifference)
                    s.vel += slide * (settings.slumpStrength * math.min(2f, (float)diff / settings.reposeDifference) * dt);
            }

            // Wind: only exposed leaves move, and only once the wind beats the type's threshold.
            if (windStrength > type.windThreshold)
            {
                float exposure = LeafPhysics.WindExposure(s.stackFrac, inCell, settings.reposeDifference);
                float variation = 0.6f + 0.8f * LeafPhysics.Hash01((uint)i);
                float2 accel = LeafPhysics.WindAcceleration(windDir, windStrength, type, exposure, settings.windForce) * variation;
                if (!airborne) accel *= 0.6f; // ground friction: leaves tumble, they don't skate
                s.vel += accel * dt;

                // Strong gusts pick some leaves up so they tumble through the air.
                if (!airborne && exposure > 0.5f)
                {
                    float excess = windStrength - type.windThreshold;
                    var rng = Unity.Mathematics.Random.CreateFromIndex(math.hash(new uint2((uint)i, frameSeed)));
                    float chance = settings.windLiftChance * math.saturate(excess / 3f) * dt;
                    if (rng.NextFloat() < chance)
                    {
                        s.liftVel = (0.8f + rng.NextFloat()) * math.min(2.5f, excess * 0.5f) * type.invMass;
                        airborne = true;
                    }
                }
            }

            float2 v = s.vel;
            if (math.lengthsq(v) > 1e-6f)
            {
                float maxSpeed = settings.maxSpeed;
                if (math.lengthsq(v) > maxSpeed * maxSpeed) v = math.normalize(v) * maxSpeed;
                float2 p = s.pos + v * dt;
                float2 min = grid.origin, max = grid.origin + grid.size;
                if (p.x < min.x) { p.x = min.x; v.x = 0f; }
                else if (p.x > max.x) { p.x = max.x; v.x = 0f; }
                if (p.y < min.y) { p.y = min.y; v.y = 0f; }
                else if (p.y > max.y) { p.y = max.y; v.y = 0f; }
                s.pos = p;
                s.yaw += math.length(v) * 90f * dt * ((i & 1) == 0 ? 1f : -1f);
                v *= LeafPhysics.DampFactor(settings.damping, airborne ? type.airDrag : type.groundDrag, dt);
                if (math.lengthsq(v) < 1e-4f) v = float2.zero;
                s.vel = v;
                s.ground = LeafPhysics.SampleGround(grid, groundCorners, p);
            }

            if (airborne)
            {
                s.liftVel -= settings.liftGravity * type.gravityScale * dt;
                s.lift = math.min(settings.maxLift, s.lift + s.liftVel * dt);
                if (s.lift <= 0f) { s.lift = 0f; s.liftVel = 0f; }
            }

            float target = s.ground + cellHeight[cell] * s.stackFrac + s.lift;
            if (math.abs(target - s.height) > 0.0005f)
            {
                float settle = 1.5f * dt;
                s.height = s.lift > 0f ? target : s.height + math.clamp(target - s.height, -settle, settle);
            }

            leaves[i] = s;
        }
    }

    /// <summary>
    /// Rebuilds the uniform grid (counting sort, O(n)) and the smoothed pile heights.
    /// Single-threaded on purpose: the prefix sum is sequential and the whole pass is
    /// well under a millisecond for 30k leaves with Burst.
    /// </summary>
    [BurstCompile]
    public struct LeafGridJob : IJob
    {
        public NativeArray<LeafState> leaves;
        public NativeArray<int> cellCount;
        /// <summary>Length cells + 1.</summary>
        public NativeArray<int> cellStart;
        public NativeArray<int> cellCursor;
        /// <summary>Leaf indices sorted by cell. Length = leaf count.</summary>
        public NativeArray<int> cellLeaves;
        public NativeArray<float> cellHeight;
        /// <summary>[0] = live leaves, [1] = leaves moving or airborne.</summary>
        public NativeArray<int> stats;
        public LeafGrid grid;
        public LeafSimSettings settings;

        public void Execute()
        {
            int cells = cellCount.Length;
            for (int c = 0; c < cells; c++) cellCount[c] = 0;

            int live = 0, moving = 0;
            for (int i = 0; i < leaves.Length; i++)
            {
                LeafState s = leaves[i];
                if (s.collected != 0)
                {
                    if (s.cell != 0) { s.cell = 0; leaves[i] = s; }
                    continue;
                }
                int c = grid.CellIndex(s.pos);
                if (c != s.cell) { s.cell = c; leaves[i] = s; }
                cellCount[c]++;
                live++;
                if (s.IsMoving) moving++;
            }
            stats[0] = live;
            stats[1] = moving;

            cellStart[0] = 0;
            for (int c = 0; c < cells; c++)
            {
                cellStart[c + 1] = cellStart[c] + cellCount[c];
                cellCursor[c] = cellStart[c];
            }
            for (int i = 0; i < leaves.Length; i++)
            {
                LeafState s = leaves[i];
                if (s.collected != 0) continue;
                int slot = cellCursor[s.cell];
                cellLeaves[slot] = i;
                cellCursor[s.cell] = slot + 1;
            }

            int w = grid.width, h = grid.height;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int c = y * w + x;
                int own = cellCount[c];
                cellHeight[c] = LeafPhysics.SmoothedCellHeight(
                    own,
                    x > 0 ? cellCount[c - 1] : own,
                    x < w - 1 ? cellCount[c + 1] : own,
                    y > 0 ? cellCount[c - w] : own,
                    y < h - 1 ? cellCount[c + w] : own,
                    settings.pileHeightPerLeaf, settings.maxPileHeight);
            }
        }
    }

    /// <summary>
    /// Per render slice (up to 1023 leaves of one type/material): frustum and distance cull
    /// every live leaf, split survivors into near (full mesh) and far (LOD mesh) lists and
    /// write their matrices compacted at the start of the slice's range.
    /// </summary>
    [BurstCompile]
    public struct LeafCullJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<LeafState> leaves;
        /// <summary>x = first leaf, y = leaf count, z = render bucket.</summary>
        [ReadOnly] public NativeArray<int3> slices;
        [ReadOnly] public NativeArray<float4> planes;
        /// <summary>0 disables frustum culling.</summary>
        public int planeCount;
        public float3 cameraPos;
        public float lodDistanceSq;
        public float cullDistanceSq;
        public float time;

        [NativeDisableParallelForRestriction] public NativeArray<Matrix4x4> nearMatrices;
        [NativeDisableParallelForRestriction] public NativeArray<Matrix4x4> farMatrices;
        /// <summary>Per slice: x = near count, y = far count.</summary>
        public NativeArray<int2> sliceCounts;

        public void Execute(int sliceIndex)
        {
            int3 slice = slices[sliceIndex];
            int start = slice.x, end = slice.x + slice.y;
            int near = 0, far = 0;
            for (int i = start; i < end; i++)
            {
                LeafState s = leaves[i];
                if (s.collected != 0) continue;
                var p = new float3(s.pos.x, s.height, s.pos.y);
                float distSq = math.distancesq(p, cameraPos);
                if (distSq > cullDistanceSq) continue;
                if (planeCount > 0 && !LeafPhysics.SphereInFrustum(planes, planeCount, p, s.scale * 0.6f)) continue;

                float tilt = LeafPhysics.FlutterTilt(s.tilt, s.lift, time, i);
                Matrix4x4 m = LeafPhysics.LeafMatrix(p, s.yaw, tilt, s.scale);
                if (distSq <= lodDistanceSq) nearMatrices[start + near++] = m;
                else farMatrices[start + far++] = m;
            }
            sliceCounts[sliceIndex] = new int2(near, far);
        }
    }

    /// <summary>Rake: leaves in a circle blend their velocity toward a target, strongest at the centre.</summary>
    [BurstCompile]
    public struct LeafPushJob : IJob
    {
        public NativeArray<LeafState> leaves;
        [ReadOnly] public NativeArray<LeafTypeParams> types;
        [ReadOnly] public NativeArray<int> cellStart;
        [ReadOnly] public NativeArray<int> cellLeaves;
        public LeafGrid grid;
        public float2 center;
        public float radius;
        public float2 targetVelocity;
        public float strength;
        public float dt;
        /// <summary>[0] receives the number of leaves affected.</summary>
        public NativeArray<int> result;

        public void Execute()
        {
            int n = 0;
            float r2 = radius * radius;
            grid.CellRect(center, radius, out int x0, out int y0, out int x1, out int y1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int cell = y * grid.width + x;
                for (int k = cellStart[cell]; k < cellStart[cell + 1]; k++)
                {
                    int i = cellLeaves[k];
                    LeafState s = leaves[i];
                    if (s.collected != 0) continue;
                    float d2 = math.distancesq(s.pos, center);
                    if (d2 > r2) continue;
                    float falloff = 1f - math.saturate(math.sqrt(d2) / radius);
                    float t = math.saturate(strength * (0.35f + 0.65f * falloff) * dt * types[s.type].rakeGrip);
                    s.vel = math.lerp(s.vel, targetVelocity, t);
                    leaves[i] = s;
                    n++;
                }
            }
            result[0] = n;
        }
    }

    /// <summary>Blower: leaves in a cone are pushed away from the nozzle and lifted.</summary>
    [BurstCompile]
    public struct LeafBlowConeJob : IJob
    {
        public NativeArray<LeafState> leaves;
        [ReadOnly] public NativeArray<LeafTypeParams> types;
        [ReadOnly] public NativeArray<int> cellStart;
        [ReadOnly] public NativeArray<int> cellLeaves;
        public LeafGrid grid;
        public float3 origin;
        /// <summary>Normalized.</summary>
        public float3 dir;
        public float range;
        public float halfAngleDeg;
        public float force;
        public float lift;
        public float dt;
        public NativeArray<int> result;

        public void Execute()
        {
            int n = 0;
            float3 mid = origin + dir * (range * 0.5f);
            float queryRadius = range * 0.75f;
            float r2 = queryRadius * queryRadius;
            float cosHalf = math.cos(math.radians(halfAngleDeg));
            grid.CellRect(mid.xz, queryRadius, out int x0, out int y0, out int x1, out int y1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int cell = y * grid.width + x;
                for (int k = cellStart[cell]; k < cellStart[cell + 1]; k++)
                {
                    int i = cellLeaves[k];
                    LeafState s = leaves[i];
                    if (s.collected != 0 || math.distancesq(s.pos, mid.xz) > r2) continue;
                    float3 to = new float3(s.pos.x, s.height, s.pos.y) - origin;
                    float dist = math.length(to);
                    if (dist > range || dist < 0.01f) continue;
                    float cosAngle = math.dot(dir, to / dist);
                    if (cosAngle < cosHalf) continue;
                    float angle = math.degrees(math.acos(math.clamp(cosAngle, -1f, 1f)));

                    float falloff = (1f - dist / range) * (1f - angle / math.max(0.01f, halfAngleDeg));
                    LeafTypeParams type = types[s.type];
                    float response = type.blowerGrip * type.invMass;
                    float2 push = to.xz;
                    if (math.lengthsq(push) < 1e-4f) push = dir.xz;
                    push = math.normalizesafe(push);
                    s.vel += push * (force * falloff * response * dt);
                    if (s.lift < 0.6f) s.liftVel = math.max(s.liftVel, lift * falloff * math.min(1.5f, response));
                    leaves[i] = s;
                    n++;
                }
            }
            result[0] = n;
        }
    }

    /// <summary>Vacuum / suction: leaves in a circle are drawn toward a point.</summary>
    [BurstCompile]
    public struct LeafAttractJob : IJob
    {
        public NativeArray<LeafState> leaves;
        [ReadOnly] public NativeArray<LeafTypeParams> types;
        [ReadOnly] public NativeArray<int> cellStart;
        [ReadOnly] public NativeArray<int> cellLeaves;
        public LeafGrid grid;
        public float2 center;
        public float radius;
        public float speed;
        public float strength;
        public float dt;
        public NativeArray<int> result;

        public void Execute()
        {
            int n = 0;
            float r2 = radius * radius;
            grid.CellRect(center, radius, out int x0, out int y0, out int x1, out int y1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int cell = y * grid.width + x;
                for (int k = cellStart[cell]; k < cellStart[cell + 1]; k++)
                {
                    int i = cellLeaves[k];
                    LeafState s = leaves[i];
                    if (s.collected != 0) continue;
                    float2 to = center - s.pos;
                    float d2 = math.lengthsq(to);
                    if (d2 > r2) continue;
                    LeafTypeParams type = types[s.type];
                    float falloff = 1f - math.saturate(math.sqrt(d2) / radius);
                    float t = math.saturate(strength * (0.3f + 0.7f * falloff) * dt * type.blowerGrip * type.invMass);
                    s.vel = math.lerp(s.vel, math.normalizesafe(to) * speed, t);
                    leaves[i] = s;
                    n++;
                }
            }
            result[0] = n;
        }
    }
}

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace FallenYard.Leaves
{
    /// <summary>Per-frame rendering numbers, for the debug HUD and profiling.</summary>
    public struct LeafRenderStats
    {
        public int visibleNear;
        public int visibleFar;
        public int drawCalls;
        public int Visible => visibleNear + visibleFar;
    }

    /// <summary>
    /// Simulates and draws tens of thousands of leaves without one GameObject each.
    ///
    /// Data: one <see cref="LeafState"/> per leaf in a persistent NativeArray, plus a uniform
    /// grid (counting sort) for radius queries and pile heights. The more leaves in a cell,
    /// the higher they stack, which is what makes raked leaves look like piles.
    ///
    /// Frame flow (see docs/leaves-performance.md):
    /// - Update (order -100): finish last frame's jobs, so tools and queries see fresh data.
    /// - Tools call Push / BlowCone / Attract / CollectInRadius during their Update; each
    ///   runs as a small Burst job on the main thread.
    /// - LateUpdate: cull + build matrices (Burst, parallel per render slice), submit
    ///   instanced draws, then schedule the next simulation step (Burst, parallel) and the
    ///   grid rebuild. They run on worker threads while Unity renders the frame.
    ///
    /// Every public method completes outstanding jobs first, so it is always safe to call.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class LeafField : MonoBehaviour, ILeafQuery
    {
        public static LeafField Instance { get; private set; }
        /// <summary>The active field through the cross-package interface.</summary>
        public static ILeafQuery Query => Instance;

        /// <summary>Max instances per draw call (64 KB constant buffer of matrices).</summary>
        public const int SliceSize = 1023;

        [Header("Spawn")]
        [Tooltip("Leaves spawned. 30k is the M2 target on a mid PC.")]
        public int leafCount = 12000;
        [Tooltip("Yard size in metres (X by Z), centred on this transform.")]
        public Vector2 yardSize = new Vector2(30f, 30f);
        public int seed = 12345;
        public bool spawnOnStart = true;

        [Header("Leaf types")]
        [Tooltip("Leaf types and their spawn mix. Leave empty to use the built-in mix below.")]
        public LeafTypeDefinition[] leafTypes;
        [Tooltip("When Leaf Types is empty: on = maple/oak/wet clump/pine mix sharing Leaf Materials, off = plain oak leaves only.")]
        public bool builtInTypeMix = true;

        [Header("Rendering")]
        [Tooltip("Optional. Mesh for oak leaves (and every leaf when the built-in mix is off). A procedural leaf is generated if empty.")]
        public Mesh leafMesh;
        [Tooltip("Optional cheap mesh for leaves past LOD Distance. A procedural quad is generated if empty.")]
        public Mesh lodMesh;
        [Tooltip("Colour variants for types without their own materials. GPU Instancing is enabled on them at spawn.")]
        public Material[] leafMaterials;
        [Tooltip("Scale range for plain oak leaves (built-in mix off). Leaf types carry their own range.")]
        public Vector2 leafScaleRange = new Vector2(0.16f, 0.3f);
        [Tooltip("Shadows for near leaves only; far leaves never cast them.")]
        public ShadowCastingMode castShadows = ShadowCastingMode.Off;
        public bool receiveShadows = true;
        [Tooltip("Camera used for culling and LOD. Camera.main if empty.")]
        public Camera cullingCamera;
        [Tooltip("Skip leaves outside the culling camera's view. Turn off if leaves vanish in the Scene view during Play.")]
        public bool frustumCulling = true;
        [Tooltip("Leaves farther than this (m) use the LOD mesh and no shadows. 0 = no LOD.")]
        [Min(0f)] public float lodDistance = 18f;
        [Tooltip("Leaves farther than this (m) are not drawn. 0 = no limit.")]
        [Min(0f)] public float cullDistance = 90f;

        [Header("Simulation")]
        [Tooltip("Grid cell size (m). Smaller = finer piles, more cells to scan.")]
        public float cellSize = 0.4f;
        [Tooltip("Velocity decay per second. Leaf types scale it with their drag.")]
        public float damping = 5f;
        [Tooltip("Speed cap (m/s).")]
        public float maxSpeed = 8f;
        [Tooltip("Stack height each leaf adds to its grid cell.")]
        public float pileHeightPerLeaf = 0.012f;
        public float maxPileHeight = 1.1f;
        [Tooltip("Leaves start sliding off a cell when it has this many more leaves than its lowest neighbour.")]
        [Min(1)] public int reposeDifference = 14;
        [Tooltip("How hard top-of-pile leaves slide off steep piles.")]
        public float slumpStrength = 2.5f;
        [Tooltip("Gravity on airborne leaves (m/s²). Leaf types scale it.")]
        public float liftGravity = 9f;
        [Tooltip("Highest a leaf can be blown above its rest point (m).")]
        public float maxLift = 3f;
        [Tooltip("Optional terrain to sit leaves on (baked into the grid at spawn). Otherwise the ground is flat at this transform's Y.")]
        public Terrain terrain;

        [Header("Wind")]
        [Tooltip("Scales how hard wind above a type's threshold pushes leaves.")]
        public float windForce = 1.5f;
        [Tooltip("Chance per second (at strong wind) that an exposed leaf is picked up and tumbles.")]
        public float windLiftChance = 0.8f;

        public int TotalLeaves => _count;
        public int CollectedLeaves { get; private set; }
        public int RemainingLeaves => _count - CollectedLeaves;
        public float CleanFraction => _count == 0 ? 1f : (float)CollectedLeaves / _count;

        public int MovingLeaves
        {
            get
            {
                CompleteJobs();
                return _allocated ? _stats[1] : 0;
            }
        }

        public int GridWidth => _allocated ? _grid.width : 0;
        public int GridHeight => _allocated ? _grid.height : 0;
        public float CellSize => _grid.cellSize > 0f ? _grid.cellSize : cellSize;

        public int LeafTypeCount => _types.Count;
        public string LeafTypeId(int typeIndex) => _types[typeIndex].id;
        public int RenderSliceCount => _allocated ? _slices.Length : 0;
        public LeafRenderStats LastRenderStats { get; private set; }

        public Vector3 WindDirection => new Vector3(_windDir.x, 0f, _windDir.y);
        public float WindStrength => _windStrength;

        /// <summary>Raised with the number of leaves removed (bagged).</summary>
        public event Action<int> OnLeavesCollected;
        public event Action OnFieldSpawned;

        sealed class ResolvedType
        {
            public string id;
            public float weight;
            public Vector2 scaleRange;
            public Mesh mesh;
            public Mesh lodMesh;
            public Material[] materials;
            public LeafTypeParams physics;
        }

        sealed class RenderBucket
        {
            public Mesh mesh;
            public Mesh lodMesh;
            public Material material;
        }

        // Native data
        NativeArray<LeafState> _leaves;
        NativeArray<LeafTypeParams> _typeParams;
        NativeArray<int> _cellCount, _cellStart, _cellCursor, _cellLeaves;
        NativeArray<float> _cellHeight, _groundCorners;
        NativeArray<int> _stats, _result;
        NativeArray<int3> _slices;
        NativeArray<int2> _sliceCounts;
        NativeArray<Matrix4x4> _nearMatrices, _farMatrices;
        NativeArray<float4> _planes;
        bool _allocated;

        JobHandle _stepHandle;
        bool _jobsPending;

        readonly List<ResolvedType> _types = new List<ResolvedType>();
        readonly List<RenderBucket> _buckets = new List<RenderBucket>();
        readonly List<Mesh> _ownedMeshes = new List<Mesh>();
        readonly Plane[] _planeScratch = new Plane[6];
        bool _ownsLeafMesh;
        Mesh _ownedLodMesh;

        LeafGrid _grid;
        int _count;
        float2 _windDir = new float2(0f, 1f);
        float _windStrength;
        float _time;
        uint _frame;
        Bounds _renderBounds;
        bool _warnedNoMaterial;

        void Awake() => Instance = this;

        void OnDisable() => CompleteJobs();

        void OnDestroy()
        {
            CompleteJobs();
            DisposeNative();
            DestroyOwnedMeshes();
            if (_ownsLeafMesh && leafMesh != null) DestroySafe(leafMesh);
            _ownsLeafMesh = false;
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            if (spawnOnStart) Spawn(seed);
        }

        // ------------------------------------------------------------------ spawn

        public void Spawn(int spawnSeed)
        {
            CompleteJobs();
            DisposeNative();
            DestroyOwnedMeshes();

            _count = Mathf.Max(0, leafCount);
            CollectedLeaves = 0;
            _grid = LeafGrid.Create(transform.position, yardSize, cellSize);
            var rng = new System.Random(spawnSeed);
            float R() => (float)rng.NextDouble();

            ResolveTypes();
            var bucketBase = BuildBuckets();
            float totalWeight = 0f;
            foreach (var t in _types) totalWeight += t.weight;

            // Generate, then sort by (render bucket, Z-order cell) so each render slice is one
            // material and roughly one patch of yard.
            var temp = new LeafState[_count];
            var keys = new ulong[_count];
            for (int i = 0; i < _count; i++)
            {
                int type = PickType(R() * totalWeight);
                var rt = _types[type];
                int variants = Mathf.Max(1, rt.materials.Length);
                int variant = Mathf.Min(variants - 1, (int)(R() * variants));
                var p = SpawnSampler != null
                    ? SampledSpawnPoint(rng)
                    : new float2(_grid.origin.x + R() * yardSize.x, _grid.origin.y + R() * yardSize.y);
                temp[i] = new LeafState
                {
                    pos = p,
                    yaw = R() * 360f,
                    tilt = (R() - 0.5f) * 30f,
                    scale = Mathf.Lerp(rt.scaleRange.x, rt.scaleRange.y, R()),
                    stackFrac = R(),
                    type = type,
                };
                int2 c = _grid.CellXY(p);
                keys[i] = ((ulong)(uint)(bucketBase[type] + variant) << 32) | LeafPhysics.Morton2((uint)c.x, (uint)c.y);
            }
            Array.Sort(keys, temp);

            AllocateNative(temp, keys);
            BakeGround();
            for (int i = 0; i < _count; i++)
            {
                LeafState s = _leaves[i];
                s.ground = LeafPhysics.SampleGround(_grid, _groundCorners, s.pos);
                _leaves[i] = s;
            }

            RunGrid();
            for (int i = 0; i < _count; i++)
            {
                LeafState s = _leaves[i];
                s.height = s.ground + _cellHeight[s.cell] * s.stackFrac;
                _leaves[i] = s;
            }

            Vector3 center = transform.position;
            _renderBounds = new Bounds(center + Vector3.up * 2f, new Vector3(yardSize.x + 4f, 20f, yardSize.y + 4f));
            _warnedNoMaterial = false;
            OnFieldSpawned?.Invoke();
        }

        /// <summary>
        /// Where leaves land on <see cref="Spawn"/>. Null spreads them evenly over the yard.
        /// Yards (WP7) set this to put leaves under trees and keep them out of houses.
        /// Not serialized: set it before Start (or before calling Spawn).
        /// </summary>
        public ILeafSpawnSampler SpawnSampler { get; set; }

        float2 SampledSpawnPoint(System.Random rng)
        {
            Vector2 local = SpawnSampler.SamplePosition(rng);
            float hx = yardSize.x * 0.5f, hz = yardSize.y * 0.5f;
            // Keep a hair inside the edge so the leaf maps to a valid grid cell.
            float x = Mathf.Clamp(local.x, -hx, hx - 0.001f) + hx;
            float z = Mathf.Clamp(local.y, -hz, hz - 0.001f) + hz;
            return new float2(_grid.origin.x + Mathf.Max(0f, x), _grid.origin.y + Mathf.Max(0f, z));
        }

        void ResolveTypes()
        {
            _types.Clear();
            Material[] fallbackMats = leafMaterials ?? Array.Empty<Material>();
            if (leafTypes != null)
            {
                foreach (var def in leafTypes)
                {
                    if (def == null) continue;
                    _types.Add(new ResolvedType
                    {
                        id = def.typeId,
                        weight = Mathf.Max(0f, def.spawnWeight),
                        scaleRange = def.scaleRange,
                        mesh = def.mesh != null ? def.mesh : Own(LeafMeshFactory.Create(def.shape)),
                        lodMesh = def.lodMesh != null ? def.lodMesh : SharedLodMesh(),
                        materials = def.materials != null && def.materials.Length > 0 ? def.materials : fallbackMats,
                        physics = def.physics.ToParams(),
                    });
                }
            }

            if (_types.Count == 0)
            {
                if (leafMesh == null)
                {
                    leafMesh = LeafMeshFactory.CreateLeafMesh();
                    _ownsLeafMesh = true;
                }

                if (builtInTypeMix)
                {
                    foreach (LeafPreset preset in Enum.GetValues(typeof(LeafPreset)))
                    {
                        _types.Add(new ResolvedType
                        {
                            id = LeafTypeDefinition.PresetId(preset),
                            weight = LeafPhysicsSettings.SpawnWeightFor(preset),
                            scaleRange = LeafPhysicsSettings.ScaleRangeFor(preset),
                            mesh = preset == LeafPreset.Oak ? leafMesh : Own(LeafMeshFactory.Create(preset)),
                            lodMesh = SharedLodMesh(),
                            materials = fallbackMats,
                            physics = LeafPhysicsSettings.For(preset).ToParams(),
                        });
                    }
                }
                else
                {
                    _types.Add(new ResolvedType
                    {
                        id = LeafTypeDefinition.PresetId(LeafPreset.Oak),
                        weight = 1f,
                        scaleRange = leafScaleRange,
                        mesh = leafMesh,
                        lodMesh = SharedLodMesh(),
                        materials = fallbackMats,
                        physics = LeafPhysicsSettings.For(LeafPreset.Oak).ToParams(),
                    });
                }
            }

            float total = 0f;
            foreach (var t in _types) total += t.weight;
            if (total <= 0f)
                foreach (var t in _types) t.weight = 1f;
        }

        /// <summary>One bucket per (type, material variant). Returns each type's first bucket index.</summary>
        int[] BuildBuckets()
        {
            _buckets.Clear();
            var bucketBase = new int[_types.Count];
            for (int t = 0; t < _types.Count; t++)
            {
                var rt = _types[t];
                bucketBase[t] = _buckets.Count;
                int variants = Mathf.Max(1, rt.materials.Length);
                for (int v = 0; v < variants; v++)
                {
                    Material mat = v < rt.materials.Length ? rt.materials[v] : null;
                    if (mat != null) mat.enableInstancing = true;
                    _buckets.Add(new RenderBucket { mesh = rt.mesh, lodMesh = rt.lodMesh != null ? rt.lodMesh : rt.mesh, material = mat });
                }
            }
            return bucketBase;
        }

        int PickType(float r)
        {
            for (int t = 0; t < _types.Count; t++)
            {
                if (r < _types[t].weight) return t;
                r -= _types[t].weight;
            }
            return _types.Count - 1;
        }

        void AllocateNative(LeafState[] sortedLeaves, ulong[] sortedKeys)
        {
            const Allocator A = Allocator.Persistent;
            int cells = _grid.CellCount;
            _leaves = new NativeArray<LeafState>(sortedLeaves, A);
            _typeParams = new NativeArray<LeafTypeParams>(_types.Count, A);
            for (int t = 0; t < _types.Count; t++) _typeParams[t] = _types[t].physics;
            _cellCount = new NativeArray<int>(cells, A);
            _cellStart = new NativeArray<int>(cells + 1, A);
            _cellCursor = new NativeArray<int>(cells, A);
            _cellLeaves = new NativeArray<int>(_count, A);
            _cellHeight = new NativeArray<float>(cells, A);
            _groundCorners = new NativeArray<float>((_grid.width + 1) * (_grid.height + 1), A);
            _stats = new NativeArray<int>(2, A);
            _result = new NativeArray<int>(1, A);
            _nearMatrices = new NativeArray<Matrix4x4>(_count, A, NativeArrayOptions.UninitializedMemory);
            _farMatrices = new NativeArray<Matrix4x4>(_count, A, NativeArrayOptions.UninitializedMemory);
            _planes = new NativeArray<float4>(6, A);

            var slices = new List<int3>();
            int start = 0;
            while (start < _count)
            {
                int bucket = (int)(sortedKeys[start] >> 32);
                int end = start + 1;
                while (end < _count && end - start < SliceSize && (int)(sortedKeys[end] >> 32) == bucket) end++;
                slices.Add(new int3(start, end - start, bucket));
                start = end;
            }
            _slices = new NativeArray<int3>(slices.ToArray(), A);
            _sliceCounts = new NativeArray<int2>(slices.Count, A);
            _allocated = true;
        }

        void DisposeNative()
        {
            if (!_allocated) return;
            _leaves.Dispose();
            _typeParams.Dispose();
            _cellCount.Dispose();
            _cellStart.Dispose();
            _cellCursor.Dispose();
            _cellLeaves.Dispose();
            _cellHeight.Dispose();
            _groundCorners.Dispose();
            _stats.Dispose();
            _result.Dispose();
            _slices.Dispose();
            _sliceCounts.Dispose();
            _nearMatrices.Dispose();
            _farMatrices.Dispose();
            _planes.Dispose();
            _allocated = false;
            _count = 0;
            _grid = default;
        }

        void BakeGround()
        {
            int stride = _grid.width + 1;
            for (int y = 0; y <= _grid.height; y++)
            for (int x = 0; x <= _grid.width; x++)
            {
                var p = new Vector2(_grid.origin.x + x * _grid.cellSize, _grid.origin.y + y * _grid.cellSize);
                _groundCorners[y * stride + x] = SampleGroundMain(p);
            }
        }

        Mesh Own(Mesh m)
        {
            _ownedMeshes.Add(m);
            return m;
        }

        Mesh SharedLodMesh()
        {
            if (lodMesh != null) return lodMesh;
            if (_ownedLodMesh == null) _ownedLodMesh = Own(LeafMeshFactory.CreateLodQuad());
            return _ownedLodMesh;
        }

        void DestroyOwnedMeshes()
        {
            foreach (var m in _ownedMeshes)
                if (m != null) DestroySafe(m);
            _ownedMeshes.Clear();
            _ownedLodMesh = null;
        }

        static void DestroySafe(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        // ------------------------------------------------------------------ frame

        void Update() => CompleteJobs();

        void LateUpdate()
        {
            if (!_allocated || _count == 0) return;
            CompleteJobs();
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Render();
            ScheduleStep(dt);
            JobHandle.ScheduleBatchedJobs();
        }

        /// <summary>
        /// Advances the simulation by dt and waits for it. The game loop does this itself
        /// (asynchronously); call it from tests or tools that need a deterministic step.
        /// </summary>
        public void Step(float dt)
        {
            CompleteJobs();
            if (!_allocated || _count == 0) return;
            ScheduleStep(dt);
            CompleteJobs();
        }

        void ScheduleStep(float dt)
        {
            if (dt <= 0f) return;
            _time += dt;
            _frame++;
            var settings = Settings();
            var sim = new LeafSimulateJob
            {
                leaves = _leaves,
                types = _typeParams,
                cellCount = _cellCount,
                cellHeight = _cellHeight,
                groundCorners = _groundCorners,
                grid = _grid,
                settings = settings,
                windDir = _windDir,
                windStrength = _windStrength,
                dt = dt,
                frameSeed = _frame * 0x9E3779B9u + (uint)seed,
            }.Schedule(_count, 256);
            _stepHandle = GridJob(settings).Schedule(sim);
            _jobsPending = true;
        }

        /// <summary>Waits for any running simulation jobs. Cheap when none are running.</summary>
        public void CompleteJobs()
        {
            if (!_jobsPending) return;
            _stepHandle.Complete();
            _jobsPending = false;
        }

        LeafSimSettings Settings() => new LeafSimSettings
        {
            damping = Mathf.Max(0f, damping),
            maxSpeed = Mathf.Max(0.01f, maxSpeed),
            reposeDifference = Mathf.Max(1, reposeDifference),
            slumpStrength = slumpStrength,
            liftGravity = Mathf.Max(0.01f, liftGravity),
            maxLift = Mathf.Max(0f, maxLift),
            pileHeightPerLeaf = pileHeightPerLeaf,
            maxPileHeight = maxPileHeight,
            windForce = windForce,
            windLiftChance = Mathf.Max(0f, windLiftChance),
        };

        LeafGridJob GridJob(LeafSimSettings settings) => new LeafGridJob
        {
            leaves = _leaves,
            cellCount = _cellCount,
            cellStart = _cellStart,
            cellCursor = _cellCursor,
            cellLeaves = _cellLeaves,
            cellHeight = _cellHeight,
            stats = _stats,
            grid = _grid,
            settings = settings,
        };

        void RunGrid() => GridJob(Settings()).Run();

        // ------------------------------------------------------------------ rendering

        void Render()
        {
            Camera cam = cullingCamera != null ? cullingCamera : Camera.main;
            LeafRenderStats stats = cam != null
                ? CullAndBuildMatrices(cam.transform.position, frustumCulling ? CameraPlanes(cam) : null)
                : CullAndBuildMatrices(_renderBounds.center, null, useDistances: false);

            int draws = 0;
            bool anyMaterial = false;
            for (int s = 0; s < _slices.Length; s++)
            {
                int3 slice = _slices[s];
                RenderBucket bucket = _buckets[slice.z];
                if (bucket.material == null) continue;
                anyMaterial = true;
                int2 counts = _sliceCounts[s];
                if (counts.x > 0)
                {
                    var rp = Params(bucket.material, castShadows);
                    Graphics.RenderMeshInstanced(rp, bucket.mesh, 0, _nearMatrices, counts.x, slice.x);
                    draws++;
                }
                if (counts.y > 0)
                {
                    var rp = Params(bucket.material, ShadowCastingMode.Off);
                    Graphics.RenderMeshInstanced(rp, bucket.lodMesh, 0, _farMatrices, counts.y, slice.x);
                    draws++;
                }
            }

            if (!anyMaterial && !_warnedNoMaterial && _slices.Length > 0)
            {
                Debug.LogWarning("[FallenYard] LeafField has no materials assigned; leaves are invisible.", this);
                _warnedNoMaterial = true;
            }

            stats.drawCalls = draws;
            LastRenderStats = stats;
        }

        RenderParams Params(Material mat, ShadowCastingMode shadows) => new RenderParams(mat)
        {
            worldBounds = _renderBounds,
            shadowCastingMode = shadows,
            receiveShadows = receiveShadows,
            layer = gameObject.layer,
        };

        Plane[] CameraPlanes(Camera cam)
        {
            GeometryUtility.CalculateFrustumPlanes(cam, _planeScratch);
            return _planeScratch;
        }

        /// <summary>
        /// Culls every live leaf against the camera (frustum planes may be null) and writes the
        /// near/far matrix lists that the next draw submits. Public for tests and profiling.
        /// </summary>
        public LeafRenderStats CullAndBuildMatrices(Vector3 cameraPosition, Plane[] frustumPlanes) =>
            CullAndBuildMatrices(cameraPosition, frustumPlanes, useDistances: true);

        LeafRenderStats CullAndBuildMatrices(Vector3 cameraPosition, Plane[] frustumPlanes, bool useDistances)
        {
            CompleteJobs();
            var stats = new LeafRenderStats();
            if (!_allocated || _slices.Length == 0) return stats;

            int planeCount = 0;
            if (frustumPlanes != null)
            {
                planeCount = Mathf.Min(6, frustumPlanes.Length);
                for (int p = 0; p < planeCount; p++)
                {
                    Plane pl = frustumPlanes[p];
                    _planes[p] = new float4(pl.normal.x, pl.normal.y, pl.normal.z, pl.distance);
                }
            }

            float lodSq = useDistances && lodDistance > 0f ? lodDistance * lodDistance : float.MaxValue;
            float cullSq = useDistances && cullDistance > 0f ? cullDistance * cullDistance : float.MaxValue;
            new LeafCullJob
            {
                leaves = _leaves,
                slices = _slices,
                planes = _planes,
                planeCount = planeCount,
                cameraPos = cameraPosition,
                lodDistanceSq = lodSq,
                cullDistanceSq = cullSq,
                time = _time,
                nearMatrices = _nearMatrices,
                farMatrices = _farMatrices,
                sliceCounts = _sliceCounts,
            }.Schedule(_slices.Length, 1).Complete();

            for (int s = 0; s < _sliceCounts.Length; s++)
            {
                stats.visibleNear += _sliceCounts[s].x;
                stats.visibleFar += _sliceCounts[s].y;
            }
            return stats;
        }

        // ------------------------------------------------------------------ wind

        /// <summary>Sets the wind the next steps use. Called every frame by <see cref="WindSystem"/>.</summary>
        public void SetWind(Vector3 direction, float strength)
        {
            var flat = new float2(direction.x, direction.z);
            _windDir = math.lengthsq(flat) > 1e-8f ? math.normalize(flat) : new float2(0f, 1f);
            _windStrength = Mathf.Max(0f, strength);
        }

        // ------------------------------------------------------------------ helpers

        float SampleGroundMain(Vector2 p)
        {
            if (terrain != null)
                return terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y;
            return transform.position.y;
        }

        static float2 XZ(Vector3 v) => new float2(v.x, v.z);

        public float GroundHeightAt(Vector3 world) => SampleGroundMain(new Vector2(world.x, world.z));

        public bool IsInsideYard(Vector3 world)
        {
            float2 p = XZ(world) - _grid.origin;
            return p.x >= 0f && p.y >= 0f && p.x <= _grid.size.x && p.y <= _grid.size.y;
        }

        public int CellCount(int x, int y)
        {
            CompleteJobs();
            return _cellCount[y * _grid.width + x];
        }

        public Vector3 CellCenter(int x, int y)
        {
            CompleteJobs();
            float2 p = _grid.CellCenter(x, y);
            return new Vector3(p.x, LeafPhysics.SampleGround(_grid, _groundCorners, p), p.y);
        }

        /// <summary>A copy of one leaf's state (for tests, debugging and VFX).</summary>
        public LeafState GetLeaf(int index)
        {
            CompleteJobs();
            return _leaves[index];
        }

        /// <summary>World position of a leaf as it is drawn.</summary>
        public Vector3 GetLeafPosition(int index)
        {
            LeafState s = GetLeaf(index);
            return new Vector3(s.pos.x, s.height, s.pos.y);
        }

        /// <summary>Calls visit(leafIndex) for every live leaf within radius of center (XZ).</summary>
        public void ForEachLeafInRadius(Vector3 center, float radius, Action<int> visit)
        {
            CompleteJobs();
            if (!_allocated || _count == 0) return;
            float2 c = XZ(center);
            float r2 = radius * radius;
            _grid.CellRect(c, radius, out int x0, out int y0, out int x1, out int y1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int cell = y * _grid.width + x;
                for (int k = _cellStart[cell]; k < _cellStart[cell + 1]; k++)
                {
                    int i = _cellLeaves[k];
                    LeafState s = _leaves[i];
                    if (s.collected == 0 && math.distancesq(s.pos, c) <= r2) visit(i);
                }
            }
        }

        // ------------------------------------------------------------------ tool API

        public int CountInRadius(Vector3 center, float radius)
        {
            int n = 0;
            ForEachLeafInRadius(center, radius, _ => n++);
            return n;
        }

        /// <summary>
        /// Rake-style push: leaves inside the radius blend their velocity toward
        /// targetVelocity, strongest at the centre, scaled by each type's rake grip.
        /// </summary>
        public int Push(Vector3 center, float radius, Vector3 targetVelocity, float strength, float dt)
        {
            CompleteJobs();
            if (!_allocated || _count == 0 || radius <= 0f) return 0;
            new LeafPushJob
            {
                leaves = _leaves,
                types = _typeParams,
                cellStart = _cellStart,
                cellLeaves = _cellLeaves,
                grid = _grid,
                center = XZ(center),
                radius = radius,
                targetVelocity = XZ(targetVelocity),
                strength = strength,
                dt = dt,
                result = _result,
            }.Run();
            return _result[0];
        }

        /// <summary>Blower-style cone: pushes leaves away from origin along dir and lifts them. Heavy types barely move.</summary>
        public int BlowCone(Vector3 origin, Vector3 dir, float range, float halfAngleDeg, float force, float lift, float dt)
        {
            CompleteJobs();
            if (!_allocated || _count == 0 || range <= 0f || dir.sqrMagnitude < 1e-8f) return 0;
            new LeafBlowConeJob
            {
                leaves = _leaves,
                types = _typeParams,
                cellStart = _cellStart,
                cellLeaves = _cellLeaves,
                grid = _grid,
                origin = origin,
                dir = (float3)dir.normalized,
                range = range,
                halfAngleDeg = halfAngleDeg,
                force = force,
                lift = lift,
                dt = dt,
                result = _result,
            }.Run();
            return _result[0];
        }

        /// <summary>Vacuum-style suction: leaves in the radius are drawn toward center at up to speed m/s.</summary>
        public int Attract(Vector3 center, float radius, float speed, float strength, float dt)
        {
            CompleteJobs();
            if (!_allocated || _count == 0 || radius <= 0f) return 0;
            new LeafAttractJob
            {
                leaves = _leaves,
                types = _typeParams,
                cellStart = _cellStart,
                cellLeaves = _cellLeaves,
                grid = _grid,
                center = XZ(center),
                radius = radius,
                speed = speed,
                strength = strength,
                dt = dt,
                result = _result,
            }.Run();
            return _result[0];
        }

        /// <summary>Removes up to maxLeaves leaves within radius (closest first is not guaranteed). Returns how many.</summary>
        public int CollectInRadius(Vector3 center, float radius, int maxLeaves = int.MaxValue)
        {
            CompleteJobs();
            if (!_allocated || _count == 0 || maxLeaves <= 0) return 0;
            float2 c = XZ(center);
            float r2 = radius * radius;
            int n = 0;
            _grid.CellRect(c, radius, out int x0, out int y0, out int x1, out int y1);
            for (int y = y0; y <= y1 && n < maxLeaves; y++)
            for (int x = x0; x <= x1 && n < maxLeaves; x++)
            {
                int cell = y * _grid.width + x;
                for (int k = _cellStart[cell]; k < _cellStart[cell + 1] && n < maxLeaves; k++)
                {
                    int i = _cellLeaves[k];
                    LeafState s = _leaves[i];
                    if (s.collected != 0 || math.distancesq(s.pos, c) > r2) continue;
                    s.collected = 1;
                    s.vel = float2.zero;
                    s.lift = 0f;
                    s.liftVel = 0f;
                    _leaves[i] = s;
                    n++;
                }
            }

            if (n > 0)
            {
                CollectedLeaves += n;
                RunGrid();
                OnLeavesCollected?.Invoke(n);
            }
            return n;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
            Gizmos.DrawWireCube(transform.position, new Vector3(yardSize.x, 0.1f, yardSize.y));
        }
    }
}

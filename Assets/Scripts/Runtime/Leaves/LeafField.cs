using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FallenYard.Leaves
{
    /// <summary>
    /// Simulates and draws thousands of leaves without one GameObject each.
    ///
    /// - Leaves are plain arrays (position, velocity, height...) on the XZ plane.
    /// - A uniform grid is rebuilt every frame (counting sort, O(n)) for fast
    ///   radius queries and for pile heights: the more leaves in a cell, the
    ///   higher they stack, which is what makes raked leaves look like piles.
    /// - Leaves slide off a cell when it is much taller than a neighbour
    ///   (angle of repose), so piles slump into mounds instead of towers.
    /// - Rendering uses Graphics.RenderMeshInstanced in batches of 1023 and only
    ///   rewrites matrices for leaves that moved.
    ///
    /// For 50k+ leaves, move Simulate/RebuildGrid into Burst jobs; the data
    /// layout here is already job-friendly.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class LeafField : MonoBehaviour
    {
        public static LeafField Instance { get; private set; }
        const int BatchSize = 1023;

        [Header("Spawn")]
        public int leafCount = 12000;
        [Tooltip("Yard size in metres (X by Z), centred on this transform.")]
        public Vector2 yardSize = new Vector2(30f, 30f);
        public int seed = 12345;
        public bool spawnOnStart = true;

        [Header("Rendering")]
        [Tooltip("Optional. A procedural leaf is generated if empty.")]
        public Mesh leafMesh;
        [Tooltip("Colour variants. GPU Instancing must be enabled (done automatically at runtime).")]
        public Material[] leafMaterials;
        public Vector2 leafScaleRange = new Vector2(0.16f, 0.3f);
        public ShadowCastingMode castShadows = ShadowCastingMode.Off;
        public bool receiveShadows = true;

        [Header("Simulation")]
        public float cellSize = 0.4f;
        public float damping = 5f;
        public float maxSpeed = 8f;
        [Tooltip("Stack height each leaf adds to its grid cell.")]
        public float pileHeightPerLeaf = 0.012f;
        public float maxPileHeight = 1.1f;
        [Tooltip("Leaves start sliding off a cell when it has this many more leaves than its lowest neighbour.")]
        public int reposeDifference = 14;
        public float slumpStrength = 2.5f;
        public float liftGravity = 9f;
        [Tooltip("Optional terrain to sit leaves on. Otherwise the ground is flat at this transform's Y.")]
        public Terrain terrain;

        public int TotalLeaves => _count;
        public int CollectedLeaves { get; private set; }
        public int RemainingLeaves => _count - CollectedLeaves;
        public float CleanFraction => _count == 0 ? 1f : (float)CollectedLeaves / _count;

        public int GridWidth => _gw;
        public int GridHeight => _gh;
        public float CellSize => cellSize;

        /// <summary>Raised with the number of leaves removed (bagged).</summary>
        public event Action<int> OnLeavesCollected;
        public event Action OnFieldSpawned;

        // Per-leaf data
        Vector2[] _pos, _vel;
        float[] _height, _ground, _lift, _liftVel, _yaw, _tilt, _scale, _stackFrac;
        bool[] _collected;
        int[] _leafCell;
        Matrix4x4[][] _batches;
        int _count;

        // Grid
        int _gw, _gh;
        int[] _cellCount, _cellStart, _cellCursor, _cellLeaves;
        float[] _cellHeight;
        Vector2 _origin;
        Bounds _renderBounds;
        bool _warnedNoMaterial;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            if (spawnOnStart) Spawn(seed);
        }

        // ------------------------------------------------------------------ spawn

        public void Spawn(int spawnSeed)
        {
            _count = Mathf.Max(0, leafCount);
            var rng = new System.Random(spawnSeed);
            float R() => (float)rng.NextDouble();

            _pos = new Vector2[_count];
            _vel = new Vector2[_count];
            _height = new float[_count];
            _ground = new float[_count];
            _lift = new float[_count];
            _liftVel = new float[_count];
            _yaw = new float[_count];
            _tilt = new float[_count];
            _scale = new float[_count];
            _stackFrac = new float[_count];
            _collected = new bool[_count];
            _leafCell = new int[_count];

            Vector3 c = transform.position;
            _origin = new Vector2(c.x - yardSize.x * 0.5f, c.z - yardSize.y * 0.5f);
            _gw = Mathf.Max(1, Mathf.CeilToInt(yardSize.x / cellSize));
            _gh = Mathf.Max(1, Mathf.CeilToInt(yardSize.y / cellSize));
            int cells = _gw * _gh;
            _cellCount = new int[cells];
            _cellStart = new int[cells + 1];
            _cellCursor = new int[cells];
            _cellHeight = new float[cells];
            _cellLeaves = new int[_count];

            _renderBounds = new Bounds(c + Vector3.up * 2f, new Vector3(yardSize.x + 4f, 20f, yardSize.y + 4f));

            for (int i = 0; i < _count; i++)
            {
                // Slight clumping: half the leaves fall near a few random "drop zones" (under trees).
                Vector2 p = new Vector2(R() * yardSize.x, R() * yardSize.y);
                _pos[i] = _origin + p;
                _yaw[i] = R() * 360f;
                _tilt[i] = (R() - 0.5f) * 30f;
                _scale[i] = Mathf.Lerp(leafScaleRange.x, leafScaleRange.y, R());
                _stackFrac[i] = R();
                _ground[i] = SampleGround(_pos[i]);
            }

            int batchCount = (_count + BatchSize - 1) / BatchSize;
            _batches = new Matrix4x4[batchCount][];
            for (int b = 0; b < batchCount; b++)
                _batches[b] = new Matrix4x4[Mathf.Min(BatchSize, _count - b * BatchSize)];

            if (leafMesh == null) leafMesh = LeafMeshFactory.CreateLeafMesh();
            if (leafMaterials != null)
                foreach (var m in leafMaterials)
                    if (m != null) m.enableInstancing = true;

            CollectedLeaves = 0;
            RebuildGrid();
            for (int i = 0; i < _count; i++)
            {
                _height[i] = _ground[i] + _cellHeight[_leafCell[i]] * _stackFrac[i];
                WriteMatrix(i);
            }
            OnFieldSpawned?.Invoke();
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (_count == 0) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Simulate(dt);
            RebuildGrid();
            Render();
        }

        void Simulate(float dt)
        {
            float damp = Mathf.Exp(-damping * dt);
            Vector2 min = _origin, max = _origin + yardSize;
            float maxSpeedSq = maxSpeed * maxSpeed;

            for (int i = 0; i < _count; i++)
            {
                if (_collected[i]) continue;
                bool changed = false;
                int cell = _leafCell[i];

                // Angle of repose: top-of-pile leaves slide toward the lowest neighbour.
                if (_stackFrac[i] > 0.4f && _cellCount[cell] > reposeDifference)
                {
                    Vector2 slide = LowestNeighbourDirection(cell, out int diff);
                    if (diff > reposeDifference)
                        _vel[i] += slide * (slumpStrength * Mathf.Min(2f, (float)diff / reposeDifference) * dt);
                }

                Vector2 v = _vel[i];
                if (v.sqrMagnitude > 1e-6f)
                {
                    if (v.sqrMagnitude > maxSpeedSq) v = v.normalized * maxSpeed;
                    Vector2 p = _pos[i] + v * dt;
                    if (p.x < min.x) { p.x = min.x; v.x = 0f; }
                    else if (p.x > max.x) { p.x = max.x; v.x = 0f; }
                    if (p.y < min.y) { p.y = min.y; v.y = 0f; }
                    else if (p.y > max.y) { p.y = max.y; v.y = 0f; }
                    _pos[i] = p;
                    _yaw[i] += v.magnitude * 90f * dt * ((i & 1) == 0 ? 1f : -1f);
                    v *= damp;
                    if (v.sqrMagnitude < 1e-4f) v = Vector2.zero;
                    _vel[i] = v;
                    _ground[i] = SampleGround(p);
                    changed = true;
                }

                if (_lift[i] > 0f || _liftVel[i] > 0f)
                {
                    _liftVel[i] -= liftGravity * dt;
                    _lift[i] += _liftVel[i] * dt;
                    if (_lift[i] <= 0f) { _lift[i] = 0f; _liftVel[i] = 0f; }
                    changed = true;
                }

                float target = _ground[i] + _cellHeight[cell] * _stackFrac[i] + _lift[i];
                if (Mathf.Abs(target - _height[i]) > 0.0005f)
                {
                    _height[i] = _lift[i] > 0f ? target : Mathf.MoveTowards(_height[i], target, 1.5f * dt);
                    changed = true;
                }

                if (changed) WriteMatrix(i);
            }
        }

        Vector2 LowestNeighbourDirection(int cell, out int diff)
        {
            int x = cell % _gw, y = cell / _gw;
            int own = _cellCount[cell];
            int best = own;
            Vector2 dir = Vector2.zero;
            if (x > 0 && _cellCount[cell - 1] < best) { best = _cellCount[cell - 1]; dir = Vector2.left; }
            if (x < _gw - 1 && _cellCount[cell + 1] < best) { best = _cellCount[cell + 1]; dir = Vector2.right; }
            if (y > 0 && _cellCount[cell - _gw] < best) { best = _cellCount[cell - _gw]; dir = Vector2.down; }
            if (y < _gh - 1 && _cellCount[cell + _gw] < best) { best = _cellCount[cell + _gw]; dir = Vector2.up; }
            diff = own - best;
            return dir;
        }

        void RebuildGrid()
        {
            int cells = _cellCount.Length;
            Array.Clear(_cellCount, 0, cells);
            for (int i = 0; i < _count; i++)
            {
                if (_collected[i]) { _leafCell[i] = 0; continue; }
                int c = CellIndex(_pos[i]);
                _leafCell[i] = c;
                _cellCount[c]++;
            }

            _cellStart[0] = 0;
            for (int c = 0; c < cells; c++) _cellStart[c + 1] = _cellStart[c] + _cellCount[c];
            Array.Copy(_cellStart, _cellCursor, cells);
            for (int i = 0; i < _count; i++)
                if (!_collected[i]) _cellLeaves[_cellCursor[_leafCell[i]]++] = i;

            // Smoothed pile heights (own cell + 4 neighbours) for rounder mounds.
            for (int y = 0; y < _gh; y++)
            for (int x = 0; x < _gw; x++)
            {
                int c = y * _gw + x;
                float sum = _cellCount[c] * 0.5f;
                sum += (x > 0 ? _cellCount[c - 1] : _cellCount[c]) * 0.125f;
                sum += (x < _gw - 1 ? _cellCount[c + 1] : _cellCount[c]) * 0.125f;
                sum += (y > 0 ? _cellCount[c - _gw] : _cellCount[c]) * 0.125f;
                sum += (y < _gh - 1 ? _cellCount[c + _gw] : _cellCount[c]) * 0.125f;
                _cellHeight[c] = Mathf.Min(maxPileHeight, 0.01f + sum * pileHeightPerLeaf);
            }
        }

        void WriteMatrix(int i)
        {
            ref Matrix4x4 m = ref _batches[i / BatchSize][i % BatchSize];
            if (_collected[i])
            {
                m = Matrix4x4.zero;
                return;
            }
            var rot = Quaternion.Euler(_tilt[i], _yaw[i], _tilt[i] * 0.5f);
            m = Matrix4x4.TRS(new Vector3(_pos[i].x, _height[i], _pos[i].y), rot, Vector3.one * _scale[i]);
        }

        void Render()
        {
            if (leafMaterials == null || leafMaterials.Length == 0)
            {
                if (!_warnedNoMaterial)
                {
                    Debug.LogWarning("[FallenYard] LeafField has no materials assigned; leaves are invisible.", this);
                    _warnedNoMaterial = true;
                }
                return;
            }

            for (int b = 0; b < _batches.Length; b++)
            {
                var mat = leafMaterials[b % leafMaterials.Length];
                if (mat == null) continue;
                var rp = new RenderParams(mat)
                {
                    worldBounds = _renderBounds,
                    shadowCastingMode = castShadows,
                    receiveShadows = receiveShadows,
                    layer = gameObject.layer,
                };
                Graphics.RenderMeshInstanced(rp, leafMesh, 0, _batches[b]);
            }
        }

        // ------------------------------------------------------------------ helpers

        float SampleGround(Vector2 p)
        {
            if (terrain != null)
                return terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y;
            return transform.position.y;
        }

        int CellIndex(Vector2 p)
        {
            int x = Mathf.Clamp((int)((p.x - _origin.x) / cellSize), 0, _gw - 1);
            int y = Mathf.Clamp((int)((p.y - _origin.y) / cellSize), 0, _gh - 1);
            return y * _gw + x;
        }

        static Vector2 XZ(Vector3 v) => new Vector2(v.x, v.z);

        public float GroundHeightAt(Vector3 world) => SampleGround(XZ(world));

        public bool IsInsideYard(Vector3 world)
        {
            Vector2 p = XZ(world) - _origin;
            return p.x >= 0f && p.y >= 0f && p.x <= yardSize.x && p.y <= yardSize.y;
        }

        public int CellCount(int x, int y) => _cellCount[y * _gw + x];

        public Vector3 CellCenter(int x, int y)
        {
            Vector2 p = _origin + new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);
            return new Vector3(p.x, SampleGround(p), p.y);
        }

        /// <summary>Calls visit(leafIndex) for every live leaf within radius of center (XZ).</summary>
        public void ForEachLeafInRadius(Vector3 center, float radius, Action<int> visit)
        {
            if (_count == 0) return;
            Vector2 c = XZ(center);
            float r2 = radius * radius;
            int x0 = Mathf.Clamp((int)((c.x - radius - _origin.x) / cellSize), 0, _gw - 1);
            int x1 = Mathf.Clamp((int)((c.x + radius - _origin.x) / cellSize), 0, _gw - 1);
            int y0 = Mathf.Clamp((int)((c.y - radius - _origin.y) / cellSize), 0, _gh - 1);
            int y1 = Mathf.Clamp((int)((c.y + radius - _origin.y) / cellSize), 0, _gh - 1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int cell = y * _gw + x;
                for (int k = _cellStart[cell]; k < _cellStart[cell + 1]; k++)
                {
                    int i = _cellLeaves[k];
                    if (!_collected[i] && (_pos[i] - c).sqrMagnitude <= r2) visit(i);
                }
            }
        }

        readonly List<int> _scratch = new List<int>(2048);

        List<int> Gather(Vector3 center, float radius)
        {
            _scratch.Clear();
            ForEachLeafInRadius(center, radius, _scratch.Add);
            return _scratch;
        }

        // ------------------------------------------------------------------ tool API

        /// <summary>
        /// Rake-style push: leaves inside the radius blend their velocity toward
        /// targetVelocity, strongest at the centre.
        /// </summary>
        public void Push(Vector3 center, float radius, Vector3 targetVelocity, float strength, float dt)
        {
            Vector2 c = XZ(center), target = XZ(targetVelocity);
            foreach (int i in Gather(center, radius))
            {
                float falloff = 1f - Mathf.Clamp01((_pos[i] - c).magnitude / radius);
                float t = Mathf.Clamp01(strength * (0.35f + 0.65f * falloff) * dt);
                _vel[i] = Vector2.Lerp(_vel[i], target, t);
            }
        }

        /// <summary>Blower-style cone: pushes leaves away from origin along dir and lifts them.</summary>
        public void BlowCone(Vector3 origin, Vector3 dir, float range, float halfAngleDeg, float force, float lift, float dt)
        {
            dir.Normalize();
            Vector3 mid = origin + dir * (range * 0.5f);
            foreach (int i in Gather(mid, range * 0.75f))
            {
                Vector3 leaf = new Vector3(_pos[i].x, _height[i], _pos[i].y);
                Vector3 to = leaf - origin;
                float dist = to.magnitude;
                if (dist > range || dist < 0.01f) continue;
                float angle = Vector3.Angle(dir, to);
                if (angle > halfAngleDeg) continue;

                float falloff = (1f - dist / range) * (1f - angle / halfAngleDeg);
                Vector2 push = new Vector2(to.x, to.z);
                if (push.sqrMagnitude < 1e-4f) push = new Vector2(dir.x, dir.z);
                push.Normalize();
                _vel[i] += push * (force * falloff * dt);
                if (_lift[i] < 0.6f) _liftVel[i] = Mathf.Max(_liftVel[i], lift * falloff);
            }
        }

        public int CountInRadius(Vector3 center, float radius)
        {
            int n = 0;
            ForEachLeafInRadius(center, radius, _ => n++);
            return n;
        }

        /// <summary>Removes up to maxLeaves leaves within radius (closest first is not guaranteed). Returns how many.</summary>
        public int CollectInRadius(Vector3 center, float radius, int maxLeaves = int.MaxValue)
        {
            int n = 0;
            foreach (int i in Gather(center, radius))
            {
                if (n >= maxLeaves) break;
                _collected[i] = true;
                _vel[i] = Vector2.zero;
                WriteMatrix(i);
                n++;
            }
            if (n > 0)
            {
                CollectedLeaves += n;
                RebuildGrid();
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

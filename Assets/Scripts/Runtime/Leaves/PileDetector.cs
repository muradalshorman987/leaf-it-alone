using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallenYard.Leaves
{
    public struct LeafPile
    {
        public Vector3 center;
        public int leafCount;
        public int cellCount;
        /// <summary>Approximate radius in metres covering the pile's cells.</summary>
        public float radius;
    }

    /// <summary>
    /// Periodically scans the LeafField grid for dense cells and flood-fills
    /// them into piles. Bagging only works on detected piles.
    /// </summary>
    public class PileDetector : MonoBehaviour
    {
        public static PileDetector Instance { get; private set; }

        [Tooltip("A grid cell counts as 'piled' with at least this many leaves.")]
        public int denseCellThreshold = 10;
        [Tooltip("A cluster needs this many leaves to count as a pile.")]
        public int minLeavesPerPile = 60;
        public float scanInterval = 0.4f;

        public IReadOnlyList<LeafPile> Piles => _piles;
        /// <summary>Total leaves currently sitting in detected piles.</summary>
        public int LeavesInPiles { get; private set; }
        public event Action OnPilesUpdated;

        readonly List<LeafPile> _piles = new List<LeafPile>();
        readonly Stack<int> _stack = new Stack<int>();
        bool[] _visited;
        float _nextScan;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (Time.time < _nextScan) return;
            _nextScan = Time.time + scanInterval;
            Scan();
        }

        public void Scan()
        {
            var field = LeafField.Instance;
            _piles.Clear();
            LeavesInPiles = 0;
            if (field == null || field.GridWidth == 0) return;

            int w = field.GridWidth, h = field.GridHeight;
            if (_visited == null || _visited.Length != w * h) _visited = new bool[w * h];
            else Array.Clear(_visited, 0, _visited.Length);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int start = y * w + x;
                if (_visited[start] || field.CellCount(x, y) < denseCellThreshold) continue;

                int leaves = 0, cells = 0;
                Vector3 weighted = Vector3.zero;
                _stack.Push(start);
                _visited[start] = true;
                while (_stack.Count > 0)
                {
                    int c = _stack.Pop();
                    int cx = c % w, cy = c / w;
                    int n = field.CellCount(cx, cy);
                    leaves += n;
                    cells++;
                    weighted += field.CellCenter(cx, cy) * n;

                    TryVisit(cx - 1, cy); TryVisit(cx + 1, cy);
                    TryVisit(cx, cy - 1); TryVisit(cx, cy + 1);
                }

                if (leaves >= minLeavesPerPile)
                {
                    _piles.Add(new LeafPile
                    {
                        center = weighted / leaves,
                        leafCount = leaves,
                        cellCount = cells,
                        radius = Mathf.Sqrt(cells / Mathf.PI) * field.CellSize + field.CellSize,
                    });
                    LeavesInPiles += leaves;
                }

                void TryVisit(int vx, int vy)
                {
                    if (vx < 0 || vy < 0 || vx >= w || vy >= h) return;
                    int i = vy * w + vx;
                    if (_visited[i] || field.CellCount(vx, vy) < denseCellThreshold) return;
                    _visited[i] = true;
                    _stack.Push(i);
                }
            }
            OnPilesUpdated?.Invoke();
        }

        /// <summary>Finds the pile whose edge is closest to point, within maxDistance of that edge.</summary>
        public bool TryGetPileNear(Vector3 point, float maxDistance, out LeafPile pile)
        {
            pile = default;
            float best = float.MaxValue;
            foreach (var p in _piles)
            {
                Vector3 d = p.center - point;
                d.y = 0f;
                float edgeDist = d.magnitude - p.radius;
                if (edgeDist <= maxDistance && edgeDist < best)
                {
                    best = edgeDist;
                    pile = p;
                }
            }
            return best < float.MaxValue;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.8f);
            foreach (var p in _piles) Gizmos.DrawWireSphere(p.center, p.radius);
        }
    }
}

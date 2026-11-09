using System.Collections.Generic;
using FallenYard.Core;
using FallenYard.Leaves;
using FallenYard.Settings;
using UnityEngine;

namespace FallenYard.Accessibility
{
    /// <summary>
    /// Draws a ring on the ground around every detected leaf pile, in the player's colour-blind
    /// palette (<see cref="AccessibilityPalette"/>). The pile the player can bag right now gets
    /// the "ready" colour and a thicker, pulsing ring, so it never relies on colour alone.
    /// Zero setup: added next to the UIManager by <see cref="OnboardingBootstrap"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class PileHighlighter : MonoBehaviour
    {
        [Tooltip("Ring line width in metres.")]
        public float lineWidth = 0.05f;
        [Tooltip("Width multiplier for the pile you can bag right now.")]
        public float readyWidthMultiplier = 2.2f;
        [Tooltip("Pulses per second on the ready ring.")]
        public float pulseSpeed = 1.5f;
        [Tooltip("Points per ring.")]
        [Range(12, 96)] public int segments = 40;
        [Tooltip("How far (m) the player can reach to bag; matches BagInteractor.reach.")]
        public float reach = 3.5f;
        [Tooltip("Height above the ground so the ring isn't hidden in the grass.")]
        public float groundOffset = 0.04f;
        [Tooltip("Ring alpha for ordinary piles.")]
        [Range(0f, 1f)] public float pileAlpha = 0.65f;

        ColorBlindMode _mode = ColorBlindMode.Off;
        bool _settingEnabled = true;
        bool _playing = true;
        Material _material;
        readonly List<LineRenderer> _rings = new List<LineRenderer>();
        readonly List<Vector3> _points = new List<Vector3>();

        public ColorBlindMode Mode => _mode;
        public int VisibleRings { get; private set; }

        void OnEnable()
        {
            SettingsManager.Changed += ApplySettings;
            GameEvents.StateChanged += OnStateChanged;
            if (SettingsManager.Instance != null) ApplySettings(SettingsManager.Instance.Current);
        }

        void OnDisable()
        {
            SettingsManager.Changed -= ApplySettings;
            GameEvents.StateChanged -= OnStateChanged;
            HideFrom(0);
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        public void ApplySettings(GameSettings s)
        {
            if (s == null) return;
            _mode = s.colorBlindMode;
            _settingEnabled = s.pileHighlights;
        }

        void OnStateChanged(GamePhase phase) => _playing = phase == GamePhase.Playing;

        void LateUpdate()
        {
            var detector = PileDetector.Instance;
            if (!_settingEnabled || !_playing || detector == null || detector.Piles.Count == 0)
            {
                HideFrom(0);
                return;
            }

            var cam = Camera.main;
            int ready = cam != null ? FindReadyPile(detector.Piles, cam.transform.position, cam.transform.forward, reach) : -1;
            var field = LeafField.Instance;

            int i = 0;
            for (; i < detector.Piles.Count; i++)
            {
                var pile = detector.Piles[i];
                var ring = GetRing(i);
                bool isReady = i == ready;
                Color c = AccessibilityPalette.Get(_mode, isReady ? PaletteRole.PileReady : PaletteRole.Pile);
                c.a = isReady ? 1f : pileAlpha;
                ring.startColor = ring.endColor = c;
                float w = isReady ? lineWidth * readyWidthMultiplier * PulseScale(Time.unscaledTime, pulseSpeed) : lineWidth;
                ring.startWidth = ring.endWidth = w;

                BuildRing(pile.center, pile.radius, segments, _points,
                    field != null ? (System.Func<Vector3, float>)field.GroundHeightAt : null, groundOffset);
                ring.positionCount = _points.Count;
                for (int p = 0; p < _points.Count; p++) ring.SetPosition(p, _points[p]);
                ring.enabled = true;
            }
            VisibleRings = i;
            HideFrom(i);
        }

        void HideFrom(int index)
        {
            for (int i = index; i < _rings.Count; i++)
                if (_rings[i] != null) _rings[i].enabled = false;
            if (index == 0) VisibleRings = 0;
        }

        LineRenderer GetRing(int index)
        {
            while (_rings.Count <= index)
            {
                var go = new GameObject("PileRing");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.loop = true;
                lr.useWorldSpace = true;
                lr.numCornerVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.alignment = LineAlignment.View;
                lr.sharedMaterial = GetMaterial();
                _rings.Add(lr);
            }
            return _rings[index];
        }

        Material GetMaterial()
        {
            if (_material != null) return _material;
            // Sprites/Default is always included in builds and honours vertex colours.
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            _material = new Material(shader) { name = "PileRing (runtime)" };
            return _material;
        }

        // ------------------------------------------------------------ pure helpers (tested)

        /// <summary>
        /// Index of the pile the player would bag, or -1. Mirrors BagInteractor without physics: the
        /// view ray meets the ground (taken as the pile's height) within <paramref name="reach"/>, and
        /// the pile whose edge is closest to that point, within 0.6 m, wins.
        /// </summary>
        public static int FindReadyPile(IReadOnlyList<LeafPile> piles, Vector3 eye, Vector3 forward, float reach)
        {
            if (piles == null || forward.sqrMagnitude < 1e-6f) return -1;
            forward.Normalize();
            if (forward.y > -0.01f) return -1; // looking level or up: the ray never reaches the ground

            int best = -1;
            float bestEdge = float.MaxValue;
            for (int i = 0; i < piles.Count; i++)
            {
                var p = piles[i];
                float t = (p.center.y - eye.y) / forward.y;
                if (t < 0f || t > reach) continue;
                Vector3 hit = eye + forward * t;
                Vector3 d = p.center - hit;
                d.y = 0f;
                float edge = d.magnitude - p.radius;
                if (edge <= 0.6f && edge < bestEdge)
                {
                    bestEdge = edge;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Width multiplier for the ready ring: 1..1.35, smooth pulse.</summary>
        public static float PulseScale(float time, float speed) =>
            1f + 0.35f * (0.5f + 0.5f * Mathf.Sin(time * speed * Mathf.PI * 2f));

        /// <summary>Fills <paramref name="points"/> with a ground-hugging circle.</summary>
        public static void BuildRing(Vector3 center, float radius, int segments, List<Vector3> points,
            System.Func<Vector3, float> groundHeight = null, float offset = 0.04f)
        {
            points.Clear();
            segments = Mathf.Max(3, segments);
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var p = new Vector3(center.x + Mathf.Cos(a) * radius, center.y, center.z + Mathf.Sin(a) * radius);
                p.y = (groundHeight != null ? groundHeight(p) : center.y) + offset;
                points.Add(p);
            }
        }
    }
}

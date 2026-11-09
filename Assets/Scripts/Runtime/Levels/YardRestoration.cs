using System;
using System.Collections.Generic;
using FallenYard.Core;
using UnityEngine;

namespace FallenYard.Levels
{
    /// <summary>
    /// The visible payoff of the compost bin: as <see cref="GameEvents.YardRestorationChanged"/>
    /// rises, the grass eases from its tired autumn colour to a fresh green and hidden flowers
    /// grow back one by one. Shaders can also read the global <c>_FallenYardRestoration</c>
    /// float the compost bin sets (WP10 builds the fancy transition on top of this hook).
    /// </summary>
    public class YardRestoration : MonoBehaviour
    {
        [Serializable]
        public class RegrowthItem
        {
            public GameObject target;
            [Tooltip("Restoration (0..1) at which this appears.")]
            [Range(0f, 1f)] public float showAt = 0.5f;
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("Renderers tinted from Grass Color to Restored Grass Color.")]
        public Renderer[] grassRenderers = Array.Empty<Renderer>();
        public Color grassColor = new Color(0.42f, 0.55f, 0.27f);
        public Color restoredGrassColor = new Color(0.36f, 0.68f, 0.30f);
        [Tooltip("Things that appear as the yard is restored.")]
        public List<RegrowthItem> regrowth = new List<RegrowthItem>();
        [Tooltip("How fast the shown restoration catches up with the real value (per second). 0 = instantly.")]
        [Min(0f)] public float easeSpeed = 0.6f;

        /// <summary>Restoration the yard is heading to, 0..1.</summary>
        public float Target { get; private set; }
        /// <summary>Restoration currently shown, 0..1. Eases towards <see cref="Target"/>.</summary>
        public float Shown { get; private set; }

        /// <summary>Raised with the shown value whenever it changes, for VFX or audio that wants to follow it.</summary>
        public event Action<float> ShownChanged;

        MaterialPropertyBlock _block;

        bool _ticked;

        void OnEnable() => GameEvents.YardRestorationChanged += OnRestorationChanged;
        void OnDisable() => GameEvents.YardRestorationChanged -= OnRestorationChanged;

        // A value that arrives before the first frame is the save being loaded: show it at once.
        void OnRestorationChanged(float restoration)
        {
            if (_ticked) SetTarget(restoration);
            else ApplyImmediately(restoration);
        }

        void Update() => Tick(Time.deltaTime);

        public void SetTarget(float restoration) => Target = Mathf.Clamp01(float.IsNaN(restoration) ? 0f : restoration);

        /// <summary>Moves the shown restoration towards the target. Public for tests.</summary>
        public void Tick(float dt)
        {
            _ticked = true;
            if (Mathf.Approximately(Shown, Target)) return;
            float next = easeSpeed <= 0f ? Target : Mathf.MoveTowards(Shown, Target, easeSpeed * Mathf.Max(0f, dt));
            Apply(next);
        }

        /// <summary>Jumps straight to <paramref name="restoration"/> (loading a save, building the scene).</summary>
        public void ApplyImmediately(float restoration)
        {
            SetTarget(restoration);
            Apply(Target);
        }

        public static Color GrassColorAt(Color tired, Color restored, float restoration) =>
            Color.Lerp(tired, restored, Mathf.Clamp01(restoration));

        public static bool IsRegrown(RegrowthItem item, float restoration) =>
            item != null && restoration >= item.showAt - 0.0001f && restoration > 0f;

        void Apply(float shown)
        {
            Shown = Mathf.Clamp01(shown);
            var color = GrassColorAt(grassColor, restoredGrassColor, Shown);
            _block ??= new MaterialPropertyBlock();
            foreach (var r in grassRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _block.SetColor(ColorId, color);
                r.SetPropertyBlock(_block);
            }
            foreach (var item in regrowth)
                if (item?.target != null)
                {
                    bool show = IsRegrown(item, Shown);
                    if (item.target.activeSelf != show) item.target.SetActive(show);
                }
            ShownChanged?.Invoke(Shown);
        }
    }
}

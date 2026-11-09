using UnityEngine;
using UnityEngine.Rendering;

namespace FallenYard.VFX
{
    /// <summary>
    /// Builds the game's particle systems and materials in code, so effects need no
    /// hand-made prefabs. Systems are emit-only: emission is off and scripts call
    /// <see cref="ParticleSystem.Emit(ParticleSystem.EmitParams, int)"/> with exact positions.
    /// </summary>
    public static class ParticleFactory
    {
        public const string UnlitShader = "Universal Render Pipeline/Particles/Unlit";
        public const string SimpleLitShader = "Universal Render Pipeline/Particles/Simple Lit";

        static Texture2D _softDot;

        /// <summary>An emit-only, world-space system parented under <paramref name="parent"/>.</summary>
        public static ParticleSystem Create(string name, Transform parent, int maxParticles, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(1, maxParticles);
            main.startSpeed = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortMode = ParticleSystemSortMode.None;
            return ps;
        }

        /// <summary>Fades particles in quickly and out slowly.</summary>
        public static void FadeOverLife(ParticleSystem ps, float peakAlpha = 1f)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.12f), new GradientAlphaKey(peakAlpha * 0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        public static void GrowOverLife(ParticleSystem ps, float from, float to)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, from, 1f, to));
        }

        /// <summary>Cheap curl-noise flutter (low quality, 2D-ish) for leaves and dust.</summary>
        public static void Flutter(ParticleSystem ps, float strength, float frequency)
        {
            var noise = ps.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = strength;
            noise.frequency = frequency;
            noise.scrollSpeed = 0.3f;
            noise.damping = true;
        }

        /// <summary>Soft round transparent material for dust and air streaks.</summary>
        public static Material CreateDustMaterial()
        {
            var shader = Shader.Find(UnlitShader) ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
            var m = new Material(shader) { name = "VFX_Dust (runtime)" };
            SetTexture(m, SoftDot());
            MakeTransparent(m);
            return m;
        }

        /// <summary>Double-sided lit material for leaf particles. Colour comes from the particle (vertex colour).</summary>
        public static Material CreateLeafMaterial()
        {
            var shader = Shader.Find(SimpleLitShader) ?? Shader.Find(UnlitShader) ?? Shader.Find("Sprites/Default");
            var m = new Material(shader) { name = "VFX_Leaf (runtime)", enableInstancing = true };
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)CullMode.Off);
            return m;
        }

        public static void SetTexture(Material m, Texture tex)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        }

        /// <summary>Switches a URP particle material to alpha-blended transparent.</summary>
        public static void MakeTransparent(Material m)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>A 64x64 soft white dot, made once.</summary>
        public static Texture2D SoftDot()
        {
            if (_softDot != null) return _softDot;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "VFX_SoftDot", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels32(SoftDotPixels(size));
            tex.Apply(false, true);
            _softDot = tex;
            return tex;
        }

        /// <summary>White pixels whose alpha falls off smoothly from the centre to the edge.</summary>
        public static Color32[] SoftDotPixels(int size)
        {
            size = Mathf.Max(2, size);
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            return px;
        }
    }
}

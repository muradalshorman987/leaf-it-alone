using UnityEngine;
using UnityEngine.Rendering;

namespace FallenYard.VFX
{
    /// <summary>
    /// Time-of-day light rig: turns the <see cref="DayCycle"/> hour and the current
    /// weather into the sun/moon light, trilight ambient and fog, using a
    /// <see cref="LightingPreset"/>. Nothing here touches the leaves, so it has no
    /// cost against the leaf budget beyond one directional light.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class LightingRig : MonoBehaviour
    {
        public static LightingRig Instance { get; private set; }

        [Tooltip("Directional light to drive. RenderSettings.sun or the first directional light if empty.")]
        public Light sun;
        [Tooltip("Colours over the day. A built-in autumn preset is used if empty.")]
        public LightingPreset preset;
        public DayCycleSettings day = new DayCycleSettings();
        [Tooltip("Weather to read. WeatherSystem.Instance if empty; calm if there is none.")]
        public WeatherSystem weather;
        public bool controlAmbient = true;
        public bool controlFog = true;

        public DayCycle Cycle => _cycle ??= new DayCycle(day);
        public LightingState Current { get; private set; }
        public float Hour => Cycle.Hour;
        public float Daylight01 => Cycle.Daylight01;

        DayCycle _cycle;
        LightingPreset _ownedPreset;

        void Awake()
        {
            Instance = this;
            if (sun == null) sun = FindSun();
            if (preset == null) preset = _ownedPreset = LightingPreset.CreateDefault();
            Apply();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_ownedPreset != null)
            {
                if (Application.isPlaying) Destroy(_ownedPreset);
                else DestroyImmediate(_ownedPreset);
            }
        }

        void Update() => Tick(Time.deltaTime);

        /// <summary>Advances the clock and applies the look. Public for tests and cutscenes.</summary>
        public void Tick(float dt)
        {
            Cycle.Tick(dt);
            Apply();
        }

        public void SetHour(float hour)
        {
            Cycle.SetHour(hour);
            Apply();
        }

        public void Apply()
        {
            if (preset == null) preset = _ownedPreset = LightingPreset.CreateDefault();
            var w = weather != null ? weather : WeatherSystem.Instance;
            var state = preset.Evaluate(Cycle.Hour, w != null ? w.Current : WeatherSample.Calm);
            Current = state;

            if (sun != null)
            {
                sun.transform.rotation = Cycle.LightRotation;
                sun.color = state.sunColor;
                sun.intensity = state.sunIntensity;
                sun.shadowStrength = state.shadowStrength;
            }
            if (controlAmbient)
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = state.skyAmbient;
                RenderSettings.ambientEquatorColor = state.equatorAmbient;
                RenderSettings.ambientGroundColor = state.groundAmbient;
            }
            if (controlFog)
            {
                RenderSettings.fog = state.fogDensity > 0f;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = state.fogColor;
                RenderSettings.fogDensity = state.fogDensity;
            }
        }

        static Light FindSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) return l;
            return null;
        }
    }
}

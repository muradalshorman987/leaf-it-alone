using FallenYard.Core;
using FallenYard.Leaves;
using UnityEngine;

namespace FallenYard.VFX
{
    /// <summary>
    /// Runs the weather and ties it to the wind: each weather kind scales the
    /// <see cref="WindSystem"/>'s breeze and gusts, and every <see cref="GameEvents.WindGust"/>
    /// gives a short "flash" (a cloud racing past, a swirl of leaves) that lighting,
    /// post and particles read from <see cref="Current"/>.
    /// Raises <see cref="GameEvents.WeatherChanged"/> when a new weather starts.
    /// </summary>
    [DefaultExecutionOrder(-50)] // before WindSystem reads its settings and before the lighting rig
    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Instance { get; private set; }

        public WeatherSettings settings = new WeatherSettings();
        public int seed = 4242;
        [Tooltip("Wind to scale. Found in the scene if empty.")]
        public WindSystem wind;
        [Tooltip("Let weather change breeze and gust strength/frequency (gameplay). Off = looks only.")]
        public bool affectWind = true;

        public WeatherModel Model => _model ??= new WeatherModel(settings, seed);
        public WeatherSample Current { get; private set; } = WeatherSample.Calm;
        public WeatherKind Kind => Model.Current;

        WeatherModel _model;
        GustSettings _windSettings;
        float _baseBreeze;
        Vector2 _baseInterval, _baseGust;

        void Awake()
        {
            Instance = this;
            if (wind == null) wind = FindFirstObjectByType<WindSystem>();
            CaptureWind();
            Current = Model.Sample();
        }

        void OnEnable() => GameEvents.WindGust += OnGust;

        void OnDisable()
        {
            GameEvents.WindGust -= OnGust;
            RestoreWind();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start() => GameEvents.RaiseWeatherChanged(WeatherModel.Id(Model.Current));

        void Update() => Tick(Time.deltaTime);

        /// <summary>Advances the weather by dt. Public so tests and cutscenes can drive it.</summary>
        public void Tick(float dt)
        {
            if (Model.Tick(dt)) GameEvents.RaiseWeatherChanged(WeatherModel.Id(Model.Current));
            Current = Model.Sample();
            ApplyWind();
        }

        /// <summary>Changes the weather now (tutorial, debug, yard intro).</summary>
        public void SetWeather(WeatherKind kind, bool immediate = false)
        {
            Model.SetWeather(kind, immediate);
            GameEvents.RaiseWeatherChanged(WeatherModel.Id(kind));
            Current = Model.Sample();
            ApplyWind();
        }

        [ContextMenu("Next weather: Blustery")]
        void DebugBlustery() => SetWeather(WeatherKind.Blustery);

        [ContextMenu("Next weather: Clear")]
        void DebugClear() => SetWeather(WeatherKind.Clear);

        void OnGust(Vector3 direction, float strength) => Model.OnGust(strength);

        void CaptureWind()
        {
            if (wind == null || wind.settings == null) return;
            _windSettings = wind.settings;
            _baseBreeze = _windSettings.breezeStrength;
            _baseInterval = _windSettings.gustInterval;
            _baseGust = _windSettings.gustStrength;
        }

        void ApplyWind()
        {
            if (!affectWind) return;
            if (_windSettings == null) CaptureWind();
            if (_windSettings == null) return;
            // GustScheduler reads these live: breeze changes now, gust timing from the next gust.
            _windSettings.breezeStrength = _baseBreeze * Current.windMultiplier;
            _windSettings.gustInterval = _baseInterval * Current.gustIntervalMultiplier;
            _windSettings.gustStrength = _baseGust * Current.gustStrengthMultiplier;
        }

        void RestoreWind()
        {
            if (_windSettings == null) return;
            _windSettings.breezeStrength = _baseBreeze;
            _windSettings.gustInterval = _baseInterval;
            _windSettings.gustStrength = _baseGust;
        }
    }
}

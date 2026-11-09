using FallenYard.Core;
using FallenYard.Progression;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FallenYard.VFX
{
    /// <summary>
    /// Drives URP post-processing from the game: per-yard autumn colour grading that
    /// warms up as the yard is cleaned and restored (YardCleanChanged /
    /// YardRestorationChanged), a glow when the yard is completed, and weather and
    /// night layered on top. Works on a global Volume it owns (priority above the
    /// project's DefaultVolumeProfile, which keeps the baseline tonemapping and bloom).
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public class PostProcessDriver : MonoBehaviour
    {
        [Tooltip("Optional profile to start from (copied at runtime, never modified). Built in code if empty.")]
        public VolumeProfile template;
        [Tooltip("Per-yard messy/restored grades. Built-in defaults if empty.")]
        public YardGradeTable grades;
        [Tooltip("Yard to grade. YardProgress.yardId if empty.")]
        public string yardId;
        public float volumePriority = 10f;
        [Tooltip("Turn on post-processing on every camera at start (URP cameras default to off).")]
        public bool enableOnCameras = true;

        public RestorationTransition Transition { get; } = new RestorationTransition();
        public ColorGrade CurrentGrade { get; private set; }
        public Volume Volume => _volume;

        Volume _volume;
        VolumeProfile _profile;
        ColorAdjustments _color;
        WhiteBalance _white;
        Vignette _vignette;
        Bloom _bloom;
        YardGradeTable _ownedGrades;

        void Awake()
        {
            if (grades == null) grades = _ownedGrades = ScriptableObject.CreateInstance<YardGradeTable>();
            if (string.IsNullOrEmpty(yardId))
            {
                var progress = FindFirstObjectByType<YardProgress>();
                if (progress != null) yardId = progress.yardId;
            }
            BuildVolume();
            Transition.Snap();
            Apply(0f);
        }

        void Start()
        {
            if (!enableOnCameras) return;
            foreach (var cam in Camera.allCameras)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = true;
            }
        }

        void OnEnable()
        {
            GameEvents.YardCleanChanged += Transition.SetClean;
            GameEvents.YardRestorationChanged += Transition.SetRestoration;
            GameEvents.YardCompleted += OnYardCompleted;
        }

        void OnDisable()
        {
            GameEvents.YardCleanChanged -= Transition.SetClean;
            GameEvents.YardRestorationChanged -= Transition.SetRestoration;
            GameEvents.YardCompleted -= OnYardCompleted;
        }

        void OnDestroy()
        {
            if (_profile != null) Destroy(_profile);
            if (_ownedGrades != null) Destroy(_ownedGrades);
        }

        void OnYardCompleted(string id) => Transition.StartCelebration();

        void Update() => Apply(Time.deltaTime);

        void BuildVolume()
        {
            var go = new GameObject("PostProcess Volume (runtime)");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = volumePriority;

            _profile = template != null ? Instantiate(template) : ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "FallenYard Post (runtime)";
            _volume.sharedProfile = _profile;

            _color = Ensure<ColorAdjustments>();
            _white = Ensure<WhiteBalance>();
            _vignette = Ensure<Vignette>();
            _bloom = Ensure<Bloom>();
            _color.postExposure.overrideState = true;
            _color.contrast.overrideState = true;
            _color.saturation.overrideState = true;
            _color.colorFilter.overrideState = true;
            _white.temperature.overrideState = true;
            _white.tint.overrideState = true;
            _vignette.intensity.overrideState = true;
            _bloom.intensity.overrideState = true;
        }

        T Ensure<T>() where T : VolumeComponent
        {
            if (!_profile.TryGet(out T component)) component = _profile.Add<T>(false);
            component.active = true;
            return component;
        }

        void Apply(float dt)
        {
            Transition.Tick(dt);
            var weather = WeatherSystem.Instance != null ? WeatherSystem.Instance.Current : WeatherSample.Calm;
            float daylight = LightingRig.Instance != null ? LightingRig.Instance.Daylight01 : 1f;
            var grade = ColorGrade.Compose(grades.Evaluate(yardId, Transition.Value), weather, daylight, Transition.Celebrate);
            CurrentGrade = grade;

            _color.postExposure.value = grade.postExposure;
            _color.contrast.value = grade.contrast;
            _color.saturation.value = grade.saturation;
            _color.colorFilter.value = grade.colorFilter;
            _white.temperature.value = grade.temperature;
            _white.tint.value = grade.tint;
            _vignette.intensity.value = grade.vignette;
            _bloom.intensity.value = grade.bloom;
        }
    }
}

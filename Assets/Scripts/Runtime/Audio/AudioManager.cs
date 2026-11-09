using System.Collections;
using System.Threading.Tasks;
using FallenYard.Core;
using FallenYard.Settings;
using UnityEngine;
using UnityEngine.Audio;

namespace FallenYard.Audio
{
    /// <summary>
    /// Plays every sound in Fallen Yard by listening to <see cref="GameEvents"/> and
    /// <see cref="SettingsManager.Changed"/>; nothing else needs a reference to it.
    ///
    /// - Tool loops: rake rustle, blower, vacuum and mulcher engines follow ToolThrottle.
    /// - One-shots: equip, bag rustle, coin chime (rising on streaks), purchase, upgrade, compost, restoration.
    /// - Ambience: a looping wind bed, gust swells from WindGust, occasional birds, both shaped by WeatherChanged.
    /// - Music: title and yard loops picked by GamePhase, ducked and muffled under menus.
    ///
    /// Every clip is synthesized at runtime (<see cref="ProceduralSounds"/>) unless an
    /// <see cref="AudioLibrary"/> overrides it. One is created automatically in any scene with
    /// an AudioListener; place one yourself to tune it.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        /// <summary>Set false (e.g. in tests) to stop the manager creating itself at scene load.</summary>
        public static bool AutoCreate = true;

        [Tooltip("Optional clip overrides. Empty = all procedural sounds.")]
        public AudioLibrary library;

        [Header("Mixer (optional)")]
        [Tooltip("If set, channel sliders drive this mixer's exposed parameters instead of each AudioSource.")]
        public AudioMixer mixer;
        public AudioMixerGroup musicGroup;
        public AudioMixerGroup sfxGroup;
        public AudioMixerGroup ambienceGroup;
        [Tooltip("Exposed mixer parameters (dB).")]
        public string musicVolumeParam = "MusicVolume";
        public string sfxVolumeParam = "SfxVolume";
        public string ambienceVolumeParam = "AmbienceVolume";

        [Header("Levels (before player settings)")]
        [Range(0f, 1f)] public float musicLevel = 0.45f;
        [Range(0f, 1f)] public float toolLevel = 0.6f;
        [Range(0f, 1f)] public float sfxLevel = 0.85f;
        [Range(0f, 1f)] public float windBedLevel = 0.3f;
        [Range(0f, 1f)] public float gustLevel = 0.7f;
        [Range(0f, 1f)] public float birdLevel = 0.28f;

        [Header("Music")]
        [Tooltip("Seconds to cross-fade between tracks.")]
        [Min(0.01f)] public float musicFadeSeconds = 2.5f;
        [Tooltip("How long the yard-complete stinger ducks the music.")]
        [Min(0f)] public float stingerDuckSeconds = 3.5f;

        [Header("Ambience")]
        [Tooltip("Seconds between bird calls (min, max).")]
        public Vector2 birdDelay = new Vector2(4f, 12f);
        [Tooltip("Seconds birds stay quiet after a wind gust.")]
        [Min(0f)] public float birdQuietAfterGust = 6f;
        [Tooltip("Gust strength (m/s) that plays the gust at full volume.")]
        [Min(0.1f)] public float fullGustStrength = 5.5f;
        public int seed = 2024;

        [Header("Tools")]
        public ToolLoopModel.Tuning toolTuning = new ToolLoopModel.Tuning();
        [Tooltip("Leaves in one bag that give the loudest bag rustle.")]
        [Min(1)] public int loudBagLeaves = 300;

        [Header("Voices")]
        [Tooltip("Simultaneous one-shot sounds. The oldest is cut when all are busy.")]
        [Range(2, 32)] public int oneShotVoices = 12;
        [Tooltip("Render music and ambience on a worker thread at startup instead of on first use.")]
        public bool prewarm = true;

        public AudioBuses Buses { get; } = new AudioBuses();
        public MusicDirector Music { get; } = new MusicDirector();
        public RewardPitch Rewards { get; } = new RewardPitch();
        public ToolLoopModel ToolLoop { get; private set; }
        public AmbienceScheduler Birds { get; private set; }

        /// <summary>The last cue started, for tests and debugging.</summary>
        public SoundCue? LastCue { get; private set; }
        public int CuesPlayed { get; private set; }
        public MusicTrack ActiveTrack => _activeTrack;
        public AudioSource ToolSource => _toolSource;
        public AudioSource WindBedSource => _windBed;
        public AudioSource ActiveMusicSource => _aActive ? _musicA : _musicB;

        AudioSource _musicA, _musicB, _windBed, _toolSource;
        AudioLowPassFilter _lowPassA, _lowPassB;
        AudioSource[] _voices;
        int _nextVoice;
        SynthRandom _rng;

        MusicTrack _activeTrack = MusicTrack.None;
        bool _aActive = true;
        float _fadeA, _fadeB, _duck = 1f, _cutoff = MusicDirector.OpenCutoff;
        float _clock, _gustBoost, _weatherWind = 1f, _weatherWindTarget = 1f;
        float _lastThud = -10f, _lastSparkle = -10f;
        int _lastCoins = -1, _lastReadyBatches = -1;
        float _lastCompostFill = -1f, _lastRestoration = -1f;
        bool _built, _subscribed;

        static bool CanPlay => Application.isPlaying;

        // ------------------------------------------------------------------ lifecycle

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null) return;
            if (FindAnyObjectByType<AudioManager>() != null) return;
            // Scenes with nobody listening (test scenes, loaders) don't need audio.
            if (FindAnyObjectByType<AudioListener>() == null) return;
            new GameObject("AudioManager").AddComponent<AudioManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(gameObject);
                return;
            }
            Instance = this;
            if (Application.isPlaying && transform.parent == null) DontDestroyOnLoad(gameObject);
            Build();
        }

        void OnEnable()
        {
            if (Instance == this) Subscribe();
        }

        void OnDisable() => Unsubscribe();

        void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            if (prewarm && CanPlay) StartCoroutine(Prewarm());
        }

        void Update() => Tick(Time.unscaledDeltaTime);

        /// <summary>Creates the sources and models. Safe to call twice. Public for tests.</summary>
        public void Build()
        {
            if (_built) return;
            _built = true;
            _rng = new SynthRandom(seed);
            ToolLoop = new ToolLoopModel(toolTuning);
            Birds = new AmbienceScheduler(seed + 1, birdDelay.x, birdDelay.y);
            Buses.MixerDrivesChannels = mixer != null;

            _musicA = MakeSource("Music A", musicGroup, true);
            _musicB = MakeSource("Music B", musicGroup, true);
            _lowPassA = _musicA.gameObject.AddComponent<AudioLowPassFilter>();
            _lowPassB = _musicB.gameObject.AddComponent<AudioLowPassFilter>();
            _lowPassA.cutoffFrequency = _lowPassB.cutoffFrequency = MusicDirector.OpenCutoff;
            _windBed = MakeSource("Wind Bed", ambienceGroup, true);
            _toolSource = MakeSource("Tool Loop", sfxGroup, true);
            _voices = new AudioSource[Mathf.Max(2, oneShotVoices)];
            for (int i = 0; i < _voices.Length; i++) _voices[i] = MakeSource("Voice " + i, sfxGroup, false);

            var settings = SettingsManager.Instance != null ? SettingsManager.Instance.Current : null;
            if (settings != null) OnSettingsChanged(settings);
        }

        AudioSource MakeSource(string sourceName, AudioMixerGroup group, bool loop)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.outputAudioMixerGroup = group;
            return s;
        }

        void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            GameEvents.ToolEquipped += OnToolEquipped;
            GameEvents.ToolThrottle += OnToolThrottle;
            GameEvents.PileBagged += OnPileBagged;
            GameEvents.CoinsChanged += OnCoinsChanged;
            GameEvents.ToolUpgraded += OnToolUpgraded;
            GameEvents.CompostChanged += OnCompostChanged;
            GameEvents.YardRestorationChanged += OnYardRestorationChanged;
            GameEvents.WindGust += OnWindGust;
            GameEvents.YardCompleted += OnYardCompleted;
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.WeatherChanged += OnWeatherChanged;
            SettingsManager.Changed += OnSettingsChanged;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            GameEvents.ToolEquipped -= OnToolEquipped;
            GameEvents.ToolThrottle -= OnToolThrottle;
            GameEvents.PileBagged -= OnPileBagged;
            GameEvents.CoinsChanged -= OnCoinsChanged;
            GameEvents.ToolUpgraded -= OnToolUpgraded;
            GameEvents.CompostChanged -= OnCompostChanged;
            GameEvents.YardRestorationChanged -= OnYardRestorationChanged;
            GameEvents.WindGust -= OnWindGust;
            GameEvents.YardCompleted -= OnYardCompleted;
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.WeatherChanged -= OnWeatherChanged;
            SettingsManager.Changed -= OnSettingsChanged;
        }

        /// <summary>Renders the long clips on a worker thread so the first gust or track change doesn't hitch.</summary>
        IEnumerator Prewarm()
        {
            SoundCue[] cues =
            {
                SoundCue.WindBed, SoundCue.MusicYard, SoundCue.MusicTitle, SoundCue.RakeRustle,
                SoundCue.BlowerLoop, SoundCue.VacuumLoop, SoundCue.MulcherLoop, SoundCue.WindGust,
            };
            bool threads = Application.platform != RuntimePlatform.WebGLPlayer;
            foreach (var cue in cues)
            {
                if (library != null && library.Find(cue) != null) continue;
                for (int v = 0; v < ProceduralSounds.VariantCount(cue); v++)
                {
                    if (GeneratedClips.IsCached(cue, v)) continue;
                    if (threads)
                    {
                        var c = cue;
                        int variant = v;
                        var task = Task.Run(() => ProceduralSounds.Generate(c, variant));
                        while (!task.IsCompleted) yield return null;
                        if (task.Status == TaskStatus.RanToCompletion) GeneratedClips.Store(cue, v, task.Result);
                    }
                    else
                    {
                        GeneratedClips.Get(cue, v);
                        yield return null;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ event handlers (public for tests)

        public void OnToolEquipped(string toolId)
        {
            if (!_built) Build();
            if (ToolLoop.Equip(toolId))
            {
                var cue = ToolLoop.Cue;
                _toolSource.Stop();
                _toolSource.clip = cue.HasValue ? Clip(cue.Value, 0) : null;
            }
            PlayCue(SoundCue.ToolEquip, 0.6f, _rng.Range(0.95f, 1.05f));
        }

        public void OnToolThrottle(float throttle)
        {
            if (!_built) Build();
            ToolLoop.SetThrottle(throttle);
        }

        public void OnPileBagged(int leaves, int coinsEarned)
        {
            float size = Mathf.Clamp01(leaves / (float)Mathf.Max(1, loudBagLeaves));
            PlayCue(SoundCue.BagRustle, Mathf.Lerp(0.45f, 1f, size), _rng.Range(0.94f, 1.06f) * Mathf.Lerp(1.08f, 0.94f, size));
            if (coinsEarned > 0)
                PlayCue(SoundCue.CoinChime, 0.7f, Rewards.Next(_clock), 0f, AudioChannel.Sfx, -1, 0.12f);
        }

        public void OnCoinsChanged(int total)
        {
            // Earning coins already chimes from PileBagged; spending them is a purchase.
            if (_lastCoins >= 0 && total < _lastCoins) PlayCue(SoundCue.Purchase, 0.75f);
            _lastCoins = total;
        }

        public void OnToolUpgraded(string toolId, int level) => PlayCue(SoundCue.Upgrade, 0.8f, 1f + 0.02f * Mathf.Clamp(level, 0, 10));

        public void OnCompostChanged(float fill, int readyBatches)
        {
            if (_lastReadyBatches >= 0 && readyBatches > _lastReadyBatches)
                PlayCue(SoundCue.CompostReady, 0.75f);
            else if (_lastCompostFill >= 0f && fill > _lastCompostFill + 1e-4f && _clock - _lastThud > 0.25f)
            {
                _lastThud = _clock;
                PlayCue(SoundCue.CompostThud, 0.8f, _rng.Range(0.92f, 1.08f));
            }
            _lastReadyBatches = readyBatches;
            _lastCompostFill = fill;
        }

        public void OnYardRestorationChanged(float restoration)
        {
            if (_lastRestoration >= 0f && restoration > _lastRestoration + 1e-3f && _clock - _lastSparkle > 0.5f)
            {
                _lastSparkle = _clock;
                PlayCue(SoundCue.RestoreSparkle, 0.65f, Mathf.Lerp(0.95f, 1.12f, restoration));
            }
            _lastRestoration = restoration;
        }

        public void OnWindGust(Vector3 direction, float strength)
        {
            if (!_built) Build();
            float level = Mathf.Clamp(strength / fullGustStrength, 0.35f, 1f);
            float pan = 0f;
            var cam = Camera.main;
            if (cam != null && direction.sqrMagnitude > 1e-6f)
                pan = Mathf.Clamp(Vector3.Dot(direction.normalized, cam.transform.right), -1f, 1f) * 0.6f;
            PlayCue(SoundCue.WindGust, gustLevel * level, _rng.Range(0.92f, 1.06f), pan, AudioChannel.Ambience);
            _gustBoost = Mathf.Max(_gustBoost, level);
            Birds.Suppress(birdQuietAfterGust);
        }

        public void OnYardCompleted(string yardId)
        {
            Music.StartStinger(stingerDuckSeconds);
            PlayCue(SoundCue.YardComplete, 0.9f);
        }

        public void OnStateChanged(GamePhase phase)
        {
            if (!_built) Build();
            Music.SetPhase(phase);
            ToolLoop.Suspended = !Music.GameplayAudible;
            Birds.Enabled = phase != GamePhase.Paused && phase != GamePhase.Shop;
        }

        public void OnWeatherChanged(string weatherId)
        {
            if (!_built) Build();
            WeatherAmbience.For(weatherId, out _weatherWindTarget, out float birdRate);
            Birds.Rate = birdRate;
        }

        /// <summary>Current wind bed multiplier from the weather (glides toward the target).</summary>
        public float WeatherWindGain => _weatherWind;

        public void OnSettingsChanged(GameSettings settings)
        {
            Buses.Apply(settings);
            ApplyMixer();
        }

        void ApplyMixer()
        {
            if (mixer == null) return;
            SetMixerParam(musicVolumeParam, Buses.MixerDecibels(AudioChannel.Music));
            SetMixerParam(sfxVolumeParam, Buses.MixerDecibels(AudioChannel.Sfx));
            SetMixerParam(ambienceVolumeParam, Buses.MixerDecibels(AudioChannel.Ambience));
        }

        void SetMixerParam(string param, float db)
        {
            if (!string.IsNullOrEmpty(param) && !mixer.SetFloat(param, db))
                Debug.LogWarning($"[FallenYard] Audio mixer has no exposed parameter '{param}'.");
        }

        // ------------------------------------------------------------------ playback

        /// <summary>
        /// Plays a one-shot. <paramref name="variant"/> -1 picks a random take.
        /// Returns the voice used, or null if nothing could play.
        /// </summary>
        public AudioSource PlayCue(SoundCue cue, float level = 1f, float pitch = 1f, float pan = 0f,
            AudioChannel channel = AudioChannel.Sfx, int variant = -1, float delay = 0f)
        {
            if (!_built) Build();
            int variants = AudioLibrary.Variants(library, cue);
            if (variant < 0) variant = variants > 1 ? _rng.Range(0, variants) : 0;
            var clip = Clip(cue, variant);
            if (clip == null) return null;

            var src = NextVoice();
            src.Stop();
            src.clip = clip;
            src.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
            src.panStereo = Mathf.Clamp(pan, -1f, 1f);
            src.outputAudioMixerGroup = GroupFor(channel);
            float baseLevel = channel == AudioChannel.Sfx ? sfxLevel : 1f;
            src.volume = Buses.SourceGain(channel, level * baseLevel * GainFor(cue));
            if (CanPlay)
            {
                if (delay > 0f) src.PlayDelayed(delay);
                else src.Play();
            }

            LastCue = cue;
            CuesPlayed++;
            GameEvents.RaiseAudioCuePlayed(cue.ToString());
            return src;
        }

        AudioClip Clip(SoundCue cue, int variant) => AudioLibrary.Resolve(library, cue, variant);

        float GainFor(SoundCue cue) => library != null ? library.GainFor(cue) : 1f;

        AudioMixerGroup GroupFor(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Music: return musicGroup;
                case AudioChannel.Ambience: return ambienceGroup;
                default: return sfxGroup;
            }
        }

        AudioSource NextVoice()
        {
            // Prefer an idle voice; otherwise steal the next one round-robin (the oldest).
            for (int i = 0; i < _voices.Length; i++)
            {
                int idx = (_nextVoice + i) % _voices.Length;
                if (!_voices[idx].isPlaying)
                {
                    _nextVoice = (idx + 1) % _voices.Length;
                    return _voices[idx];
                }
            }
            var v = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            return v;
        }

        // ------------------------------------------------------------------ per frame

        /// <summary>Advances fades, loops and ambience by <paramref name="dt"/> unscaled seconds. Public for tests.</summary>
        public void Tick(float dt)
        {
            if (!_built) Build();
            dt = Mathf.Max(0f, dt);
            _clock += dt;
            Music.Tick(dt);
            ToolLoop.Tick(dt);
            UpdateMusic(dt);
            UpdateToolLoop();
            UpdateAmbience(dt);
        }

        void UpdateMusic(float dt)
        {
            var desired = Music.Track;
            if (desired != _activeTrack)
            {
                _activeTrack = desired;
                _aActive = !_aActive;
                var incoming = _aActive ? _musicA : _musicB;
                var cue = MusicDirector.CueFor(desired);
                incoming.Stop();
                incoming.clip = cue.HasValue ? Clip(cue.Value, 0) : null;
                if (_aActive) _fadeA = 0f; else _fadeB = 0f;
                if (incoming.clip != null && CanPlay) incoming.Play();
            }

            float step = dt / musicFadeSeconds;
            _fadeA = Mathf.MoveTowards(_fadeA, _aActive && _musicA.clip != null ? 1f : 0f, step);
            _fadeB = Mathf.MoveTowards(_fadeB, !_aActive && _musicB.clip != null ? 1f : 0f, step);

            _duck = Mathf.MoveTowards(_duck, Music.Duck, dt * 1.5f);
            Buses.MusicDuck = _duck;
            // Glide the cutoff in log space so the muffle sounds even.
            float k = 1f - Mathf.Exp(-dt * 4f);
            _cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(_cutoff), Mathf.Log(Music.Cutoff), k));
            _lowPassA.cutoffFrequency = _lowPassB.cutoffFrequency = _cutoff;

            ApplyMusicSource(_musicA, _fadeA);
            ApplyMusicSource(_musicB, _fadeB);
        }

        void ApplyMusicSource(AudioSource src, float fade)
        {
            src.volume = Buses.SourceGain(AudioChannel.Music, musicLevel * fade);
            if (fade <= 0f && src.isPlaying) src.Stop();
        }

        void UpdateToolLoop()
        {
            float vol = Buses.SourceGain(AudioChannel.Sfx, toolLevel * ToolLoop.Volume * (ToolLoop.Cue.HasValue ? GainFor(ToolLoop.Cue.Value) : 1f));
            _toolSource.volume = vol;
            _toolSource.pitch = ToolLoop.Pitch;
            if (!CanPlay || _toolSource.clip == null) return;
            bool audible = vol > 0.0005f;
            if (audible && !_toolSource.isPlaying)
            {
                // Start each stroke somewhere new in the loop so repeated strokes don't sound identical.
                _toolSource.timeSamples = _rng.Range(0, Mathf.Max(1, _toolSource.clip.samples));
                _toolSource.Play();
            }
            else if (!audible && _toolSource.isPlaying)
            {
                _toolSource.Pause();
            }
        }

        void UpdateAmbience(float dt)
        {
            _gustBoost = Mathf.MoveTowards(_gustBoost, 0f, dt / 4f);
            _weatherWind = Mathf.MoveTowards(_weatherWind, _weatherWindTarget, dt * 0.1f);
            if (_windBed.clip == null && CanPlay) _windBed.clip = Clip(SoundCue.WindBed, 0);
            _windBed.volume = Buses.SourceGain(AudioChannel.Ambience, windBedLevel * _weatherWind * (1f + 0.8f * _gustBoost));
            if (CanPlay && _windBed.clip != null && !_windBed.isPlaying) _windBed.Play();

            if (Birds.Tick(dt))
            {
                Birds.PickCall(AudioLibrary.Variants(library, SoundCue.Birdsong), out int variant, out float pan, out float pitch);
                PlayCue(SoundCue.Birdsong, birdLevel, pitch, pan, AudioChannel.Ambience, variant);
            }
        }

        [ContextMenu("Play test gust")]
        void TestGust() => OnWindGust(Vector3.right, fullGustStrength);

        [ContextMenu("Play coin chime")]
        void TestCoin() => OnPileBagged(120, 5);
    }
}

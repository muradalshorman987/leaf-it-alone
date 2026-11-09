using FallenYard.Core;
using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>
    /// The yard's compost bin. Every leaf that is bagged, vacuumed or mulched
    /// lands here (it listens to <see cref="GameEvents.PileBagged"/>). Full
    /// batches compost into fertilizer; the player looks at the bin and presses
    /// E to spread it, which restores the yard a step at a time.
    /// Restoration is published as <see cref="GameEvents.YardRestorationChanged"/>
    /// and as the global shader float <c>_FallenYardRestoration</c> for the
    /// grass/flower regrowth shader (WP7/WP10).
    /// Needs a collider so the player's interaction ray can hit it.
    /// </summary>
    public class CompostBin : MonoBehaviour
    {
        public static CompostBin Instance { get; private set; }

        public static readonly int RestorationShaderId = Shader.PropertyToID("_FallenYardRestoration");

        [Header("Rules")]
        [Tooltip("Leaves needed to start one batch of compost.")]
        public int leavesPerBatch = 400;
        [Tooltip("Seconds a full batch takes to turn into fertilizer.")]
        public float compostSeconds = 20f;
        [Tooltip("How much one batch of fertilizer restores the yard (0..1).")]
        [Range(0.01f, 1f)] public float restorePerBatch = 0.2f;

        [Header("Visuals (optional)")]
        [Tooltip("Scaled on Y from 0 to 1 with the bin's fill.")]
        public Transform fillVisual;
        [Tooltip("Shown while fertilizer is ready to spread.")]
        public GameObject readyIndicator;
        [Tooltip("Shown while a batch is composting (steam, flies, a lid ajar...).")]
        public GameObject compostingIndicator;

        public CompostModel Model => _model ??= new CompostModel(leavesPerBatch, compostSeconds, restorePerBatch);

        /// <summary>Hint for the HUD when the player looks at the bin.</summary>
        public string Prompt
        {
            get
            {
                var m = Model;
                if (m.ReadyBatches > 0 && !m.IsFullyRestored)
                    return $"[{GameInput.Label(GameAction.Interact)}] Spread fertilizer ({m.ReadyBatches} ready)";
                if (m.IsComposting)
                    return $"Composting... {Mathf.RoundToInt(m.CompostProgress * 100f)}%";
                if (m.IsFullyRestored)
                    return "The yard is fully restored";
                return $"Compost bin {Mathf.RoundToInt(m.Fill01 * 100f)}% full";
            }
        }

        CompostModel _model;
        float _lastFill = -1f;
        int _lastReady = -1;

        void Awake()
        {
            Instance = this;
            Shader.SetGlobalFloat(RestorationShaderId, Model.Restoration);
            RefreshVisuals();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable() => GameEvents.PileBagged += OnPileBagged;
        void OnDisable() => GameEvents.PileBagged -= OnPileBagged;

        void OnPileBagged(int leaves, int coins) => AddLeaves(leaves);

        void Update() => Tick(Time.deltaTime);

        /// <summary>Advances composting; called from Update (public for tests).</summary>
        public void Tick(float dt)
        {
            Model.Tick(dt);
            Publish();
        }

        public void AddLeaves(int leaves)
        {
            Model.AddLeaves(leaves);
            Publish();
        }

        /// <summary>Spreads one batch of fertilizer if any is ready. Returns true if the yard changed.</summary>
        public bool TrySpread()
        {
            if (!Model.TrySpread()) return false;
            Shader.SetGlobalFloat(RestorationShaderId, Model.Restoration);
            GameEvents.RaiseYardRestorationChanged(Model.Restoration);
            Publish();
            return true;
        }

        /// <summary>
        /// Restores saved bin state (called by the save layer once the yard is known).
        /// Updates the shader, raises YardRestorationChanged and refreshes visuals.
        /// </summary>
        public void ApplySavedState(float restoration, int pendingLeaves, int readyBatches, bool isComposting, float compostProgress)
        {
            Model.RestoreState(restoration, pendingLeaves, readyBatches, isComposting, compostProgress);
            Shader.SetGlobalFloat(RestorationShaderId, Model.Restoration);
            GameEvents.RaiseYardRestorationChanged(Model.Restoration);
            _lastFill = -1f;
            Publish();
        }

        void Publish()
        {
            var m = Model;
            if (Mathf.Abs(m.Fill01 - _lastFill) < 0.001f && m.ReadyBatches == _lastReady) return;
            _lastFill = m.Fill01;
            _lastReady = m.ReadyBatches;
            GameEvents.RaiseCompostChanged(m.Fill01, m.ReadyBatches);
            RefreshVisuals();
        }

        void RefreshVisuals()
        {
            var m = Model;
            if (fillVisual != null)
            {
                var s = fillVisual.localScale;
                s.y = Mathf.Max(0.02f, m.Fill01);
                fillVisual.localScale = s;
            }
            if (readyIndicator != null) readyIndicator.SetActive(m.ReadyBatches > 0);
            if (compostingIndicator != null) compostingIndicator.SetActive(m.IsComposting);
        }
    }
}

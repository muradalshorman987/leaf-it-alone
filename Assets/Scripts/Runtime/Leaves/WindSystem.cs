using FallenYard.Core;
using UnityEngine;

namespace FallenYard.Leaves
{
    /// <summary>
    /// Yard weather: a gentle breeze plus periodic gusts that strip the top off
    /// un-bagged piles, so the player chooses when to bag. Raises
    /// <see cref="GameEvents.WindGust"/> when a gust starts (audio/VFX listen to it)
    /// and feeds the current wind to the <see cref="LeafField"/> every frame.
    /// </summary>
    public class WindSystem : MonoBehaviour
    {
        [Tooltip("Field to blow. LeafField.Instance if empty.")]
        public LeafField field;
        public GustSettings settings = new GustSettings();
        public int seed = 777;

        public Vector3 WindDirection { get; private set; } = Vector3.forward;
        public float WindStrength { get; private set; }
        public bool GustActive => _scheduler != null && _scheduler.GustActive;
        public float SecondsToNextGust => _scheduler == null || _scheduler.GustActive ? 0f : Mathf.Max(0f, _scheduler.NextGustTime - _scheduler.Time);
        public GustScheduler Scheduler => _scheduler ??= new GustScheduler(settings, seed);

        GustScheduler _scheduler;

        void Update() => Tick(Time.deltaTime);

        /// <summary>Advances the wind by dt. Public so tests and cutscenes can drive it.</summary>
        public void Tick(float dt)
        {
            if (Scheduler.Tick(dt)) RaiseGust();
            Apply();
        }

        /// <summary>Starts a gust right now (tutorials, debugging). Null values are randomised.</summary>
        public void TriggerGust(Vector3? direction = null, float? strength = null)
        {
            Vector2? dir = direction.HasValue ? new Vector2(direction.Value.x, direction.Value.z) : (Vector2?)null;
            Scheduler.StartGust(dir, strength);
            RaiseGust();
            Apply();
        }

        [ContextMenu("Trigger gust")]
        void TriggerGustFromMenu() => TriggerGust();

        void RaiseGust()
        {
            Vector2 d = _scheduler.GustDirection;
            GameEvents.RaiseWindGust(new Vector3(d.x, 0f, d.y), _scheduler.GustPeakStrength);
        }

        void Apply()
        {
            WindSample w = _scheduler.Sample();
            WindDirection = w.Direction3;
            WindStrength = w.strength;
            var target = field != null ? field : LeafField.Instance;
            if (target != null) target.SetWind(WindDirection, WindStrength);
        }

        void OnDisable()
        {
            var target = field != null ? field : LeafField.Instance;
            if (target != null) target.SetWind(WindDirection, 0f);
        }
    }
}

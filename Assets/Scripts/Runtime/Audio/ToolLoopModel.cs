using UnityEngine;

namespace FallenYard.Audio
{
    /// <summary>
    /// Turns the equipped tool and its throttle (<see cref="FallenYard.Core.GameEvents.ToolThrottle"/>)
    /// into a loop clip, volume and pitch. Engines idle quietly while held and rev with the
    /// trigger; the rake is silent until you move leaves and rustles louder the faster you sweep.
    /// Pure logic so it can be unit tested.
    /// </summary>
    public sealed class ToolLoopModel
    {
        [System.Serializable]
        public class Tuning
        {
            [Tooltip("Engine volume while equipped but not throttled (0..1).")]
            public float engineIdle = 0.22f;
            [Tooltip("Engine pitch at idle and at full throttle.")]
            public Vector2 enginePitch = new Vector2(0.82f, 1.22f);
            [Tooltip("Rake rustle pitch at a slow and a full-speed stroke.")]
            public Vector2 rustlePitch = new Vector2(0.9f, 1.15f);
            [Tooltip("How fast the sound follows throttle going up, per second.")]
            public float attackRate = 9f;
            [Tooltip("How fast the sound follows throttle going down, per second. Engines spool down slower than they rev.")]
            public float releaseRate = 3.5f;
        }

        public readonly Tuning tuning;

        public ToolLoopModel(Tuning tuning = null) => this.tuning = tuning ?? new Tuning();

        /// <summary>Current loop, or null when the tool makes no continuous sound (e.g. the bag).</summary>
        public SoundCue? Cue { get; private set; }
        public string ToolId { get; private set; }
        public float TargetThrottle { get; private set; }
        /// <summary>Throttle after attack/release smoothing; this is what volume and pitch follow.</summary>
        public float SmoothedThrottle { get; private set; }
        /// <summary>When true (paused, menus), the loop fades out entirely, idle included.</summary>
        public bool Suspended { get; set; }

        public bool IsEngine => Cue.HasValue && Cue.Value != SoundCue.RakeRustle;

        /// <summary>Which loop a tool id plays. Unknown ids are silent.</summary>
        public static SoundCue? CueForTool(string toolId)
        {
            switch (toolId)
            {
                case "rake": return SoundCue.RakeRustle;
                case "blower": return SoundCue.BlowerLoop;
                case "vacuum": return SoundCue.VacuumLoop;
                case "mulcher": return SoundCue.MulcherLoop;
                default: return null;
            }
        }

        /// <summary>Equips a tool. Returns true if the loop clip changed.</summary>
        public bool Equip(string toolId)
        {
            ToolId = toolId;
            var next = CueForTool(toolId);
            bool changed = !System.Nullable.Equals(next, Cue);
            Cue = next;
            TargetThrottle = 0f;
            SmoothedThrottle = 0f;
            return changed;
        }

        public void SetThrottle(float throttle) => TargetThrottle = Mathf.Clamp01(throttle);

        public void Tick(float dt)
        {
            float target = Suspended ? 0f : TargetThrottle;
            float rate = target > SmoothedThrottle ? tuning.attackRate : tuning.releaseRate;
            float k = 1f - Mathf.Exp(-rate * Mathf.Max(0f, dt));
            SmoothedThrottle = Mathf.Lerp(SmoothedThrottle, target, k);
            if (Mathf.Abs(SmoothedThrottle - target) < 1e-3f) SmoothedThrottle = target;
        }

        /// <summary>0..1 loop volume for the current state.</summary>
        public float Volume
        {
            get
            {
                if (!Cue.HasValue || (Suspended && SmoothedThrottle <= 0f)) return 0f;
                float t = SmoothedThrottle;
                if (IsEngine)
                    return Suspended ? t : Mathf.Lerp(tuning.engineIdle, 1f, t);
                // Rustle: quiet strokes stay quiet; a little curve so light raking isn't harsh.
                return t * t * (3f - 2f * t);
            }
        }

        public float Pitch
        {
            get
            {
                Vector2 range = IsEngine ? tuning.enginePitch : tuning.rustlePitch;
                return Mathf.Lerp(range.x, range.y, SmoothedThrottle);
            }
        }
    }
}

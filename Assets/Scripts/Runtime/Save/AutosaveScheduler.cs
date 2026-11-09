using System;

namespace FallenYard.Save
{
    /// <summary>
    /// Decides when to autosave. Plain C# with the clock passed in, so it is easy to test.
    ///
    /// Two triggers:
    /// - periodic: every <see cref="Interval"/> seconds since the last save (0 = off);
    /// - requested: gameplay milestones call <see cref="Request"/>; the save happens at most
    ///   once per <see cref="MinGap"/> seconds, so a burst of events causes one write.
    /// </summary>
    public class AutosaveScheduler
    {
        public float Interval { get; set; }
        public float MinGap { get; set; }

        public bool HasPendingRequest { get; private set; }
        public string PendingReason { get; private set; }
        public double LastSaveTime { get; private set; }

        bool _urgent;

        public AutosaveScheduler(float interval, float minGap, double now = 0)
        {
            Interval = Math.Max(0f, interval);
            MinGap = Math.Max(0f, minGap);
            LastSaveTime = now;
        }

        /// <summary>Asks for a save soon. Urgent requests (quit, pause, yard completed) ignore the gap.</summary>
        public void Request(string reason, bool urgent = false)
        {
            if (!HasPendingRequest || urgent) PendingReason = reason;
            HasPendingRequest = true;
            if (urgent) _urgent = true;
        }

        /// <summary>Returns true when a save should happen now. Call <see cref="MarkSaved"/> after saving.</summary>
        public bool ShouldSave(double now, out string reason)
        {
            double sinceLast = now - LastSaveTime;
            if (HasPendingRequest && (_urgent || sinceLast >= MinGap))
            {
                reason = PendingReason;
                return true;
            }
            if (Interval > 0f && sinceLast >= Interval)
            {
                reason = "periodic";
                return true;
            }
            reason = null;
            return false;
        }

        public void MarkSaved(double now)
        {
            LastSaveTime = now;
            HasPendingRequest = false;
            PendingReason = null;
            _urgent = false;
        }
    }
}

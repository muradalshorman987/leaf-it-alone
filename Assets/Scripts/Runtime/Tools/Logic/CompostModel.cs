using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>
    /// Compost bin rules (our signature mechanic). Leaves go in; every
    /// <see cref="LeavesPerBatch"/> leaves start a batch that composts for
    /// <see cref="CompostSeconds"/>; a finished batch is fertilizer the player
    /// spreads to restore the yard by <see cref="RestorePerBatch"/>.
    /// Plain C# so it can be tested and saved without a scene.
    /// </summary>
    public class CompostModel
    {
        public int LeavesPerBatch { get; }
        public float CompostSeconds { get; }
        public float RestorePerBatch { get; }

        /// <summary>Leaves waiting for the next batch.</summary>
        public int PendingLeaves { get; private set; }
        /// <summary>True while a batch is composting.</summary>
        public bool IsComposting { get; private set; }
        /// <summary>0..1 progress of the batch that is composting.</summary>
        public float CompostProgress { get; private set; }
        /// <summary>Finished batches ready to spread.</summary>
        public int ReadyBatches { get; private set; }
        /// <summary>How restored the yard is, 0..1 (drives the regrowth shader).</summary>
        public float Restoration { get; private set; }

        /// <summary>0..1 fill of the next batch, for the bin visual and HUD.</summary>
        public float Fill01 => Mathf.Clamp01((float)PendingLeaves / LeavesPerBatch);
        public bool IsFullyRestored => Restoration >= 1f - 1e-4f;

        public CompostModel(int leavesPerBatch, float compostSeconds, float restorePerBatch, float restoration = 0f)
        {
            LeavesPerBatch = Mathf.Max(1, leavesPerBatch);
            CompostSeconds = Mathf.Max(0f, compostSeconds);
            RestorePerBatch = Mathf.Clamp01(restorePerBatch);
            Restoration = Mathf.Clamp01(restoration);
        }

        public void AddLeaves(int leaves)
        {
            if (leaves <= 0) return;
            PendingLeaves += leaves;
            TryStartBatch();
        }

        /// <returns>True on the frame a batch finishes composting.</returns>
        public bool Tick(float dt)
        {
            if (!IsComposting) return false;
            CompostProgress = CompostSeconds <= 0f ? 1f : CompostProgress + dt / CompostSeconds;
            if (CompostProgress < 1f) return false;

            IsComposting = false;
            CompostProgress = 0f;
            ReadyBatches++;
            TryStartBatch();
            return true;
        }

        /// <summary>Spreads one batch of fertilizer. False when none is ready or the yard is fully restored.</summary>
        public bool TrySpread()
        {
            if (ReadyBatches <= 0 || IsFullyRestored) return false;
            ReadyBatches--;
            Restoration = Mathf.Min(1f, Restoration + RestorePerBatch);
            return true;
        }

        /// <summary>
        /// Puts the bin back the way a save file recorded it. Values are clamped, and a
        /// bin that was composting resumes where it left off.
        /// </summary>
        public void RestoreState(float restoration, int pendingLeaves, int readyBatches, bool isComposting, float compostProgress)
        {
            Restoration = Mathf.Clamp01(restoration);
            PendingLeaves = Mathf.Max(0, pendingLeaves);
            ReadyBatches = Mathf.Max(0, readyBatches);
            IsComposting = isComposting;
            CompostProgress = isComposting ? Mathf.Clamp01(compostProgress) : 0f;
            TryStartBatch();
        }

        void TryStartBatch()
        {
            if (IsComposting || PendingLeaves < LeavesPerBatch) return;
            PendingLeaves -= LeavesPerBatch;
            IsComposting = true;
            CompostProgress = 0f;
        }
    }
}

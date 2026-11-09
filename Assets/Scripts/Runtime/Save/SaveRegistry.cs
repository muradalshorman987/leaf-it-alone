using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallenYard.Save
{
    /// <summary>
    /// Keeps track of <see cref="ISaveable"/> systems. SaveSystem.Save captures all of them
    /// before writing; whoever loads data calls <see cref="RestoreAll"/>.
    /// </summary>
    public static class SaveRegistry
    {
        static readonly List<ISaveable> Saveables = new List<ISaveable>();

        public static int Count => Saveables.Count;

        /// <summary>
        /// Registers a saveable. If <paramref name="restoreFrom"/> is given (usually the
        /// already-loaded GameManager data) it is restored immediately, so systems that
        /// spawn after the load still get their state.
        /// </summary>
        public static void Register(ISaveable saveable, SaveData restoreFrom = null)
        {
            if (saveable == null || Saveables.Contains(saveable)) return;
            Saveables.Add(saveable);
            if (restoreFrom != null) Safe(saveable, s => s.RestoreState(restoreFrom), "restore");
        }

        public static void Unregister(ISaveable saveable) => Saveables.Remove(saveable);

        public static void CaptureAll(SaveData data)
        {
            if (data == null) return;
            // Copy so a saveable may unregister itself while capturing.
            foreach (var s in Saveables.ToArray()) Safe(s, x => x.CaptureState(data), "capture");
        }

        public static void RestoreAll(SaveData data)
        {
            if (data == null) return;
            foreach (var s in Saveables.ToArray()) Safe(s, x => x.RestoreState(data), "restore");
        }

        /// <summary>Clears registrations before each play session (domain reload may be off). Tests call it too.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() => Saveables.Clear();

        static void Safe(ISaveable s, Action<ISaveable> action, string what)
        {
            try
            {
                action(s);
            }
            catch (Exception e)
            {
                // One broken system must never stop the rest of the game from saving.
                Debug.LogError($"[FallenYard] {s.GetType().Name} failed to {what} save state: {e}");
            }
        }
    }
}

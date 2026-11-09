using FallenYard.Progression;
using FallenYard.Save;

namespace FallenYard.Levels
{
    /// <summary>
    /// Which yard to play: where "Continue" goes, what "Next yard" offers. Pure rules over the
    /// save, the catalog and its unlock table, used by <see cref="LevelFlow"/> and the UI.
    /// </summary>
    public static class YardFlow
    {
        public static bool IsPlayable(SaveData data, YardCatalog catalog, string yardId) =>
            data != null && catalog != null && catalog.Get(yardId) != null &&
            MetaProgression.IsPlayable(data, catalog.unlockTable, yardId);

        public static YardStatus Status(SaveData data, YardCatalog catalog, string yardId) =>
            data == null || catalog == null ? YardStatus.Locked : MetaProgression.GetStatus(data, catalog.unlockTable, yardId);

        /// <summary>
        /// The yard "Continue" should open: the last yard played if it's still playable and not
        /// finished, else the first playable yard not finished yet, else the last yard played,
        /// else the first playable yard, else the first yard in the catalog.
        /// </summary>
        public static string ContinueTarget(SaveData data, YardCatalog catalog)
        {
            if (catalog == null || catalog.Count == 0) return null;
            string first = null, firstPlayable = null, firstUnfinished = null;
            foreach (var y in catalog.yards)
            {
                if (y == null) continue;
                first ??= y.yardId;
                var status = Status(data, catalog, y.yardId);
                if (status == YardStatus.Unlocked || status == YardStatus.Completed) firstPlayable ??= y.yardId;
                if (status == YardStatus.Unlocked) firstUnfinished ??= y.yardId;
            }

            string last = data?.lastYardId;
            if (!string.IsNullOrEmpty(last) && Status(data, catalog, last) == YardStatus.Unlocked) return last;
            if (firstUnfinished != null) return firstUnfinished;
            if (!string.IsNullOrEmpty(last) && IsPlayable(data, catalog, last)) return last;
            return firstPlayable ?? first;
        }

        /// <summary>The yard after <paramref name="currentYardId"/> and its status, or (null, Locked) at the end of the town.</summary>
        public static (YardDefinition yard, YardStatus status) Next(SaveData data, YardCatalog catalog, string currentYardId)
        {
            var next = catalog != null ? catalog.NextAfter(currentYardId) : null;
            if (next == null) return (null, YardStatus.Locked);
            return (next, Status(data, catalog, next.yardId));
        }

        /// <summary>Scene to load for the yard, or null if the catalog doesn't know it.</summary>
        public static string SceneFor(YardCatalog catalog, string yardId)
        {
            var y = catalog != null ? catalog.Get(yardId) : null;
            return y != null && !string.IsNullOrEmpty(y.sceneName) ? y.sceneName : null;
        }
    }
}

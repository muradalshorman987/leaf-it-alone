using System;
using System.Collections.Generic;

namespace FallenYard.Accessibility
{
    /// <summary>How a sound cue is captioned.</summary>
    public readonly struct CaptionInfo
    {
        public readonly string Text;
        /// <summary>Seconds on screen after the last time the cue fired.</summary>
        public readonly float Seconds;

        public CaptionInfo(string text, float seconds)
        {
            Text = text;
            Seconds = seconds;
        }
    }

    /// <summary>
    /// Cue id to caption text. Audio (WP8) raises <c>GameEvents.RaiseAudioCuePlayed(id)</c> with these ids;
    /// unknown ids are ignored unless registered with <see cref="Register"/>. Captions follow the usual
    /// subtitle style for sound effects: a short description in square brackets.
    /// </summary>
    public static class CaptionCatalog
    {
        // Ids are the names of WP8's Audio.SoundCue values (AudioManager raises cue.ToString()).
        public const string WindGust = "WindGust";
        public const string RakeRustle = "RakeRustle";
        public const string BlowerLoop = "BlowerLoop";
        public const string VacuumLoop = "VacuumLoop";
        public const string MulcherLoop = "MulcherLoop";
        public const string BagRustle = "BagRustle";
        public const string CoinChime = "CoinChime";
        public const string Purchase = "Purchase";
        public const string UpgradeChime = "Upgrade";
        public const string CompostThud = "CompostThud";
        public const string CompostReady = "CompostReady";
        public const string RestoreSparkle = "RestoreSparkle";
        public const string YardComplete = "YardComplete";
        public const string Birds = "Birdsong";

        // UI clicks, tool equips, the wind bed and music are deliberately not captioned.
        static readonly Dictionary<string, CaptionInfo> Builtin = new Dictionary<string, CaptionInfo>(StringComparer.Ordinal)
        {
            { WindGust, new CaptionInfo("[Wind gusts]", 2.5f) },
            { RakeRustle, new CaptionInfo("[Rake scraping leaves]", 0.8f) },
            { BlowerLoop, new CaptionInfo("[Leaf blower roaring]", 0.8f) },
            { VacuumLoop, new CaptionInfo("[Vacuum humming]", 0.8f) },
            { MulcherLoop, new CaptionInfo("[Mulcher grinding]", 0.8f) },
            { BagRustle, new CaptionInfo("[Bag rustles]", 2f) },
            { CoinChime, new CaptionInfo("[Coins clink]", 2f) },
            { Purchase, new CaptionInfo("[Till rings]", 2f) },
            { UpgradeChime, new CaptionInfo("[Upgrade chime]", 2.5f) },
            { CompostThud, new CaptionInfo("[Leaves thud into the bin]", 2f) },
            { CompostReady, new CaptionInfo("[Compost bin creaks]", 2.5f) },
            { RestoreSparkle, new CaptionInfo("[Grass sparkles]", 2.5f) },
            { YardComplete, new CaptionInfo("[Cheerful jingle]", 3.5f) },
            { Birds, new CaptionInfo("[Birds chirping]", 3f) },
        };

        static readonly Dictionary<string, CaptionInfo> Extra = new Dictionary<string, CaptionInfo>(StringComparer.Ordinal);

        public static bool TryGet(string cueId, out CaptionInfo info)
        {
            info = default;
            if (string.IsNullOrEmpty(cueId)) return false;
            return Extra.TryGetValue(cueId, out info) || Builtin.TryGetValue(cueId, out info);
        }

        /// <summary>Adds or overrides a caption (e.g. a new ambience from WP8). Text should be in [brackets].</summary>
        public static void Register(string cueId, string text, float seconds = 2.5f)
        {
            if (string.IsNullOrEmpty(cueId) || string.IsNullOrEmpty(text)) return;
            Extra[cueId] = new CaptionInfo(text, Math.Max(0.3f, seconds));
        }

        /// <summary>Drops registered extras (tests, domain reload).</summary>
        public static void ClearRegistered() => Extra.Clear();

        /// <summary>Loop cue that a tool makes while in use, or null for quiet tools.</summary>
        public static string ToolLoopCue(string toolId)
        {
            switch (toolId)
            {
                case "blower": return BlowerLoop;
                case "vacuum": return VacuumLoop;
                case "mulcher": return MulcherLoop;
                case "rake": return RakeRustle;
                default: return null;
            }
        }
    }
}

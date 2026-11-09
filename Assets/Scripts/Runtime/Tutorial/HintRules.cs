using FallenYard.Core;
using FallenYard.Save;

namespace FallenYard.Tutorial
{
    /// <summary>What the hint rules look at. Filled in by <see cref="TutorialDirector"/> each check.</summary>
    public struct HintContext
    {
        public GamePhase Phase;
        public bool TutorialActive;
        public int Coins;
        /// <summary>Cheapest upgrade or unlock in the shop, or -1 if nothing is for sale.</summary>
        public int CheapestPurchase;
        public int PileCount;
        /// <summary>Seconds since the player last bagged a pile (or since the yard started).</summary>
        public float SecondsSinceBag;
        /// <summary>A strong gust just blew.</summary>
        public bool GustJustHappened;
        /// <summary>A tool was just unlocked.</summary>
        public bool ToolJustUnlocked;
        /// <summary>Seconds since the last hint left the screen.</summary>
        public float SecondsSinceLastHint;
    }

    /// <summary>
    /// Contextual hints shown after (or outside) the tutorial. Pure: <see cref="Choose"/> returns
    /// the id of the hint to show now, or null. Each hint has a per-save limit tracked in
    /// <see cref="TutorialSave.hintsShown"/>, and hints are spaced out by <see cref="MinGapSeconds"/>.
    /// </summary>
    public static class HintRules
    {
        public const string AffordUpgrade = "afford_upgrade";
        public const string IdlePiles = "idle_piles";
        public const string Wind = "wind";
        public const string NewTool = "new_tool";

        /// <summary>Minimum quiet time between two hints.</summary>
        public const float MinGapSeconds = 20f;

        /// <summary>Seconds without bagging, with piles lying around, before the idle hint.</summary>
        public const float IdleSeconds = 45f;

        public static int MaxTimes(string hintId) => hintId switch
        {
            AffordUpgrade => 2,
            IdlePiles => 3,
            Wind => 1,
            NewTool => 3,
            _ => 1,
        };

        public static string Choose(in HintContext c, TutorialSave save)
        {
            if (c.Phase != GamePhase.Playing || c.TutorialActive) return null;

            // Event-driven hints don't wait for the gap: they're about what just happened.
            if (c.ToolJustUnlocked && Allowed(NewTool, save)) return NewTool;
            if (c.GustJustHappened && Allowed(Wind, save)) return Wind;

            if (c.SecondsSinceLastHint < MinGapSeconds) return null;
            if (c.CheapestPurchase >= 0 && c.Coins >= c.CheapestPurchase && Allowed(AffordUpgrade, save)) return AffordUpgrade;
            if (c.PileCount > 0 && c.SecondsSinceBag >= IdleSeconds && Allowed(IdlePiles, save)) return IdlePiles;
            return null;
        }

        public static bool Allowed(string hintId, TutorialSave save) =>
            save == null || save.TimesShown(hintId) < MaxTimes(hintId);

        public static void MarkShown(string hintId, TutorialSave save)
        {
            if (save == null || string.IsNullOrEmpty(hintId)) return;
            save.hintsShown ??= new System.Collections.Generic.List<string>();
            save.hintsShown.Add(hintId);
        }

        public static string Text(string hintId, KeyBindings keys)
        {
            keys ??= new KeyBindings();
            switch (hintId)
            {
                case AffordUpgrade: return $"You can afford an upgrade! Press {keys.Label(GameAction.Shop)} to visit the Tool Shed.";
                case IdlePiles: return $"Piles you can bag pulse when you look at them. Press {keys.Label(GameAction.Interact)} to bag one.";
                case Wind: return "Gusts scatter loose leaves. Bag your piles before the wind gets them!";
                case NewTool: return $"New tool! Switch tools with {UI.HudFormat.ToolKeys(keys)}.";
                default: return "";
            }
        }
    }
}

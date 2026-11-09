using System;
using FallenYard.Core;
using FallenYard.Save;

namespace FallenYard.Tutorial
{
    /// <summary>Steps of the first-yard tutorial, in order. Stored by index in <see cref="TutorialSave.step"/>.</summary>
    public enum TutorialStep
    {
        Move = 0,
        Rake = 1,
        Bag = 2,
        Coins = 3,
        Shop = 4,
        Finish = 5,
        Done = 6,
    }

    /// <summary>
    /// The first-yard tutorial as a pure state machine. <see cref="TutorialDirector"/> feeds it
    /// game events (moved, pile formed, pile bagged, phase changed, yard completed) and shows
    /// <see cref="Prompt"/>. Each step completes on the thing it teaches, never on a timer,
    /// except the "coins" explainer, which moves on by itself after a few seconds.
    /// </summary>
    public class TutorialModel
    {
        /// <summary>Seconds of walking that complete the Move step.</summary>
        public float moveSecondsNeeded = 1.5f;

        /// <summary>Seconds the Coins explainer stays up (or until the next pile is bagged).</summary>
        public float coinsStepSeconds = 7f;

        public TutorialStep Step { get; private set; }
        public bool Skipped { get; private set; }
        public bool IsActive => Step != TutorialStep.Done;

        /// <summary>Seconds spent on the current step.</summary>
        public float StepTime { get; private set; }

        /// <summary>Raised with the new step whenever it changes (including Done).</summary>
        public event Action<TutorialStep> StepChanged;

        float _moved;

        public TutorialModel(TutorialStep start = TutorialStep.Move)
        {
            Step = start;
        }

        // ------------------------------------------------------------ inputs

        public void Tick(float dt)
        {
            if (!IsActive || dt <= 0f) return;
            StepTime += dt;
            if (Step == TutorialStep.Coins && StepTime >= coinsStepSeconds) Advance();
        }

        /// <summary>Player walked this frame.</summary>
        public void NotifyMoved(float dt)
        {
            if (Step != TutorialStep.Move || dt <= 0f) return;
            _moved += dt;
            if (_moved >= moveSecondsNeeded) Advance();
        }

        /// <summary>A pile big enough to bag exists.</summary>
        public void NotifyPileFormed()
        {
            // Raking a pile before finishing the walk step is fine: it shows they can move.
            if (Step == TutorialStep.Move || Step == TutorialStep.Rake) GoTo(TutorialStep.Bag);
        }

        public void NotifyPileBagged()
        {
            if (Step <= TutorialStep.Bag) GoTo(TutorialStep.Coins);
            else if (Step == TutorialStep.Coins) Advance();
        }

        public void NotifyPhase(GamePhase phase)
        {
            if (phase == GamePhase.Shop && Step == TutorialStep.Shop) Advance();
        }

        public void NotifyYardCompleted()
        {
            if (IsActive) GoTo(TutorialStep.Done);
        }

        public void Skip()
        {
            if (!IsActive) return;
            Skipped = true;
            GoTo(TutorialStep.Done);
        }

        /// <summary>Starts over from the first step (options screen "Replay tutorial").</summary>
        public void Restart()
        {
            Skipped = false;
            _moved = 0f;
            GoTo(TutorialStep.Move, force: true);
        }

        // ------------------------------------------------------------ text

        /// <summary>Instruction for the current step with the player's own keys, or "" when done.</summary>
        public string Prompt(KeyBindings keys, bool toggleToolUse = false)
        {
            keys ??= new KeyBindings();
            switch (Step)
            {
                case TutorialStep.Move:
                    return $"Walk with {MoveKeys(keys)} and look around with the mouse.";
                case TutorialStep.Rake:
                    return toggleToolUse
                        ? $"Press {keys.Label(GameAction.UseTool)} to start raking (press again to stop). Sweep leaves together into a pile."
                        : $"Hold {keys.Label(GameAction.UseTool)} to rake. Sweep leaves together into a pile.";
                case TutorialStep.Bag:
                    return $"Nice pile! Look at the highlighted pile and press {keys.Label(GameAction.Interact)} to bag it.";
                case TutorialStep.Coins:
                    return "Every bag earns coins. Fill the Yard bar up to the marker to finish this yard.";
                case TutorialStep.Shop:
                    return $"Press {keys.Label(GameAction.Shop)} to open the Tool Shed and spend coins on upgrades.";
                case TutorialStep.Finish:
                    return "That's everything. Clean the rest of the yard to finish it!";
                default:
                    return "";
            }
        }

        /// <summary>Short title shown above the prompt: "Tutorial 2/6".</summary>
        public string Progress => IsActive ? $"Tutorial {(int)Step + 1}/{(int)TutorialStep.Done}" : "";

        public static string MoveKeys(KeyBindings keys)
        {
            string w = keys.Label(GameAction.MoveForward), a = keys.Label(GameAction.MoveLeft),
                s = keys.Label(GameAction.MoveBack), d = keys.Label(GameAction.MoveRight);
            bool singleLetters = w.Length == 1 && a.Length == 1 && s.Length == 1 && d.Length == 1;
            return singleLetters ? w + a + s + d : $"{w} / {a} / {s} / {d}";
        }

        // ------------------------------------------------------------ save

        public void Load(TutorialSave save)
        {
            if (save == null) return;
            int step = Math.Max(0, Math.Min(save.step, (int)TutorialStep.Done));
            Skipped = save.skipped;
            _moved = 0f;
            GoTo((TutorialStep)step, force: true);
        }

        public void Store(TutorialSave save)
        {
            if (save == null) return;
            save.step = (int)Step;
            save.skipped = Skipped;
        }

        // ------------------------------------------------------------ internals

        void Advance() => GoTo(Step + 1);

        void GoTo(TutorialStep next, bool force = false)
        {
            if (next > TutorialStep.Done) next = TutorialStep.Done;
            if (next == Step && !force) return;
            Step = next;
            StepTime = 0f;
            StepChanged?.Invoke(Step);
        }
    }
}

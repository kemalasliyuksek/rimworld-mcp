using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>
    /// Runs game code meant for a click without disturbing the player, who may be busy with something
    /// else. Much of that code, in the game and in mods, selects things, starts targeting or opens a
    /// window, and there is no way to tell beforehand. Anything it did to the player's input is undone
    /// here, and the result says that the player has to do that step themselves.
    ///
    /// Many buttons also act on whatever is selected rather than on their own pawn, so the thing being
    /// acted for is selected while the code runs. Nothing is drawn in that time, so the player never sees
    /// it, and the selection list is changed directly because Selector.Select can also change the map.
    /// </summary>
    internal static class PlayerInputGuard
    {
        public sealed class Outcome
        {
            /// <summary>Why the step needs the player, or null if it ran to the end.</summary>
            public string NeedsPlayer;

            /// <summary>The game's question, if it asked one and no answer was given in advance.</summary>
            public string NeedsConfirmation;
        }

        public static Outcome Run(Thing actingFor, Action action, bool confirm = false)
        {
            List<object> selection = Find.Selector.SelectedObjects.ToList();
            ReplaceSelection(actingFor != null ? new List<object> { actingFor } : new List<object>());
            Designator designator = Find.DesignatorManager.SelectedDesignator;
            bool wasTargeting = Find.Targeter.IsTargeting;
            HashSet<Window> windows = new HashSet<Window>(Find.WindowStack.Windows);

            var outcome = new Outcome();
            try
            {
                action();

                // A yes-or-no question is the one window worth answering here, when the player has said yes.
                foreach (Dialog_MessageBox box in Find.WindowStack.Windows.OfType<Dialog_MessageBox>().Where(w => !windows.Contains(w)).ToList())
                {
                    if (confirm)
                    {
                        Find.WindowStack.TryRemove(box, doCloseSound: false);
                        box.buttonAAction?.Invoke();
                    }
                    else
                    {
                        Find.WindowStack.TryRemove(box, doCloseSound: false);
                        outcome.NeedsConfirmation = GameAccess.Clean(box.text);
                    }
                }
            }
            finally
            {
                if (!wasTargeting && Find.Targeter.IsTargeting)
                {
                    Find.Targeter.StopTargeting();
                    outcome.NeedsPlayer = "It asks for a target on screen.";
                }

                foreach (Window window in Find.WindowStack.Windows.Where(w => !windows.Contains(w) && !(w is ImmediateWindow)).ToList())
                {
                    Find.WindowStack.TryRemove(window, doCloseSound: false);

                    // The inspect pane opens for anything selected, which says nothing about the step.
                    if (!(window is MainTabWindow))
                    {
                        outcome.NeedsPlayer = $"It opens a window with more choices ({window.GetType().Name}).";
                    }
                }

                if (Find.DesignatorManager.SelectedDesignator != designator)
                {
                    if (designator != null)
                    {
                        Find.DesignatorManager.Select(designator);
                    }
                    else
                    {
                        Find.DesignatorManager.Deselect();
                    }
                }

                ReplaceSelection(selection.Where(selected => !(selected is Thing thing) || !thing.Destroyed).ToList());
            }
            return outcome;
        }

        private static void ReplaceSelection(List<object> selection)
        {
            List<object> current = Find.Selector.SelectedObjects;
            current.Clear();
            current.AddRange(selection);
        }
    }
}

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
        }

        public static Outcome Run(Thing actingFor, Action action)
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
                    outcome.NeedsPlayer = "It opens a window with more choices.";
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

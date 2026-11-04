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
    /// </summary>
    internal static class PlayerInputGuard
    {
        public sealed class Outcome
        {
            /// <summary>Why the step needs the player, or null if it ran to the end.</summary>
            public string NeedsPlayer;
        }

        public static Outcome Run(Action action)
        {
            List<object> selection = Find.Selector.SelectedObjects.ToList();
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

                RestoreSelection(selection);
            }
            return outcome;
        }

        private static void RestoreSelection(List<object> selection)
        {
            Selector selector = Find.Selector;
            if (selector.SelectedObjects.Count == selection.Count && selector.SelectedObjects.All(selection.Contains))
            {
                return;
            }
            selector.ClearSelection();
            foreach (object selected in selection)
            {
                selector.Select(selected, playSound: false, forceDesignatorDeselect: false);
            }
        }
    }
}

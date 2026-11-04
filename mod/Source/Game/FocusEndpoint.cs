using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>What the player is looking at right now, which is what "this" means in their questions.</summary>
    internal static class FocusEndpoint
    {
        private const int MaxText = 3000;

        // Dialog_NodeTree keeps its node protected, and DiaOption keeps its label protected.
        private static readonly FieldInfo CurrentNodeField =
            typeof(Dialog_NodeTree).GetField("curNode", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo OptionTextField =
            typeof(DiaOption).GetField("text", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void Focus(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();

            json.BeginObject();
            json.Prop("current_map", Find.CurrentMap?.uniqueID ?? -1);
            json.Prop("viewing_world_map", Find.World?.renderer != null && WorldRendererUtility.WorldSelected);
            WriteCamera(json);
            WriteSelection(json);
            WriteWindows(json);
            json.Prop("open_tab", Find.MainTabsRoot?.OpenTab?.defName);
            json.Prop("active_designator", GameAccess.Clean(Find.DesignatorManager?.SelectedDesignator?.Label));
            json.EndObject();
        }

        private static void WriteCamera(JsonWriter json)
        {
            if (Find.CurrentMap == null || Find.CameraDriver == null)
            {
                return;
            }
            CellRect view = Find.CameraDriver.CurrentViewRect;
            json.Key("camera").BeginObject();
            json.Prop("min_x", view.minX);
            json.Prop("min_z", view.minZ);
            json.Prop("max_x", view.maxX);
            json.Prop("max_z", view.maxZ);
            json.EndObject();
        }

        private static void WriteSelection(JsonWriter json)
        {
            json.Key("selected").BeginArray();
            foreach (object selected in Find.Selector.SelectedObjectsListForReading)
            {
                json.BeginObject();
                switch (selected)
                {
                    case Pawn pawn:
                        json.Prop("kind", "pawn");
                        json.Prop("id", pawn.ThingID);
                        json.Prop("label", GameAccess.Clean(pawn.LabelCap));
                        GameAccess.WritePosition(json, pawn);
                        break;
                    case Thing thing:
                        json.Prop("kind", "thing");
                        json.Prop("id", thing.ThingID);
                        json.Prop("def", thing.def.defName);
                        json.Prop("label", GameAccess.Clean(thing.LabelCap));
                        GameAccess.WritePosition(json, thing);
                        json.Prop("info", SafeInspectString(thing));
                        break;
                    case Zone zone:
                        json.Prop("kind", "zone");
                        json.Prop("label", GameAccess.Clean(zone.RenamableLabel));
                        json.Prop("cells", zone.CellCount);
                        break;
                    default:
                        json.Prop("kind", selected?.GetType().Name);
                        break;
                }
                json.EndObject();
            }
            json.EndArray();
        }

        private static string SafeInspectString(Thing thing)
        {
            try
            {
                return GameAccess.Clean(thing.GetInspectString());
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void WriteWindows(JsonWriter json)
        {
            IList<Window> windows = Find.WindowStack.Windows;
            json.Key("windows").BeginArray();
            foreach (Window window in windows)
            {
                // These are always present and say nothing about what the player is doing.
                if (window is ImmediateWindow)
                {
                    continue;
                }
                json.BeginObject();
                json.Prop("type", window.GetType().Name);
                json.Prop("pauses_game", window.forcePause);
                if (window is Dialog_MessageBox messageBox)
                {
                    json.Prop("title", GameAccess.Clean(messageBox.title));
                    json.Prop("text", GameAccess.Truncate(GameAccess.Clean(messageBox.text), MaxText));
                }
                else if (window is Dialog_NodeTree tree)
                {
                    WriteDialogNode(json, tree);
                }
                json.EndObject();
            }
            json.EndArray();
        }

        // Event dialogs are where the player asks for advice most, so the choices are included.
        private static void WriteDialogNode(JsonWriter json, Dialog_NodeTree tree)
        {
            if (!(CurrentNodeField?.GetValue(tree) is DiaNode node))
            {
                return;
            }
            json.Prop("text", GameAccess.Truncate(GameAccess.Clean(node.text), MaxText));
            json.Key("options").BeginArray();
            foreach (DiaOption option in node.options)
            {
                json.BeginObject();
                json.Prop("text", GameAccess.Clean(OptionTextField?.GetValue(option) as string));
                json.Prop("disabled", option.disabled);
                if (option.disabled)
                {
                    json.Prop("disabled_reason", GameAccess.Clean(option.disabledReason));
                }
                json.EndObject();
            }
            json.EndArray();
        }
    }
}

using UnityEngine;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>The player's switches for what assistants may do. Stored in the game's config, not in saves.</summary>
    public sealed class McpSettings : ModSettings
    {
        public bool allowActions = true;

        public bool allowCheats = true;

        public bool allowHiddenInformation = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref allowActions, "allowActions", defaultValue: true);
            Scribe_Values.Look(ref allowCheats, "allowCheats", defaultValue: true);
            Scribe_Values.Look(ref allowHiddenInformation, "allowHiddenInformation", defaultValue: true);
        }
    }

    public sealed class McpMod : Mod
    {
        public static McpSettings Settings { get; private set; }

        public McpMod(ModContentPack content)
            : base(content)
        {
            Settings = GetSettings<McpSettings>();
        }

        public override string SettingsCategory()
        {
            return "RimWorld MCP";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label("What AI assistants may do when you ask them. Reading what you can see yourself is always allowed.");
            listing.Gap();
            listing.CheckboxLabeled("Actions", ref Settings.allowActions, "Anything that changes the game, its settings or its mods, such as giving orders.");
            listing.CheckboxLabeled("Cheats", ref Settings.allowCheats, "Things beyond what the game normally allows, such as editing colonists.");
            listing.CheckboxLabeled("Hidden information", ref Settings.allowHiddenInformation, "What you cannot see yourself, such as what is under the fog.");
            listing.End();
        }
    }
}

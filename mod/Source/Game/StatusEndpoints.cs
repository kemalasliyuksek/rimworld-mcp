using RimWorld;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    internal static class StatusEndpoints
    {
        public static void Status(QueryParams query, JsonWriter json)
        {
            json.BeginObject();
            json.Prop("bridge_version", BridgeHost.Version);
            json.Prop("game_version", VersionControl.CurrentVersionStringWithRev);
            json.Prop("language", LanguageDatabase.activeLanguage?.folderName);
            json.Prop("game_loaded", Current.ProgramState == ProgramState.Playing && Current.Game != null);
            json.Prop("program_state", Current.ProgramState.ToString());
            json.Prop("world_generated", Current.Game?.World != null);
            json.Prop("on_site_selection_screen", Find.WindowStack != null && Find.WindowStack.IsOpen<Page_SelectStartingSite>());
            json.Prop("active_mods", LoadedModManager.RunningModsListForReading.Count);
            json.EndObject();
        }

        public static void Mods(QueryParams query, JsonWriter json)
        {
            json.BeginObject();
            json.Key("mods").BeginArray();
            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
            {
                json.BeginObject();
                json.Prop("name", mod.Name);
                json.Prop("package_id", mod.PackageIdPlayerFacing);
                json.Prop("official", mod.IsOfficialMod);
                json.EndObject();
            }
            json.EndArray();
            json.EndObject();
        }
    }
}

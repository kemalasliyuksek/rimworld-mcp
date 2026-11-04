using UnityEngine;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>
    /// Entry point. The game runs this on the main thread once all defs are loaded. The bridge
    /// deliberately adds no defs, game components or Harmony patches, so it leaves no trace in save
    /// files and can be added to or removed from an ongoing game.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class BridgeBootstrap
    {
        static BridgeBootstrap()
        {
            var host = new GameObject("RimWorldMcp.Bridge");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<BridgeHost>();
        }
    }
}

using System;
using System.Diagnostics;
using RimWorldMcp.Bridge.Core;
using UnityEngine;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>
    /// Owns the server and pumps its queue. A plain MonoBehaviour is used instead of a game
    /// component because Update must also run at the main menu and while the game is paused.
    /// </summary>
    public sealed class BridgeHost : MonoBehaviour
    {
        public const string Version = "0.1.0";

        private const int MaxRequestsPerFrame = 4;

        private readonly MainThreadQueue queue = new MainThreadQueue();
        private BridgeServer server;
        private bool discoveryWritten;

        private void Awake()
        {
            try
            {
                EndpointRegistry endpoints = BuildEndpoints();
                server = new BridgeServer(endpoints, queue, message => Log.Warning("[RimWorld MCP] " + message));
                server.Start();
                DiscoveryFile.Write(DiscoveryFile.DefaultPath, server.Port, server.Token, Version, Process.GetCurrentProcess().Id);
                discoveryWritten = true;
                Log.Message($"[RimWorld MCP] Bridge {Version} listening on 127.0.0.1:{server.Port}.");
            }
            catch (Exception ex)
            {
                Log.Error("[RimWorld MCP] Could not start the bridge: " + ex);
                Shutdown();
            }
        }

        private static EndpointRegistry BuildEndpoints()
        {
            var endpoints = new EndpointRegistry
            {
                BeforeHandler = () =>
                {
                    // Long events run game loading on another thread, so state is unsafe to read.
                    if (LongEventHandler.AnyEventNowOrWaiting)
                    {
                        throw new GameBusyException();
                    }
                },
                OnUnexpectedError = (path, ex) => Log.Warning($"[RimWorld MCP] {path} failed: {ex}"),
            };

            Register(endpoints, "/v1/status", StatusEndpoints.Status);
            Register(endpoints, "/v1/mods", StatusEndpoints.Mods);
            Register(endpoints, "/v1/overview", OverviewEndpoint.Overview);
            Register(endpoints, "/v1/alerts", OverviewEndpoint.Alerts);
            Register(endpoints, "/v1/focus", FocusEndpoint.Focus);
            Register(endpoints, "/v1/pawns", PawnEndpoints.List);
            Register(endpoints, "/v1/pawn", PawnEndpoints.Detail);
            Register(endpoints, "/v1/pawn/work", WorkEndpoints.SetPriorities, "POST");
            Register(endpoints, "/v1/pawn/schedule", WorkEndpoints.SetSchedule, "POST");
            Register(endpoints, "/v1/orders/draft", OrderEndpoints.SetDrafted, "POST");
            Register(endpoints, "/v1/orders/move", OrderEndpoints.Move, "POST");
            Register(endpoints, "/v1/orders", OrderEndpoints.ListOrders);
            Register(endpoints, "/v1/orders/give", OrderEndpoints.GiveOrder, "POST");
            Register(endpoints, "/v1/gizmos", OrderEndpoints.ListGizmos);
            Register(endpoints, "/v1/gizmos/use", OrderEndpoints.UseGizmo, "POST");
            Register(endpoints, "/v1/designators", ArchitectEndpoints.List);
            Register(endpoints, "/v1/designators/use", ArchitectEndpoints.Use, "POST");
            Register(endpoints, "/v1/plans", ArchitectEndpoints.ListPlans);
            Register(endpoints, "/v1/plans/draw", ArchitectEndpoints.DrawPlan, "POST");
            Register(endpoints, "/v1/plans/remove", ArchitectEndpoints.RemovePlan, "POST");
            Register(endpoints, "/v1/threats", ThreatEndpoint.Threats);
            Register(endpoints, "/v1/resources", ResourceEndpoint.Resources);
            Register(endpoints, "/v1/base", BaseEndpoint.Base);
            Register(endpoints, "/v1/research", ResearchEndpoint.Research);
            Register(endpoints, "/v1/events", WorldEndpoints.Events);
            Register(endpoints, "/v1/world", WorldEndpoints.World);
            Register(endpoints, "/v1/map/view", MapEndpoints.View);
            Register(endpoints, "/v1/map/cell", MapEndpoints.Cell);
            Register(endpoints, "/v1/defs/search", DefEndpoints.Search);
            Register(endpoints, "/v1/defs/get", DefEndpoints.Get);
            Register(endpoints, "/v1/new-game", NewGameEndpoints.Setup);
            Register(endpoints, "/v1/new-game/configure", NewGameEndpoints.Configure, "POST");
            Register(endpoints, "/v1/sites/world", SiteEndpoints.WorldSettings);
            Register(endpoints, "/v1/sites/search", SiteEndpoints.Search);
            Register(endpoints, "/v1/sites/get", SiteEndpoints.Get);
            Register(endpoints, "/v1/sites/show", SiteEndpoints.Show, "POST");
            Register(endpoints, "/v1/sites/options", SiteEndpoints.SetOptions, "POST");
            Register(endpoints, "/v1/ideo/options", IdeoEndpoints.Options);
            Register(endpoints, "/v1/ideo/setup", IdeoEndpoints.Setup);
            Register(endpoints, "/v1/ideo/configure", IdeoEndpoints.Configure, "POST");
            Register(endpoints, "/v1/ideo/icon-sheet", IdeoEndpoints.IconSheetImage);
            Register(endpoints, "/v1/game-settings", GameOptionsEndpoints.GetSettings);
            Register(endpoints, "/v1/game-settings/set", GameOptionsEndpoints.SetSettings, "POST");
            Register(endpoints, "/v1/mods/installed", GameOptionsEndpoints.InstalledMods);
            Register(endpoints, "/v1/mods/configure", GameOptionsEndpoints.ConfigureMods, "POST");
            Register(endpoints, "/v1/mods/settings", GameOptionsEndpoints.GetModSettings);
            Register(endpoints, "/v1/mods/settings/set", GameOptionsEndpoints.SetModSettings, "POST");
            Register(endpoints, "/v1/starting-pawns", StartingPawnEndpoints.List);
            Register(endpoints, "/v1/starting-pawns/find", StartingPawnEndpoints.FindCandidate, "POST");
            Register(endpoints, "/v1/starting-pawns/swap", StartingPawnEndpoints.Swap, "POST");
            Register(endpoints, "/v1/starting-pawns/edit", StartingPawnEndpoints.Edit, "POST", isCheat: true);
            return endpoints;
        }

        private static void Register(EndpointRegistry endpoints, string path, EndpointHandler handler, string method = "GET", bool isCheat = false)
        {
            endpoints.Register(path, (query, json) =>
            {
                if (method == "POST" && !McpMod.Settings.allowActions)
                {
                    throw new PermissionDisabledException("Actions");
                }
                if (isCheat && !McpMod.Settings.allowCheats)
                {
                    throw new PermissionDisabledException("Cheats");
                }

                // Some game getters consume random numbers. Observing must never alter the
                // sequence that the simulation sees.
                Rand.PushState();
                try
                {
                    handler(query, json);
                }
                finally
                {
                    Rand.PopState();
                }
            }, method);
        }

        private void Update()
        {
            queue.Pump(MaxRequestsPerFrame);
        }

        private void OnApplicationQuit()
        {
            Shutdown();
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void Shutdown()
        {
            server?.Dispose();
            server = null;
            if (discoveryWritten)
            {
                discoveryWritten = false;
                try
                {
                    DiscoveryFile.Delete(DiscoveryFile.DefaultPath);
                }
                catch (Exception ex)
                {
                    Log.Warning("[RimWorld MCP] Could not remove the discovery file: " + ex.Message);
                }
            }
        }
    }
}

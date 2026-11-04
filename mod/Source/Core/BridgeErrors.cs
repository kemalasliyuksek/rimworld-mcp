using System;

namespace RimWorldMcp.Bridge.Core
{
    /// <summary>A failure with a stable machine-readable code that clients can act on.</summary>
    public class BridgeException : Exception
    {
        public int StatusCode { get; }

        public string Code { get; }

        public BridgeException(int statusCode, string code, string message)
            : base(message)
        {
            StatusCode = statusCode;
            Code = code;
        }
    }

    public sealed class BadRequestException : BridgeException
    {
        public BadRequestException(string message)
            : base(400, "bad_request", message)
        {
        }
    }

    public sealed class PermissionDisabledException : BridgeException
    {
        public PermissionDisabledException(string switchName)
            : base(403, "permission_disabled", $"The player has turned off '{switchName}' in this mod's settings. Only the player can turn it back on.")
        {
        }
    }

    public sealed class NotFoundException : BridgeException
    {
        public NotFoundException(string message)
            : base(404, "not_found", message)
        {
        }
    }

    /// <summary>The request needs a loaded game, but the player is at the main menu.</summary>
    public sealed class NoGameLoadedException : BridgeException
    {
        public NoGameLoadedException()
            : base(409, "no_game_loaded", "No game is loaded. The player is at the main menu.")
        {
        }
    }

    /// <summary>The game is loading or saving, and its state must not be read right now.</summary>
    public sealed class GameBusyException : BridgeException
    {
        public GameBusyException()
            : base(503, "game_busy", "The game is loading or saving. Try again shortly.")
        {
        }
    }
}

using System;
using System.Collections.Generic;

namespace RimWorldMcp.Bridge.Core
{
    public delegate void EndpointHandler(QueryParams query, JsonWriter json);

    /// <summary>Maps request paths to handlers and turns failures into JSON error responses.</summary>
    public sealed class EndpointRegistry
    {
        private readonly Dictionary<string, (string Method, EndpointHandler Handler)> handlers =
            new Dictionary<string, (string Method, EndpointHandler Handler)>(StringComparer.Ordinal);

        /// <summary>Hook that runs before each handler, for example to reject calls during loading.</summary>
        public Action BeforeHandler { get; set; }

        /// <summary>Receives unexpected handler exceptions, which clients only see in summary.</summary>
        public Action<string, Exception> OnUnexpectedError { get; set; }

        public EndpointRegistry Register(string path, EndpointHandler handler, string method = "GET")
        {
            handlers.Add(path, (method, handler));
            return this;
        }

        public IEnumerable<string> Paths => handlers.Keys;

        public BridgeResponse Handle(string method, string path, QueryParams query)
        {
            if (!handlers.TryGetValue(path, out (string Method, EndpointHandler Handler) endpoint))
            {
                return BridgeResponse.Error(404, "unknown_endpoint", $"Unknown endpoint '{path}'.");
            }
            if (endpoint.Method != method)
            {
                return BridgeResponse.Error(405, "method_not_allowed", $"'{path}' only accepts {endpoint.Method}.");
            }

            try
            {
                BeforeHandler?.Invoke();
                var json = new JsonWriter();
                endpoint.Handler(query, json);
                return new BridgeResponse(200, json.ToString());
            }
            catch (BridgeException ex)
            {
                return BridgeResponse.Error(ex.StatusCode, ex.Code, ex.Message);
            }
            catch (Exception ex)
            {
                OnUnexpectedError?.Invoke(path, ex);
                return BridgeResponse.Error(500, "internal_error", ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}

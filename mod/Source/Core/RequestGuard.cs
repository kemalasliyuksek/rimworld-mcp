using System.Security.Cryptography;
using System.Text;

namespace RimWorldMcp.Bridge.Core
{
    /// <summary>
    /// Decides whether an HTTP request may reach the game. Binding to loopback is not enough on
    /// its own: any web page the player opens can send requests to localhost, so the token stops
    /// other local programs and the Host and Origin checks stop browsers, including DNS rebinding.
    /// </summary>
    public sealed class RequestGuard
    {
        private readonly byte[] expectedAuthorization;
        private readonly string expectedHost;

        public RequestGuard(string token, int port)
        {
            expectedAuthorization = Encoding.UTF8.GetBytes("Bearer " + token);
            expectedHost = "127.0.0.1:" + port;
        }

        /// <returns>An error response, or null when the request is allowed.</returns>
        public BridgeResponse Check(string method, string host, string origin, string authorization)
        {
            if (host != expectedHost)
            {
                return BridgeResponse.Error(403, "forbidden_host", "Unexpected Host header.");
            }
            if (!string.IsNullOrEmpty(origin))
            {
                return BridgeResponse.Error(403, "forbidden_origin", "Browser requests are not allowed.");
            }
            if (!TokenMatches(authorization))
            {
                return BridgeResponse.Error(401, "unauthorized", "Missing or invalid bearer token.");
            }
            if (method != "GET")
            {
                return BridgeResponse.Error(405, "method_not_allowed", "This version of the bridge is read-only.");
            }
            return null;
        }

        private bool TokenMatches(string authorization)
        {
            if (authorization == null)
            {
                return false;
            }
            byte[] actual = Encoding.UTF8.GetBytes(authorization);
            if (actual.Length != expectedAuthorization.Length)
            {
                return false;
            }
            int difference = 0;
            for (int i = 0; i < actual.Length; i++)
            {
                difference |= actual[i] ^ expectedAuthorization[i];
            }
            return difference == 0;
        }

        public static string GenerateToken()
        {
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }
            var token = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                token.Append(b.ToString("x2"));
            }
            return token.ToString();
        }
    }
}

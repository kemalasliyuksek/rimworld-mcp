using RimWorldMcp.Bridge.Core;
using Xunit;

namespace RimWorldMcp.Bridge.Tests
{
    public class RequestGuardTests
    {
        private const string Token = "secret-token";
        private const string GoodHost = "127.0.0.1:5000";
        private const string GoodAuth = "Bearer secret-token";

        private readonly RequestGuard guard = new RequestGuard(Token, 5000);

        [Fact]
        public void AllowsAValidRequest()
        {
            Assert.Null(guard.Check("GET", GoodHost, null, GoodAuth));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Bearer wrong-token!")]
        [InlineData("Bearer secret-toke")]
        [InlineData("secret-token")]
        public void RejectsBadTokens(string authorization)
        {
            Assert.Equal(401, guard.Check("GET", GoodHost, null, authorization).StatusCode);
        }

        [Theory]
        [InlineData("evil.example.com")]
        [InlineData("evil.example.com:5000")]
        [InlineData("localhost:5000")]
        [InlineData("127.0.0.1:5001")]
        [InlineData(null)]
        public void RejectsForeignHostsToStopDnsRebinding(string host)
        {
            Assert.Equal(403, guard.Check("GET", host, null, GoodAuth).StatusCode);
        }

        [Theory]
        [InlineData("https://evil.example.com")]
        [InlineData("null")]
        public void RejectsBrowserRequestsEvenWithAValidToken(string origin)
        {
            Assert.Equal(403, guard.Check("GET", GoodHost, origin, GoodAuth).StatusCode);
        }

        [Theory]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("DELETE")]
        public void TheBridgeIsReadOnly(string method)
        {
            Assert.Equal(405, guard.Check(method, GoodHost, null, GoodAuth).StatusCode);
        }

        [Fact]
        public void GeneratedTokensAreLongAndUnique()
        {
            string first = RequestGuard.GenerateToken();
            Assert.Equal(64, first.Length);
            Assert.NotEqual(first, RequestGuard.GenerateToken());
        }
    }
}

using System;
using RimWorldMcp.Bridge.Core;
using Xunit;

namespace RimWorldMcp.Bridge.Tests
{
    public class EndpointRegistryTests
    {
        private static QueryParams NoQuery => QueryParams.Parse("");

        [Fact]
        public void ReturnsHandlerOutput()
        {
            var registry = new EndpointRegistry().Register("/ok", (query, json) => json.BeginObject().Prop("a", 1).EndObject());
            BridgeResponse response = registry.Handle("/ok", NoQuery);
            Assert.Equal(200, response.StatusCode);
            Assert.Equal("{\"a\":1}", response.Body);
        }

        [Fact]
        public void UnknownPathIs404()
        {
            Assert.Equal(404, new EndpointRegistry().Handle("/missing", NoQuery).StatusCode);
        }

        [Fact]
        public void KnownFailuresKeepTheirStatusAndCode()
        {
            var registry = new EndpointRegistry().Register("/x", (query, json) => throw new NoGameLoadedException());
            BridgeResponse response = registry.Handle("/x", NoQuery);
            Assert.Equal(409, response.StatusCode);
            Assert.Contains("no_game_loaded", response.Body);
        }

        [Fact]
        public void UnexpectedExceptionsAreReportedWithoutEscaping()
        {
            string reportedPath = null;
            var registry = new EndpointRegistry
            {
                OnUnexpectedError = (path, ex) => reportedPath = path,
            };
            registry.Register("/x", (query, json) => throw new InvalidOperationException("boom"));

            BridgeResponse response = registry.Handle("/x", NoQuery);

            Assert.Equal(500, response.StatusCode);
            Assert.Contains("internal_error", response.Body);
            Assert.Equal("/x", reportedPath);
        }

        [Fact]
        public void BeforeHandlerCanBlockAllEndpoints()
        {
            bool handlerRan = false;
            var registry = new EndpointRegistry { BeforeHandler = () => throw new GameBusyException() };
            registry.Register("/x", (query, json) => handlerRan = true);

            Assert.Equal(503, registry.Handle("/x", NoQuery).StatusCode);
            Assert.False(handlerRan);
        }

        [Fact]
        public void APartiallyWrittenResponseIsDiscardedOnFailure()
        {
            var registry = new EndpointRegistry().Register("/x", (query, json) =>
            {
                json.BeginObject().Prop("partial", true);
                throw new NotFoundException("gone");
            });

            BridgeResponse response = registry.Handle("/x", NoQuery);

            Assert.Equal(404, response.StatusCode);
            Assert.DoesNotContain("partial", response.Body);
        }
    }
}

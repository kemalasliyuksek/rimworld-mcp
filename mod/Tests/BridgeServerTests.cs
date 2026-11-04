using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using RimWorldMcp.Bridge.Core;
using Xunit;

namespace RimWorldMcp.Bridge.Tests
{
    /// <summary>Runs the real server over a real socket, with a thread standing in for the game loop.</summary>
    public sealed class BridgeServerTests : IDisposable
    {
        private readonly MainThreadQueue queue = new MainThreadQueue();
        private readonly BridgeServer server;
        private readonly HttpClient client = new HttpClient();
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private readonly Thread gameLoop;

        public BridgeServerTests()
        {
            var endpoints = new EndpointRegistry().Register("/v1/echo", (query, json) =>
                json.BeginObject().Prop("q", query.GetString("q")).EndObject());
            server = new BridgeServer(endpoints, queue, _ => { });
            server.Start();

            gameLoop = new Thread(() =>
            {
                while (!stop.IsCancellationRequested)
                {
                    queue.Pump(4);
                    Thread.Sleep(1);
                }
            });
            gameLoop.Start();
        }

        private HttpRequestMessage Request(string path, bool withToken = true)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{server.Port}{path}");
            if (withToken)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", server.Token);
            }
            return request;
        }

        [Fact]
        public async Task ServesAnAuthorizedRequest()
        {
            HttpResponseMessage response = await client.SendAsync(Request("/v1/echo?q=merhaba%20d%C3%BCnya"), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("{\"q\":\"merhaba dünya\"}", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task RejectsARequestWithoutAToken()
        {
            HttpResponseMessage response = await client.SendAsync(Request("/v1/echo", withToken: false), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task RejectsARequestFromABrowserOrigin()
        {
            HttpRequestMessage request = Request("/v1/echo");
            request.Headers.Add("Origin", "https://evil.example.com");
            HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task UnknownEndpointReturnsJsonError()
        {
            HttpResponseMessage response = await client.SendAsync(Request("/v1/nope"), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Contains("unknown_endpoint", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }

        public void Dispose()
        {
            stop.Cancel();
            gameLoop.Join();
            server.Dispose();
            client.Dispose();
            stop.Dispose();
        }
    }
}

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace RimWorldMcp.Bridge.Core
{
    /// <summary>Loopback HTTP server. Requests are validated here, then run on the main thread.</summary>
    public sealed class BridgeServer : IDisposable
    {
        private const int MaxBindAttempts = 5;

        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

        private readonly EndpointRegistry endpoints;
        private readonly MainThreadQueue mainThread;
        private readonly Action<string> logError;
        private HttpListener listener;
        private RequestGuard guard;
        private Thread acceptThread;
        private volatile bool stopping;

        public int Port { get; private set; }

        public string Token { get; private set; }

        public BridgeServer(EndpointRegistry endpoints, MainThreadQueue mainThread, Action<string> logError)
        {
            this.endpoints = endpoints;
            this.mainThread = mainThread;
            this.logError = logError;
        }

        public void Start()
        {
            Token = RequestGuard.GenerateToken();
            listener = BindToFreePort(out int port);
            Port = port;
            guard = new RequestGuard(Token, Port);

            acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "RimWorldMcp.Bridge" };
            acceptThread.Start();
        }

        // HttpListener cannot bind to port 0, so a free port is found first. Another program can
        // take it in between, hence the retries.
        private static HttpListener BindToFreePort(out int port)
        {
            for (int attempt = 1; ; attempt++)
            {
                port = FindFreePort();
                var candidate = new HttpListener();
                candidate.Prefixes.Add("http://127.0.0.1:" + port + "/");
                try
                {
                    candidate.Start();
                    return candidate;
                }
                catch (Exception) when (attempt < MaxBindAttempts)
                {
                    candidate.Close();
                }
            }
        }

        private static int FindFreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        private void AcceptLoop()
        {
            while (!stopping)
            {
                try
                {
                    HttpListenerContext context = listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleContext(context));
                }
                catch (Exception ex)
                {
                    if (stopping)
                    {
                        return;
                    }
                    logError("Bridge accept failed: " + ex.Message);
                }
            }
        }

        private void HandleContext(HttpListenerContext context)
        {
            try
            {
                HttpListenerRequest request = context.Request;
                BridgeResponse response = guard.Check(
                    request.HttpMethod,
                    request.Headers["Host"],
                    request.Headers["Origin"],
                    request.Headers["Authorization"]);

                if (response == null)
                {
                    string method = request.HttpMethod;
                    string path = request.Url.AbsolutePath;
                    QueryParams query;
                    try
                    {
                        query = QueryParams.Parse(request.Url.Query);
                    }
                    catch (BridgeException ex)
                    {
                        WriteResponse(context, BridgeResponse.Error(ex.StatusCode, ex.Code, ex.Message));
                        return;
                    }
                    response = mainThread.Invoke(() => endpoints.Handle(method, path, query), RequestTimeout);
                }
                WriteResponse(context, response);
            }
            catch (Exception ex)
            {
                // The client probably disconnected. Nothing useful can be sent back.
                if (!stopping)
                {
                    logError("Bridge request failed: " + ex.Message);
                }
            }
        }

        private static void WriteResponse(HttpListenerContext context, BridgeResponse response)
        {
            byte[] body = Encoding.UTF8.GetBytes(response.Body);
            HttpListenerResponse output = context.Response;
            output.StatusCode = response.StatusCode;
            output.ContentType = "application/json; charset=utf-8";
            output.ContentLength64 = body.Length;
            output.OutputStream.Write(body, 0, body.Length);
            output.OutputStream.Close();
        }

        public void Dispose()
        {
            stopping = true;
            try
            {
                listener?.Close();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using RimWorldMcp.Bridge.Core;
using Xunit;

namespace RimWorldMcp.Bridge.Tests
{
    public class MainThreadQueueTests
    {
        private static readonly TimeSpan Long = TimeSpan.FromSeconds(10);

        private static async Task PumpUntil(MainThreadQueue queue, Func<bool> done)
        {
            while (!done())
            {
                queue.Pump(10);
                await Task.Delay(1, TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task WorkRunsOnThePumpingThreadOnly()
        {
            var queue = new MainThreadQueue();
            int pumpThread = Environment.CurrentManagedThreadId;
            int workThread = -1;

            Task<BridgeResponse> call = Task.Run(() => queue.Invoke(
                () =>
                {
                    workThread = Environment.CurrentManagedThreadId;
                    return new BridgeResponse(200, "ok");
                },
                Long));

            while (!call.IsCompleted)
            {
                queue.Pump(1);
                if (workThread == -1)
                {
                    Thread.Sleep(1);
                }
                else
                {
                    break;
                }
            }

            BridgeResponse response = await call;
            Assert.Equal("ok", response.Body);
            Assert.Equal(pumpThread, workThread);
        }

        [Fact]
        public void ReportsATimeoutWhenTheMainThreadIsStuck()
        {
            BridgeResponse response = new MainThreadQueue().Invoke(() => new BridgeResponse(200, "never"), TimeSpan.FromMilliseconds(20));
            Assert.Equal(503, response.StatusCode);
            Assert.Contains("game_not_responding", response.Body);
        }

        [Fact]
        public void AbandonedWorkNeverRunsLater()
        {
            var queue = new MainThreadQueue();
            bool ran = false;
            queue.Invoke(
                () =>
                {
                    ran = true;
                    return new BridgeResponse(200, "");
                },
                TimeSpan.FromMilliseconds(5));

            queue.Pump(10);

            Assert.False(ran);
        }

        [Fact]
        public async Task RejectsWorkBeyondItsCapacity()
        {
            var queue = new MainThreadQueue(capacity: 1);
            Func<BridgeResponse> work = () => new BridgeResponse(200, "done");

            // A timed-out item keeps its slot until the main thread drains it, so a frozen game
            // cannot make the queue grow without bound.
            Assert.Equal(503, queue.Invoke(work, TimeSpan.FromMilliseconds(1)).StatusCode);
            Assert.Equal(429, queue.Invoke(work, Long).StatusCode);

            queue.Pump(10);

            Task<BridgeResponse> call = Task.Run(() => queue.Invoke(work, Long));
            await PumpUntil(queue, () => call.IsCompleted);
            Assert.Equal("done", (await call).Body);
        }

        [Fact]
        public async Task HandlerExceptionsBecomeErrorResponses()
        {
            var queue = new MainThreadQueue();
            Task<BridgeResponse> call = Task.Run(() => queue.Invoke(() => throw new InvalidOperationException("boom"), Long));
            await PumpUntil(queue, () => call.IsCompleted);
            Assert.Equal(500, (await call).StatusCode);
        }
    }
}

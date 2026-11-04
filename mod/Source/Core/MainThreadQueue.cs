using System;
using System.Collections.Concurrent;
using System.Threading;

namespace RimWorldMcp.Bridge.Core
{
    /// <summary>
    /// Hands work from HTTP threads to the game's main thread. Game state is not thread-safe, so
    /// every read happens inside <see cref="Pump"/>, which the engine host calls once per frame.
    /// </summary>
    public sealed class MainThreadQueue
    {
        private sealed class WorkItem
        {
            public Func<BridgeResponse> Work;
            public BridgeResponse Response;
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
            public int Abandoned;
        }

        private readonly ConcurrentQueue<WorkItem> queue = new ConcurrentQueue<WorkItem>();
        private readonly int capacity;
        private int pending;

        public MainThreadQueue(int capacity = 32)
        {
            this.capacity = capacity;
        }

        /// <summary>Queues work and blocks the calling thread until the main thread has run it.</summary>
        public BridgeResponse Invoke(Func<BridgeResponse> work, TimeSpan timeout)
        {
            if (Interlocked.Increment(ref pending) > capacity)
            {
                Interlocked.Decrement(ref pending);
                return BridgeResponse.Error(429, "too_many_requests", "Too many requests are queued.");
            }

            var item = new WorkItem { Work = work };
            queue.Enqueue(item);

            if (item.Done.Wait(timeout))
            {
                return item.Response;
            }

            // Lost the race if the main thread already started it, in which case wait for the result.
            if (Interlocked.CompareExchange(ref item.Abandoned, 1, 0) != 0)
            {
                item.Done.Wait();
                return item.Response;
            }
            return BridgeResponse.Error(503, "game_not_responding", "The game did not respond in time. It may be frozen or still starting.");
        }

        /// <summary>Runs queued work. Only ever call this from the main thread.</summary>
        public void Pump(int maxItems)
        {
            for (int i = 0; i < maxItems && queue.TryDequeue(out WorkItem item); i++)
            {
                Interlocked.Decrement(ref pending);
                if (Interlocked.CompareExchange(ref item.Abandoned, 2, 0) != 0)
                {
                    continue;
                }

                try
                {
                    item.Response = item.Work();
                }
                catch (Exception ex)
                {
                    item.Response = BridgeResponse.Error(500, "internal_error", ex.GetType().Name + ": " + ex.Message);
                }
                item.Done.Set();
            }
        }
    }
}

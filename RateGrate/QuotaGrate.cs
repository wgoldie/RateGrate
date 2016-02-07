using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RateGrate
{
    /// <summary>
    /// A RateGrate that will allow a specified number of queries during any rate limit period of specified.
    /// </summary>
    public class QuotaGrate : RateGrate, IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentQueue<int> _expirationQueue;
        private int _nextReleaseTick = -1;

        public readonly TimeSpan Period;
        public readonly int Queries;

        /// <summary>
        /// Returns the numbers of queries available before the grate is saturated.
        /// </summary>
        public int QueriesLeft => _semaphore.CurrentCount;

        /// <summary>
        /// Returns the system tick time when another query will be available if the grate is saturated,
        /// or 0 if it is not saturated.
        /// </summary>
        public int NextQueryAvailable => (_nextReleaseTick > Environment.TickCount) ? _nextReleaseTick : 0;

        /// <summary>
        /// Constructs a new QuoteGrate
        /// </summary>
        /// <param name="queries">The number of queries allowed per rate limit period</param>
        /// <param name="period">The length of a rate limit period in seconds.</param>
        public QuotaGrate(int queries, TimeSpan period)
        {
            Queries = queries;
            Period = period;
            _semaphore = new SemaphoreSlim(0, queries);
            _expirationQueue = new ConcurrentQueue<int>();
            Work();
        }

        public override void GrateWait()
        {
            _expirationQueue.Enqueue((int)Math.Ceiling(Period.TotalMilliseconds));
            _semaphore.Wait();
        }

        /// <summary>
        /// Works on the current queue of query expirations,
        /// releasing the semaphore when the rate limit period has passed for each query.
        /// </summary>
        private async void Work()
        {
            while(true)
            {
                int expirationTick;
                if (_expirationQueue.TryPeek(out expirationTick))
                {
                    _nextReleaseTick = expirationTick;
                    await Task.Delay(TimeSpan.FromMilliseconds(_nextReleaseTick - Environment.TickCount));
                    _expirationQueue.TryDequeue(out expirationTick);
                    _semaphore.Release();
                }
                else
                {
                    await Task.Delay(Period);
                }
            }
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }
    }
}

namespace RateGrate
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A Grate that will allow a specified number of queries during any rate limit period of specified.
    /// </summary>
    public class QuotaGrate : Grate, IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentQueue<int> _expirationQueue;
        private readonly Timer _expirationTimer;
        // private int _nextReleaseTick = -1;

        public readonly int Period;
        public readonly int Queries;

        /// <summary>
        /// Returns the numbers of queries available before the grate is saturated.
        /// </summary>
        public int QueriesLeft => _semaphore.CurrentCount;

        /// <summary>
        /// Returns the system tick time when another query will be available if the grate is saturated,
        /// or 0 if it is not saturated.
        /// </summary>
        // public int NextQueryAvailable => (_nextReleaseTick > Environment.TickCount) ? _nextReleaseTick : 0;

        /// <summary>
        /// Constructs a new QuoteGrate
        /// </summary>
        /// <param name="queries">The number of queries allowed per rate limit period</param>
        /// <param name="period">The length of a rate limit period in seconds.</param>  
        public QuotaGrate(int queries, TimeSpan period)
        {
            Queries = queries;
            Period = (int)Math.Ceiling(period.TotalMilliseconds);
            _semaphore = new SemaphoreSlim(queries, queries);
            _expirationQueue = new ConcurrentQueue<int>();
            _expirationTimer = new Timer(Work, null, Period, Timeout.Infinite);

        }

        public override void Wait()
        {
            _semaphore.Wait();
        }

        public override void Release()
        {
            var t = Period + Environment.TickCount;
            _expirationQueue.Enqueue(t);
        }

        /// <summary>
        /// Works on the current queue of query expirations,
        /// releasing the semaphore when the rate limit period has passed for each query.
        /// </summary>
        private void Work(object state)
        {
            int expirationTick;
            var currentTick = Environment.TickCount;
            while (_expirationQueue.TryPeek(out expirationTick) && unchecked(currentTick - expirationTick) >= 17)
            {
                int t;
                if (_expirationQueue.TryDequeue(out t))
                {
                    _semaphore.Release();
                }
            }

            if (_expirationQueue.TryPeek(out expirationTick))
            {
                _expirationTimer.Change(unchecked(expirationTick - currentTick) + 18, Timeout.Infinite);
            }
            else
            {
                _expirationTimer.Change(Period, Timeout.Infinite);
            }
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }
    }
}

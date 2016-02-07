namespace RateGrate
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;

    /// <summary>
    /// A Grate that will allow a specified number of queries during any rate limit period of specified.
    /// </summary>
    public class QuotaGrate : Grate, IDisposable
    {
        /// <summary>
        /// Tracks available API slots.
        /// </summary>
        private readonly SemaphoreSlim _semaphore;

        /// <summary>
        /// Contains semaphore release times.
        /// </summary>
        private readonly ConcurrentQueue<int> _expirationQueue;

        /// <summary>
        /// Triggers semaphore management worker.
        /// </summary>
        private readonly Timer _expirationTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="QuotaGrate"/> class.
        /// </summary>
        /// <param name="queries">The number of queries allowed per rate limit period.</param>
        /// <param name="period">The length of a rate limit period in seconds.</param>  
        public QuotaGrate(int queries, TimeSpan period)
        {
            Queries = queries;
            Period = (int)Math.Ceiling(period.TotalMilliseconds);
            _semaphore = new SemaphoreSlim(queries, queries);
            _expirationQueue = new ConcurrentQueue<int>();
            _expirationTimer = new Timer(Work, null, Period, Timeout.Infinite);
        }

        /// <summary>
        /// Gets the rate limit period for this Grate.
        /// </summary>
        public int Period { get; }

        /// <summary>
        /// Gets the number of queries allowed per rate limit period for this grate.
        /// </summary>
        public int Queries { get; }

        /// <summary>
        /// Returns the numbers of queries available before the grate is saturated.
        /// </summary>
        public int QueriesLeft => _semaphore.CurrentCount;

        /// <summary>
        /// Waits until a single API slot is available.
        /// </summary>
        public override void Wait()
        {
            _semaphore.Wait();
        }

        /// <summary>
        /// Consumes a single API slot.
        /// </summary>
        public override void Release()
        {
            var t = Period + Environment.TickCount;
            _expirationQueue.Enqueue(t);
        }

        /// <summary>
        /// Disposes of IDisposable members.
        /// </summary>
        public void Dispose()
        {
            _semaphore.Dispose();
            _expirationTimer.Dispose();
        }

        /// <summary>
        /// Works on the current queue of query expirations,
        /// releasing the semaphore when the rate limit period has passed for each query.
        /// </summary>
        /// <param name="state">Necessary to satisfy Timer interface, not used.</param>
        private void Work(object state)
        {
            int expirationTick;
            var currentTick = Environment.TickCount;
            while (_expirationQueue.TryPeek(out expirationTick) && unchecked(currentTick - expirationTick) >= 0)
            {
                int t;
                if (_expirationQueue.TryDequeue(out t))
                {
                    _semaphore.Release();
                }
            }

            if (_expirationQueue.TryPeek(out expirationTick))
            {
                _expirationTimer.Change(unchecked(expirationTick - currentTick), Timeout.Infinite);
            }
            else
            {
                _expirationTimer.Change(Period, Timeout.Infinite);
            }
        }
    }
}

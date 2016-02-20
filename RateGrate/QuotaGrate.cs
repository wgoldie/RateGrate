using System.Timers;

namespace RateGrate
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// A Grate that will allow a specified number of queries during any rate limit period of specified.
    /// This is just an example of a grate implementation, and not necessarily "well written" "good code" "production-ready" etc
    /// </summary>
    /// <typeparam name="T">The type of the API token representation 
    /// <typeparamref name="T"/>
    /// (not necessarily the actual token, for instance a hash might be used instead)
    /// used to differentiate between different API quotas</typeparam>
    public class QuotaGrate<T> : Grate<T>, IDisposable
    {
        private readonly Dictionary<T, QuotaListing> _quotaMap; 

        /// <summary>
        /// Initializes a new instance of the <see cref="QuotaGrate{T}"/> class.
        /// </summary>
        /// <param name="queries">The number of queries allowed per rate limit period.</param>
        /// <param name="period">The length of a rate limit period in seconds.</param>  
        public QuotaGrate(int queries, TimeSpan period)
        {
            Queries = queries;
            Period = (int)Math.Ceiling(period.TotalMilliseconds);
            _quotaMap = new Dictionary<T, QuotaListing>();
        }

        /// <summary>
        /// Gets the rate limit period for this Grate.
        /// </summary>
        public int Period { get; }

        /// <summary>
        /// Gets the number of queries allowed per rate limit period for this grate.
        /// </summary>
        public int Queries { get; }

        public override void RegisterToken(T token)
        {
            if (_quotaMap.ContainsKey(token))
            {
                _quotaMap[token].IsPooled = false;
                return;
            }

            _quotaMap[token] = new QuotaListing(Work(token), Queries, Period, true);
        }

        /// <summary>
        /// Waits until a single API slot is available.
        /// </summary>
        /// <param name="token">A representation of an individual API token to wait for availability on.</param>
        public override void Wait(T token)
        {
            if (!_quotaMap.ContainsKey(token))
            {
                _quotaMap[token] = new QuotaListing(Work(token), Queries, Period, false);
            }

            _quotaMap[token].Semaphore.Wait();
        }

        /// <summary>
        /// Consumes a single API slot.
        /// </summary>
        /// <param name="token">A representation an an individual API token to wait for availability on.</param>
        public override void Release(T token)
        {
            var t = Period + Environment.TickCount;
            _quotaMap[token].ExpirationQueue.Enqueue(t);
        }

        /// <summary>
        /// Disposes of IDisposable members.
        /// </summary>
        public void Dispose()
        {
            foreach (var tp in _quotaMap.Values)
            {
                tp.Semaphore.Dispose();
                tp.ExpirationTimer.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Works on the current queue of query expirations,
        /// releasing the semaphore when the rate limit period has passed for each query.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        private TimerCallback Work(T key) => state =>
            {
                var currentTick = Environment.TickCount;
                int expirationTick;
                var ql = _quotaMap[key];
                while (ql.ExpirationQueue.TryPeek(out expirationTick) && unchecked(currentTick - expirationTick) >= 0)
                {
                    int t;
                    if (ql.ExpirationQueue.TryDequeue(out t))
                    {
                        ql.Semaphore.Release();
                    }
                }

                if (ql.ExpirationQueue.TryPeek(out expirationTick))
                {
                    ql.ExpirationTimer.Change(unchecked(expirationTick - currentTick), Timeout.Infinite);
                }
                else
                {
                    ql.ExpirationTimer.Change(Period, Timeout.Infinite);
                }
            };

        private class QuotaListing
        {
            public SemaphoreSlim Semaphore { get; }
            public ConcurrentQueue<int> ExpirationQueue { get; }
            public Timer ExpirationTimer { get; }
            public bool IsPooled { get; set; }

            public QuotaListing(TimerCallback worker, int queries, int period, bool isPooled)
            {
                Semaphore = new SemaphoreSlim(queries, queries);
                ExpirationQueue = new ConcurrentQueue<int>();
                ExpirationTimer = new Timer(worker, null, period, Timeout.Infinite);
                IsPooled = isPooled;
            }
        }
    }
}

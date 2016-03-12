namespace RateGrate
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Timers;

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
        /// <summary>
        /// Relates API tokens to their QuotaListing objects.
        /// </summary>
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

        /// <summary>
        /// Registers a new token to this grate.
        /// </summary>
        /// <param name="token">The API token object to insert</param>
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
        /// <returns>No return value.</returns>
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

        /// <summary>
        /// Tracks information for a specific API token on this grate.
        /// </summary>
        private class QuotaListing
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="QuotaListing"/> class.
            /// </summary>
            /// <param name="worker">The function that evaluates queued actions for this grate.</param>
            /// <param name="queries">The number of queries allowed per period.</param>
            /// <param name="period">The period in milliseconds.</param>
            /// <param name="isPooled">Whether or not the grate is allowed to run pooled queries using this token.</param>
            public QuotaListing(TimerCallback worker, int queries, int period, bool isPooled)
            {
                Semaphore = new SemaphoreSlim(queries, queries);
                ExpirationQueue = new ConcurrentQueue<int>();
                ExpirationTimer = new System.Threading.Timer(worker, null, period, Timeout.Infinite);
                IsPooled = isPooled;
            }

            /// <summary>
            /// Gets a semaphore that tracks this token's availability.
            /// </summary>
            public SemaphoreSlim Semaphore { get; }

            /// <summary>
            /// Gets a queue that tracks expiration times for previously run tasks.
            /// </summary>
            public ConcurrentQueue<int> ExpirationQueue { get; }

            /// <summary>
            /// Gets a timer object used to trigger the worker function.
            /// </summary>
            public System.Threading.Timer ExpirationTimer { get; }

            /// <summary>
            /// Gets or sets a value indicating whether or not this token can be used in pooled queries.
            /// </summary>
            public bool IsPooled { get; set; }
        }
    }
}

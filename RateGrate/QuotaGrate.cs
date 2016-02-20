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
        /// <summary>
        /// Tracks available API slots.
        /// </summary>
        private readonly Dictionary<T, Tuple<SemaphoreSlim, ConcurrentQueue<int>, Timer>> _quotaMap;
        

        /// <summary>
        /// Initializes a new instance of the <see cref="QuotaGrate{T}"/> class.
        /// </summary>
        /// <param name="queries">The number of queries allowed per rate limit period.</param>
        /// <param name="period">The length of a rate limit period in seconds.</param>  
        public QuotaGrate(int queries, TimeSpan period)
        {
            Queries = queries;
            Period = (int)Math.Ceiling(period.TotalMilliseconds);
            _quotaMap = new Dictionary<T, Tuple<SemaphoreSlim, ConcurrentQueue<int>, Timer>>();
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
        /// Waits until a single API slot is available.
        /// </summary>
        /// <param name="token">A representation of an individual API token to wait for availability on.</param>
        public override void Wait(T token)
        {
            if (!_quotaMap.ContainsKey(token))
            {
                _quotaMap[token] = Tuple.Create(
                    new SemaphoreSlim(Queries, Queries), 
                    new ConcurrentQueue<int>(),
                    new Timer(Work(token), null, Period, Timeout.Infinite));
            }

            _quotaMap[token].Item1.Wait();
        }

        /// <summary>
        /// Consumes a single API slot.
        /// </summary>
        /// <param name="token">A representation an an individual API token to wait for availability on.</param>
        public override void Release(T token)
        {
            var t = Period + Environment.TickCount;
            _quotaMap[token].Item2.Enqueue(t);
        }

        /// <summary>
        /// Disposes of IDisposable members.
        /// </summary>
        public void Dispose()
        {
            foreach (var tp in _quotaMap.Values)
            {
                tp.Item1.Dispose();
                tp.Item3.Dispose();
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
                var sem = _quotaMap[key].Item1;
                var que = _quotaMap[key].Item2;
                var tim = _quotaMap[key].Item3;
                while (que.TryPeek(out expirationTick) && unchecked(currentTick - expirationTick) >= 0)
                {
                    int t;
                    if (que.TryDequeue(out t))
                    {
                        sem.Release();
                    }
                }

                if (que.TryPeek(out expirationTick))
                {
                    tim.Change(unchecked(expirationTick - currentTick), Timeout.Infinite);
                }
                else
                {
                    tim.Change(Period, Timeout.Infinite);
                }
            };
    }
}

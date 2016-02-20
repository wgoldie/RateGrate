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
        private readonly Dictionary<T, Tuple<SemaphoreSlim, ConcurrentQueue<int>>> _semaphores;
        
        /// <summary>
        /// Triggers semaphore management worker.
        /// </summary>
        private readonly Timer _expirationTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="QuotaGrate{T}"/> class.
        /// </summary>
        /// <param name="queries">The number of queries allowed per rate limit period.</param>
        /// <param name="period">The length of a rate limit period in seconds.</param>  
        public QuotaGrate(int queries, TimeSpan period)
        {
            Queries = queries;
            Period = (int)Math.Ceiling(period.TotalMilliseconds);
            _semaphores = new Dictionary<T, Tuple<SemaphoreSlim, ConcurrentQueue<int>>>();
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
        /// Waits until a single API slot is available.
        /// </summary>
        /// <param name="token">A representation of an individual API token to wait for availability on.</param>
        public override void Wait(T token)
        {
            if (!_semaphores.ContainsKey(token))
            {
                _semaphores[token] = new Tuple<SemaphoreSlim, ConcurrentQueue<int>>(
                    new SemaphoreSlim(Queries, Queries), new ConcurrentQueue<int>());
            }

            _semaphores[token].Item1.Wait();
        }

        /// <summary>
        /// Consumes a single API slot.
        /// </summary>
        /// <param name="token">A representation an an individual API token to wait for availability on.</param>
        public override void Release(T token)
        {
            var t = Period + Environment.TickCount;
            _semaphores[token].Item2.Enqueue(t);
        }

        /// <summary>
        /// Disposes of IDisposable members.
        /// </summary>
        public void Dispose()
        {
            foreach (var tp in _semaphores.Values)
            {
                tp.Item1.Dispose();
            }
            _expirationTimer.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Works on the current queue of query expirations,
        /// releasing the semaphore when the rate limit period has passed for each query.
        /// </summary>
        /// <param name="state">Necessary to satisfy Timer interface, not used.</param>
        private void Work(object state)
        {
            var currentTick = Environment.TickCount;
            foreach (var tp in _semaphores.Values)
            {
                int expirationTick;
                var sem = tp.Item1;
                var que = tp.Item2;
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
                    _expirationTimer.Change(unchecked(expirationTick - currentTick), Timeout.Infinite);
                }
                else
                {
                    _expirationTimer.Change(Period, Timeout.Infinite);
                }
            }
        }
    }
}

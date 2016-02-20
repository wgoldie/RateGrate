namespace TestApiServer.Controllers
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Web;
    using System.Web.Caching;
    using System.Web.Http;
    using CacheItemPriority = System.Web.Caching.CacheItemPriority;

    /// <summary>
    /// A series of methods that will return the system tick time with various rate limits.
    /// This is probably a bad endpoint to run in any "real life" scenario, 
    /// it leaks internal system information that could potentially be a side channel to something.
    /// </summary>
    public class RateTestController : ApiController
    {
        /// <summary>
        /// A collection of "buckets" (keyed on user id strings),
        /// which keep track of rate limit quotas for the bucketed route.
        /// </summary>
        private static readonly Dictionary<string, ConcurrentQueue<int>> Buckets = new Dictionary<string, ConcurrentQueue<int>>();

        /// <summary>
        /// Unlimited API endpoint to serve as control variable.
        /// </summary>
        /// <returns>The current tick time.</returns>
        [Route("ratetest")]
        public int GetUnlimited()
        {
            return Environment.TickCount;
        }

        /// <summary>
        /// Specify constant rate delay in milliseconds.
        /// </summary>
        /// <param name="userToken">An API token identifying an arbitrary, unique user.</param>
        /// <param name="ms">Constant rate delay in milliseconds.</param>
        /// <returns>The current tick time if the request was ok, or an error string.</returns>
        [Route("ratetest/simple/{userToken}/{ms}")]
        public string GetSimple(string userToken, int ms)
        {
            var key = $"simple-{userToken}-{ms}";
            var val = HttpRuntime.Cache[key];

            // @todo deal w/ system clock precision
            if (val != null && unchecked((int)val - Environment.TickCount) > 0) 
            {
                return $"You must wait {ms} milliseconds between subsequent requests of this route.";
            }

            HttpRuntime.Cache.Add(
                key,
                Environment.TickCount + ms, 
                null, 
                DateTime.Now.AddSeconds(ms),
                Cache.NoSlidingExpiration,
                CacheItemPriority.Low,
                null);

            return Environment.TickCount.ToString();
        }

        /// <summary>
        /// Specify rate limiting using a "bucket" of allocated queries
        /// that replenish a specified time after consumption
        /// </summary>
        /// <param name="userToken">An API token identifying an arbitrary, unique user.</param>
        /// <param name="bucketSize">The maximum available queries at any moment.</param>
        /// <param name="lifetime">The time before a consumed query to replenishes into the bucket, in milliseconds.</param>
        /// <returns>The current tick time if the request was ok, or an error string.</returns>
        [Route("ratetest/bucketed/{userToken}/{bucketSize}/{lifetime}")]
        public string GetBucketed(string userToken, int bucketSize, int lifetime)
        {
            var key = $"bucketed-{userToken}-{bucketSize}-{lifetime}";
            ConcurrentQueue<int> expirationQueue;
            var currentTime = Environment.TickCount;
            if (!Buckets.TryGetValue(key, out expirationQueue))
            {
                expirationQueue = new ConcurrentQueue<int>();
                Buckets[key] = expirationQueue;
            }

            int expirationTime;
            while (expirationQueue.TryPeek(out expirationTime) &&
                   unchecked(expirationTime - currentTime) < 0)
            {
                int t;
                expirationQueue.TryDequeue(out t);
            }

            if (expirationQueue.Count > bucketSize)
            {
                return $"You may only make {bucketSize} queries every {lifetime} seconds.";
            }
            
            expirationQueue.Enqueue(currentTime + lifetime);

            return currentTime.ToString();
        }
    }
}

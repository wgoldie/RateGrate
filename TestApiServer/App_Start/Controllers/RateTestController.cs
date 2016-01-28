namespace TestApiServer.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Caching;
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
        private static readonly Dictionary<string, MemoryCache> Buckets = new Dictionary<string, MemoryCache>();

        /// <summary>
        /// Unlimited api endpoint to serve as control variable.
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
        /// <param name="ms">Constant rate delay in milliseconds.</param>
        /// <returns>The current tick time if the request was ok, or an error string.</returns>
        [Route("ratetest/simple/{ms}")]
        public string GetSimple(int ms)
        {
            const string userId = "placeholderUser";
            var key = string.Concat("ratetest/simple/{ms}", '-', userId);

            if (HttpRuntime.Cache[key] != null)
            {
                return $"You must wait {ms} milliseconds between subsequent requests of this route.";
            }

            HttpRuntime.Cache.Add(
                key,
                true, 
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
        /// <param name="bucketSize">The maximum available queries at any moment.</param>
        /// <param name="expiration">The time before a consumed query to replenishes into the bucket.</param>
        /// <returns>The current tick time if the request was ok, or an error string.</returns>
        [Route("ratetest/bucketed/{bucketSize}/{expiration}")]
        public string GetBucketed(int bucketSize, int expiration)
        {
            const string userId = "placeholderUser";
            MemoryCache c;
            if (!Buckets.TryGetValue(userId, out c))
            {
                c = new MemoryCache(userId);
                Buckets.Add($"{userId}-{bucketSize}-{expiration}", c);
            }

            var ct = c.GetCount();

            if (ct > bucketSize)
            {
                return string.Format(
                    "You may only make {0} queries every {1} seconds " +
                    "and you have made {2} in the last {1} seconds.", 
                    bucketSize, 
                    expiration,
                    ct);
            }

            c.Add(
                Guid.NewGuid().ToString(), 
                true, 
                DateTime.Now.AddSeconds(expiration));

            return Environment.TickCount.ToString();
        }
    }
}

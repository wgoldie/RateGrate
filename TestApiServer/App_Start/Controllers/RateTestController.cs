namespace TestApiServer.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Web.Http;

    /// <summary>
    /// A series of methods that will return the system tick time with various rate limits.
    /// This is probably a bad endpoint to run in any "real life" scenario, 
    /// it leaks internal system information that could potentially be a side channel to something.
    /// </summary>
    public class RateTestController : ApiController
    {
        /// <summary>
        /// A collection of lists keyed on rate limit strings,
        /// which keep track of rate limit quotas for the bucketed route.
        /// </summary>
        private static readonly Dictionary<string, List<int>> QueryLogs = new Dictionary<string, List<int>>();

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
        [Route("ratetest/get/simple/{userToken}/{ms}")]
        public int GetSimple(string userToken, int ms)
        {
            var tick = Environment.TickCount;
            var key = $"simple-{userToken}-{ms}";

            InsertQuery(key, tick);

            return tick;
        }

        /// <summary>
        /// Specify rate limiting using a "bucket" of allocated queries
        /// that replenish a specified time after consumption
        /// </summary>
        /// <param name="userToken">An API token identifying an arbitrary, unique user.</param>
        /// <param name="bucketSize">The maximum available queries at any moment.</param>
        /// <param name="lifetime">The time before a consumed query to replenishes into the bucket, in milliseconds.</param>
        /// <returns>The current tick time if the request was ok, or an error string.</returns>
        [Route("ratetest/get/bucketed/{userToken}/{bucketSize}/{lifetime}")]
        public int GetBucketed(string userToken, int bucketSize, int lifetime)
        {
            var tick = Environment.TickCount;

            string key = $"bucketed-{userToken}-{bucketSize}-{lifetime}";

            InsertQuery(key, tick);

            return tick;
        }

        /// <summary>
        /// Tests if the given bucketed route's rate limit was violated or not in the current logs.
        /// </summary>
        /// <param name="userToken">An API token identifying an arbitrary, unique user.</param>
        /// <param name="bucketSize">The maximum available queries at any moment.</param>
        /// <param name="lifetime">The time before a consumed query to replenishes into the bucket, in milliseconds.</param>
        /// <returns>A truth string if the route's rate limit was not violated, and false if it was not.</returns>
        [Route("ratetest/eval/bucketed/{userToken}/{bucketSize}/{lifetime}")]
        public string GetEvalBucketed(string userToken, int bucketSize, int lifetime)
        {
            string key = $"bucketed-{userToken}-{bucketSize}-{lifetime}";
            var sorted = QueryLogs[key].ConvertAll(i => i);
            sorted.Sort();

            for (var i = 0; i < sorted.Count - bucketSize; i++)
            {
                if (sorted[i + bucketSize] - sorted[i] < lifetime)
                {
                    return false.ToString();
                }
            }

            return true.ToString();
        }

        /// <summary>
        /// Clears the given rate limit's logs.
        /// </summary>
        /// <param name="userToken">An API token identifying an arbitrary, unique user.</param>
        /// <param name="bucketSize">The maximum available queries at any moment.</param>
        /// <param name="lifetime">The time before a consumed query to replenishes into the bucket, in milliseconds.</param>
        [Route("ratetest/clear/bucketed/{userToken}/{bucketSize}/{lifetime}")]
        public void GetClearBucketed(string userToken, int bucketSize, int lifetime)
        {
            string key = $"bucketed-{userToken}-{bucketSize}-{lifetime}";

            if (QueryLogs.ContainsKey(key))
            {
                QueryLogs[key].Clear();
            }
        }

        /// <summary>
        /// Clears the given rate limit's logs.
        /// </summary>
        /// <param name="userToken">An API token identifying an arbitrary, unique user.</param>
        /// <param name="ms">Constant rate delay in milliseconds.</param>
        [Route("ratetest/clear/simple/{userToken}/{ms}")]
        public void GetClearSimple(string userToken, int ms)
        {
            var key = $"simple-{userToken}-{ms}";
            if (QueryLogs.ContainsKey(key))
            {
                QueryLogs[key].Clear();
            }
        }

        /// <summary>
        /// Tests if the given bucketed route's rate limit was violated or not in the current logs.
        /// </summary>
        /// <param name="userToken">An API token identifying an arbitrary, unique user.</param>
        /// <param name="ms">Constant rate delay in milliseconds.</param>
        /// <returns>A truth string if the route's rate limit was not violated, and false if it was not.</returns>
        [Route("ratetest/eval/simple/{userToken}/{ms}")]
        public string GetEvalSimple(string userToken, int ms)
        {
            var key = $"simple-{userToken}-{ms}";
            if (!QueryLogs.ContainsKey(key))
            {
                return true.ToString();
            }

            var sorted = QueryLogs[key].ConvertAll(i => i);
            sorted.Sort();

            for (var i = 0; i < sorted.Count - 1; i++)
            {
                if (sorted[i + 1] - sorted[i] < ms)
                {
                    return false.ToString();
                }
            }

            return true.ToString();
        }

        /// <summary>
        /// Logs a query at the specified system tick time to the given key.
        /// </summary>
        /// <param name="key">The key for the log to save the time to.</param>
        /// <param name="tick">The tick time in milliseconds from the system environment.</param>
        private static void InsertQuery(string key, int tick)
        {
            if (!QueryLogs.ContainsKey(key))
            {
                QueryLogs[key] = new List<int>();
            }

            QueryLogs[key].Add(tick);
        }
    }
}

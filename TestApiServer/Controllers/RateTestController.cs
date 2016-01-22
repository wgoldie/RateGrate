using System.Web.Http;
using TestApiServer.Utilities;

namespace TestApiServer.Controllers
{
    /// <summary>
    /// A series of methods that will return the system tick time with various rate limits.
    /// This is probably a bad endpoint to run in any "real life" scenario, 
    /// it leaks internal system information that could potentially be a side channel to something.
    /// </summary>
    public class RateTestController : ApiController
    {
        /// <summary>
        /// Unlimited api endpoint to serve as control variable.
        /// </summary>
        /// <returns>The current time.</returns>
        /// 
        [Route("ratetest")]
        public int GetUnlimited()
        {
            return System.Environment.TickCount;
        }

        /// <summary>
        /// Specify constant rate delay in milliseconds.
        /// </summary>
        /// <param name="ms">Constant rate delay in milliseconds.</param>
        /// <returns>The current time if the request was ok.</returns>
        [Route("ratetest/limited")]
        [Throttle(Name = "TestThrottle", Message = "You must wait {n} seconds before accessing this url again.", Seconds = 5)]
        public int GetLimited()
        {
            return System.Environment.TickCount;
        }
    }
}

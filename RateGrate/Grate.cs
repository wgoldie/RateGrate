namespace RateGrate
{
    /// <summary>
    /// A Grate that will allow a specified number of queries 
    /// during any rate limit period of specified length.
    /// </summary>
    public abstract class Grate
    {
        /// <summary>
        /// Waits until it an API query is allowed.
        /// @todo add mechanism for handover to release (task, token, etc) 
        /// @todo to prevent concurrency issues.
        /// </summary>
        public abstract void Wait();

        /// <summary>
        /// Signifies that an API query has been made.
        /// </summary>
        public abstract void Release();
    }
}

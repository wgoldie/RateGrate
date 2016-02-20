namespace RateGrate
{
    using System.Threading.Tasks;

    /// <summary>
    /// A Grate that will allow a specified number of queries 
    /// during any rate limit period of specified length.
    /// </summary>
    /// <typeparam name="T">The type of API/user token this Grate works with.</typeparam>
    public abstract class Grate<T>
    {
        /// <summary>
        /// Waits for API availability for the specified token.
        /// </summary>
        /// <param name="token">A representation of an individual API token to wait for availability on.</param>
        public abstract void Wait(T token);

        /// <summary>
        /// Signifies that an API query has been made.
        /// </summary>
        /// <param name="token">A representation of an individual API token to wait for availability on.</param>
        public abstract void Release(T token);

        /// <summary>
        /// Runs an API query action as soon as a slot is available for it.
        /// </summary>
        /// <typeparam name="TR">The result type of the API query action.</typeparam>
        /// <param name="token">A representation an an individual API token to wait for availability on.</param>
        /// <param name="action">The query action to run.</param>
        /// <returns>The result of the query.</returns>
        public async Task<TR> WaitAndRun<TR>(T token, Task<TR> action)
        {
            Wait(token);
            var result = await action;
            Release(token);
            return result;
        }
    }
}

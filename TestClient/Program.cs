namespace TestClient
{
    using System;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods to test the api server.
    /// @TODO: use actual testing framework.
    /// Potentially can't use MSTest because the tests need to be run as part of project startup.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// The root of the uri used to connect to the test api server.
        /// </summary>
        private const string BaseUri = "http://localhost:5555";

        /// <summary>
        /// Runs all tests.
        /// </summary>
        private static void Main()
        {
            Test(TestBase, "Base");
            Test(TestSimple, "Simple");
            Test(TestBucketed, "Bucketed");

            Console.ReadLine();
        }

        /// <summary>
        /// Runs a given test method and reports the results in the console.
        /// </summary>
        /// <param name="testMethod">The method to run.</param>
        /// <param name="testName">The name for this test to be printed when reporting results.</param>
        private static void Test(Func<Task<bool>> testMethod, string testName)
        {
            var test = testMethod();
            test.Wait();
            Console.Write(test.Result ? $"{testName} passed\n" : $"{testName} failed\n");
        }

        /// <summary>
        /// Attempts to query a given route.
        /// </summary>
        /// <param name="route">The route to query</param>
        /// <returns>True iff the query returned a parsable integer string.</returns>
        private static async Task<bool> TryGetRoute(string route)
        {
            using (HttpClient client = new HttpClient())
            {
                int j;
                return int.TryParse(
                    (await client.GetAsync(requestUri: new Uri(BaseUri + route))
                        .Result.Content.ReadAsStringAsync())
                        .Replace("\"", string.Empty),
                    out j);
            }
        }

        /// <summary>
        /// Tests the base api endpoint.
        /// </summary>
        /// <returns>Whether or not the test was successful.</returns>
        private static async Task<bool> TestBase()
        {
            const string route = "/ratetest";
            await TryGetRoute(route);
            return await TryGetRoute(route);
        }

        /// <summary>
        /// Tests the simple api endpoint.
        /// </summary>
        /// <returns>Whether or not the test was successful.</returns>
        private static async Task<bool> TestSimple()
        {
            const int timeout = 2;
            var route = $"/ratetest/simple/{timeout}";
            await TryGetRoute(route);
            if (await TryGetRoute(route))
            {
                return false;
            }

            await Task.Delay(TimeSpan.FromSeconds(timeout));
            return await TryGetRoute(route);
        }

        /// <summary>
        /// Tests the bucketed api endpoint.
        /// </summary>
        /// <returns>Whether or not the test was successful.</returns>
        private static async Task<bool> TestBucketed()
        {
            const int bucketSize = 5;
            const int expirationTime = 1;
            var route = $"/ratetest/bucketed/{bucketSize}/{expirationTime}";

            var sw = new Stopwatch();
            sw.Start();
            for (var i = 0; i < bucketSize; i++)
            {
                if (!await TryGetRoute(route))
                {
                    return false;
                }
            }

            await TryGetRoute(route);
            await TryGetRoute(route);
            await TryGetRoute(route);

            if (sw.Elapsed < TimeSpan.FromSeconds(expirationTime)
                && await TryGetRoute(route))
            {
                return false;
            }

            await Task.Delay(expirationTime * 1000);

            return await TryGetRoute(route);
        }
    }
}

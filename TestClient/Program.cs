namespace TestClient
{
    using System;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading.Tasks;
    using RateGrate;
    using TestResult = System.Threading.Tasks.Task<System.Tuple<bool, string>>;

    /// <summary>
    /// Provides methods to test the API server.
    /// @TODO: use actual testing framework.
    /// Potentially can't use MSTest because the tests need to be run as part of project startup.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// The root of the uri used to connect to the test API server.
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

            Test(TestQuotaGrateWithSimple, "QuotaGrate - Simple");
            Test(TestQuotaGrateWithBucketed, "QuotaGrate - Bucketed");
            Test(TestWaitAndRun, "Wait and Run");
            Console.ReadLine();
        }

        /// <summary>
        /// Runs a given test method and reports the results in the console.
        /// </summary>
        /// <param name="testMethod">The method to run.</param>
        /// <param name="testName">The name for this test to be printed when reporting results.</param>
        private static void Test(Func<TestResult> testMethod, string testName)
        {
            var test = testMethod();
            test.Wait();
            Console.Write(test.Result.Item1 ? 
                $"{testName} passed with response {test.Result.Item2}\n" : 
                $"{testName} failed with response {test.Result.Item2}\n");
        }

        /// <summary>
        /// Attempts to query a given route.
        /// </summary>
        /// <param name="route">The route to query</param>
        /// <returns>True if/only if the query returned an integer string, and the response regardless.</returns>
        private static async TestResult TryGetRoute(string route)
        {
            using (HttpClient client = new HttpClient())
            {
                int j;
                var response = (await client.GetAsync(requestUri: new Uri(BaseUri + route))
                    .Result.Content.ReadAsStringAsync())
                    .Replace("\"", string.Empty);
                var valid = int.TryParse(response, out j);
                return Tuple.Create(valid, response);
            }
        }

        /// <summary>
        /// Tests the base API endpoint.
        /// </summary>
        /// <returns>Whether or not the test was successful along with the response.</returns>
        private static async TestResult TestBase()
        {
            const string route = "/ratetest";
            await TryGetRoute(route);
            return await TryGetRoute(route);
        }

        /// <summary>
        /// Tests the simple API endpoint.
        /// </summary>
        /// <returns>Whether or not the test was successful along with the response.</returns>
        private static async TestResult TestSimple()
        {
            const int timeout = 500;
            var route = $"/ratetest/simple/TestSimple/{timeout}";
            await TryGetRoute(route);
            if ((await TryGetRoute(route)).Item1)
            {
                return Tuple.Create(false, "Request was not rate limited as expected.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(timeout));
            return await TryGetRoute(route);
        }

        /// <summary>
        /// Tests the bucketed API endpoint.
        /// </summary>
        /// <returns>Whether or not the test was successful along with the response.</returns>
        private static async TestResult TestBucketed()
        {
            const int bucketSize = 5;
            const int expirationTime = 5000;
            var route = $"/ratetest/bucketed/TestBucketed/{bucketSize}/{expirationTime}";

            var sw = new Stopwatch();
            sw.Start();
            for (var i = 0; i < bucketSize; i++)
            {
                var rateLimitedResponse = await TryGetRoute(route);
                if (!rateLimitedResponse.Item1)
                {
                    return rateLimitedResponse;
                }
            }

            await TryGetRoute(route);
            await TryGetRoute(route);
            await TryGetRoute(route);
            await TryGetRoute(route);

            if (sw.Elapsed.Milliseconds < expirationTime
                && (await TryGetRoute(route)).Item1)
            {
                return Tuple.Create(false, "Request was not rate limited as expected.");
            }

            await Task.Delay(expirationTime);

            return await TryGetRoute(route);
        }

        /// <summary>
        /// Tests the Quota grate with the basic API endpoint.
        /// </summary>
        /// <returns>Whether or not the test was successful along with the response.</returns>
        private static async TestResult TestQuotaGrateWithSimple()
        {
            const int timeout = 100;
            var route = $"/ratetest/simple/TestQuotaGrateWithSimple/{timeout}";
            var token = "demoToken";

            var grate = new QuotaGrate<string>(1, TimeSpan.FromMilliseconds(timeout));

            for (int i = 0; i < 10; i++)
            {
                grate.Wait(token);
                var response = await TryGetRoute(route);
                if (!response.Item1)
                {
                    return Tuple.Create(false, $"@{i}: {response.Item2}");
                }

                grate.Release(token);
            }

            return Tuple.Create(true, "[none]");
        }

        /// <summary>
        /// Tests the Quota grate with the bucketed API endpoint.
        /// </summary>
        /// <returns>Whether or not the test was successful along with the response.</returns>
        private static async TestResult TestQuotaGrateWithBucketed()
        {
            const int bucketSize = 5;
            const int expirationTime = 5000;
            var route = $"/ratetest/bucketed/TestQuotaGrateWithBucketed/{bucketSize}/{expirationTime}";
            var token = "demoToken";

            var grate = new QuotaGrate<string>(bucketSize, TimeSpan.FromMilliseconds(expirationTime));

            for (var i = 0; i < 10; i++)
            {
                grate.Wait(token);

                var response = await TryGetRoute(route);
                if (!response.Item1)
                {
                    return Tuple.Create(false, $"@{i}: {response.Item2}");
                }

                grate.Release(token);
            }

            return Tuple.Create(true, "[none]");
        }

        /// <summary>
        /// Tests the WaitAndRun function with a QuotaGrate and the bucketed API endpoint.
        /// </summary>
        /// <returns>Whether or not the test was successful along with the response.</returns>
        private static async TestResult TestWaitAndRun()
        {
            const int bucketSize = 5;
            const int expirationTime = 5000;
            var route = $"/ratetest/bucketed/TestWaitAndRun/{bucketSize}/{expirationTime}";
            var token = "demoToken";
            var grate = new QuotaGrate<string>(bucketSize, TimeSpan.FromMilliseconds(expirationTime));

            for (var i = 0; i < 10; i++)
            {
                var response = await grate.WaitAndRun(token, TryGetRoute(route));

                if (!response.Item1)
                {
                    return Tuple.Create(false, $"@{i}: {response.Item2}");
                }
            }

            return Tuple.Create(true, "[none]");
        }
    }
}

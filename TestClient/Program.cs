namespace TestClient
{
    using System;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading.Tasks;
    using RateGrate;
    using TestResult = System.Tuple<bool, string>;

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
            Console.Write(test.Item1 ? 
                $"{testName} passed with response {test.Item2}\n" : 
                $"{testName} failed with response {test.Item2}\n");
        }

        /// <summary>
        /// Queries the given route.
        /// </summary>
        /// <param name="route">The route to query</param>
        /// <returns>The API's response.</returns>
        private static string Get(string route)
        {
            using (var client = new HttpClient())
            {
                return client.GetAsync(requestUri: new Uri(BaseUri + route))
                    .Result.Content.ReadAsStringAsync()
                    .Result.Replace("\"", string.Empty);
            }
        }

        /// <summary>
        /// Queries the given route and tests if the response is an integer.
        /// </summary>
        /// <param name="route">The route to query</param>
        /// <returns>True if/only if the query returned an integer string, and the response regardless.</returns>
        private static TestResult GetIsInt(string route)
        {
            int j;
            var response = Get(route);
            var valid = int.TryParse(response, out j);
            return Tuple.Create(valid, response);
        }

        /// <summary>
        /// Queries the given route and tests if the response is a boolean AND if the response is true.
        /// </summary>
        /// <param name="route">The route to query</param>
        /// <returns>True if/only if the query returned an boolean string, and that string was true;
        ///  and the response regardless.
        /// </returns>
        private static TestResult GetAsBool(string route)
        {
            bool j;
            var response = Get(route);
            var valid = bool.TryParse(response, out j);
            return Tuple.Create(valid && j, response);
        }

        /// <summary>
        /// Tests the base API endpoint.
        /// </summary>
        /// <returns>Whether or not the test was successful along with the response.</returns>
        private static TestResult TestBase()
        {
            const string route = "/ratetest";
            GetIsInt(route);
            return GetIsInt(route);
        }

        /// <summary>
        /// Tests the simple API endpoint.
        /// </summary>
        /// <returns>Whether or not the test was successful along with the response.</returns>
        private static TestResult TestSimple()
        {
            const int timeout = 500;
            var getRoute = $"/ratetest/get/simple/TestSimple/{timeout}";
            var clearRoute = $"/ratetest/clear/simple/TestSimple/{timeout}";
            var evalRoute = $"/ratetest/eval/simple/TestSimple/{timeout}";

            Get(clearRoute);

            var startTick = Environment.TickCount;
            Get(getRoute);
            Get(getRoute);
            var eval1 = GetAsBool(evalRoute);
            var endTick = Environment.TickCount;

            if (endTick - startTick > timeout)
            {
                return Tuple.Create(false, "Test did not run fast enough to evaluate, try increasing timeout.");
            }

            if (eval1.Item1)
            {
                return Tuple.Create(false, "Request was not rate limited as expected.");
            }

            Get(clearRoute);

            Get(getRoute);
            Task.Delay(TimeSpan.FromMilliseconds(timeout)).Wait();
            Get(getRoute);
            return GetAsBool(evalRoute);
        }

        /// <summary>
        /// Tests the bucketed API endpoint.
        /// </summary>
        /// <returns>Whether or not the test was successful along with the response.</returns>
        private static TestResult TestBucketed()
        {
            const int bucketSize = 5;
            const int expirationTime = 500;
            var getRoute = $"/ratetest/get/bucketed/TestBucketed/{bucketSize}/{expirationTime}";
            var evalRoute = $"/ratetest/eval/bucketed/TestBucketed/{bucketSize}/{expirationTime}";
            var clearRoute = $"/ratetest/clear/bucketed/TestBucketed/{bucketSize}/{expirationTime}";

            Get(clearRoute);

            var sw = new Stopwatch();
            sw.Start();

            for (var i = 0; i < bucketSize; i++)
            {
                Get(getRoute);
            }

            if (sw.ElapsedMilliseconds > expirationTime)
            {
                return Tuple.Create(false, "Test did not run fast enough to evaluate, try increasing timeout.");
            }

            var eval1 = GetAsBool(evalRoute);

            if (!eval1.Item1)
            {
                return Tuple.Create(false, "Request was rate limited before bucket was filled. " + eval1.Item2);
            }

            Get(clearRoute);
            sw.Restart();

            for (var i = 0; i < bucketSize + 1; i++)
            {
                Get(getRoute);
            }

            if (sw.ElapsedMilliseconds > expirationTime)
            {
                return Tuple.Create(false, "Test did not run fast enough to evaluate, try increasing timeout.");
            }

            var eval2 = GetAsBool(evalRoute);

            if (eval2.Item1)
            {
                return Tuple.Create(false, "Request was not rate limited as expected.");
            }

            sw.Stop();
            Get(clearRoute);

            for (var i = 0; i < bucketSize; i++)
            {
                Get(getRoute);
            }

            Task.Delay(expirationTime).Wait();
            GetIsInt(getRoute);

            return GetAsBool(evalRoute);
        }

        /// <summary>
        /// Tests the Quota grate with the basic API endpoint.
        /// </summary>
        /// <returns>Whether or not the test was successful along with the response.</returns>
        private static TestResult TestQuotaGrateWithSimple()
        {
            const int timeout = 500;
            var getRoute = $"/ratetest/get/simple/TestQuotaGrateWithSimple/{timeout}";
            var evalRoute = $"/ratetest/eval/simple/TestQuotaGrateWithSimple/{timeout}";
            var clearRoute = $"/ratetest/clear/simple/TestQuotaGrateWithSimple/{timeout}";
            var token = "demoToken";

            Get(clearRoute);
            var grate = new QuotaGrate<string>(1, TimeSpan.FromMilliseconds(timeout));
            
            for (var i = 0; i < 10; i++)
            {
                grate.Wait(token);
                Get(getRoute);
                grate.Release(token);
            }

            // @todo timing check here
            return GetAsBool(evalRoute);
        }

        /// <summary>
        /// Tests the Quota grate with the bucketed API endpoint.
        /// </summary>
        /// <returns>Whether or not the test was successful along with the response.</returns>
        private static TestResult TestQuotaGrateWithBucketed()
        {
            const int bucketSize = 5;
            const int expirationTime = 5000;
            var getRoute = $"/ratetest/get/bucketed/TestQuotaGrateWithBucketed/{bucketSize}/{expirationTime}";
            var evalRoute = $"/ratetest/eval/bucketed/TestQuotaGrateWithBucketed/{bucketSize}/{expirationTime}";
            var clearRoute = $"/ratetest/clear/bucketed/TestQuotaGrateWithBucketed/{bucketSize}/{expirationTime}";
            var token = "demoToken";

            Get(clearRoute);
            var grate = new QuotaGrate<string>(bucketSize, TimeSpan.FromMilliseconds(expirationTime));
            
            for (var i = 0; i < 10; i++)
            {
                grate.Wait(token);
                Get(getRoute);
                grate.Release(token);
            }

            // @todo timing check here
            return GetAsBool(evalRoute);
        }

        /// <summary>
        /// Tests the WaitAndRun function with a QuotaGrate and the bucketed API endpoint.
        /// </summary>
        /// <returns>Whether or not the test was successful along with the response.</returns>
        private static TestResult TestWaitAndRun()
        {
            const int bucketSize = 5;
            const int expirationTime = 5000;
            var getRoute = $"/ratetest/get/bucketed/TestWaitAndRun/{bucketSize}/{expirationTime}";
            var evalRoute = $"/ratetest/eval/bucketed/TestWaitAndRun/{bucketSize}/{expirationTime}";
            var clearRoute = $"/ratetest/clear/bucketed/TestWaitAndRun/{bucketSize}/{expirationTime}";

            Get(clearRoute);
            var grate = new QuotaGrate<string>(bucketSize, TimeSpan.FromMilliseconds(expirationTime));
            var token = "demoToken";

            for (var i = 0; i < 10; i++)
            {
                grate.WaitAndRun(token, new Task<string>(() => Get(getRoute))).Wait();
            }

            // @todo timing check here
            return GetAsBool(evalRoute);
        }
    }
}

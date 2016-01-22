using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace TestClient
{
    /// <summary>
    /// @todo: use actual testing framework.
    /// Potentially can't use MSTest because the tests need to be run as part of project startup.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            var testBase = TestBase();
            var testLimited = TestLimited();
            Console.Write("Testing base endpoint...");
            testBase.Wait();
            Console.WriteLine(testBase.Result ? "Base passed." : "Base failed.");
            Console.Write("Testing limited endpoint...");
            testLimited.Wait();
            Console.WriteLine(testLimited.Result ? "Limited passed." : "Limited failed.");
            Console.ReadLine();
        }

        private const string BaseUri = "http://localhost:5555";

        private static async Task<bool> TestBase()
        {
            using (var client = new HttpClient())
            {
                await client.GetAsync(new Uri(BaseUri + "/ratetest/5"));
                var response = await client.GetAsync(new Uri(BaseUri + "/ratetest"));
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Base returned {0}", content);
                int i;
                return int.TryParse(content, out i);
            }
        }

        private static async Task<bool> TestLimited()
        {
            using (var client = new HttpClient())
            {
                await client.GetAsync(new Uri(BaseUri + "/ratetest/limited"));
                var response = await client.GetAsync(new Uri(BaseUri + "/ratetest/limited"));
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Limited returned {0}", content);
                return content.Equals("You must wait 5 seconds before accessing this url again.");
            }
        }
    }
}

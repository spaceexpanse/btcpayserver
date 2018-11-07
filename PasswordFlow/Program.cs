using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PasswordFlow
{
    /// <summary>
    /// This example connects to btcpayserver through username and password and gives you an access token to use
    /// </summary>
    public class Program
    {
        public static void Main(string[] args) => MainAsync(args).GetAwaiter().GetResult();

        public static async Task MainAsync(string[] args)
        {
            var client = new HttpClient();

            Console.Write("Enter btcpayserver user:");
            var email = Console.ReadLine();
            
            Console.Write("Enter btcpayserver password:");
            var password = Console.ReadLine();
            
            var token = await GetTokenAsync(client, email, password);
            Console.WriteLine("Access token: {0}", token);
            Console.WriteLine();

            var resource = await GetResourceAsync(client, token);
            Console.WriteLine("API response: {0}", resource);

            Console.ReadLine();
        }


        private static async Task<string> GetTokenAsync(HttpClient client, string email, string password)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:14142/connect/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "password", ["username"] = email, ["password"] = password
                })
            };

            var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            response.EnsureSuccessStatusCode();

            var payload = JObject.Parse(await response.Content.ReadAsStringAsync());
            if (payload["error"] != null)
            {
                throw new InvalidOperationException("An error occurred while retrieving an access token.");
            }

            return (string) payload["access_token"];
        }

        private static async Task<string> GetResourceAsync(HttpClient client, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:14142/api/v1.0/test");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
    }
}
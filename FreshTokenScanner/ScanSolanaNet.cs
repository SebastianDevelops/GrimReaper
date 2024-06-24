using RestSharp;
using System.Collections.Generic;
using System.Configuration;
using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace FreshTokenScanner
{
    public class ScanSolanaNet
    {
        private readonly string? _apiUrl;


        public ScanSolanaNet()
        {
            _apiUrl = ConfigurationManager.AppSettings["api_rugCheck"];
        }

        /// <summary>
        /// Gets the latest coins from Solana network
        /// </summary>
        /// <param name="limit"></param>
        /// <returns>List of latest 10 Solana coins</returns>
        /// <exception cref="Exception"></exception>
        public async Task<List<string>> GetPreLaunchCoins()
        {
            if(!string.IsNullOrWhiteSpace(_apiUrl))
            {
                var preLaunchAddressList = new List<string>();

                string reqUrl = $"{_apiUrl.TrimEnd('/')}/v1/stats/new_tokens";

                var options = new RestClientOptions(reqUrl);
                var client = new RestClient(options);
                var request = new RestRequest("");
                var response = await client.GetAsync(request);

                if(response.Content != null)
                {
                    return ExtractCoinsAddress(response.Content);
                }
                else
                {
                    Console.WriteLine("Response from api was null/empty");
                    return preLaunchAddressList;
                }
            }
            else { 
                Console.WriteLine("Required auth values not found");
                return new List<string>();
            }
        }

        private List<string> ExtractCoinsAddress(string rawAddress)
        {
            List<string> preLaunchAddressList = new List<string>();

            using (JsonDocument document = JsonDocument.Parse(rawAddress))
            {
                JsonElement root = document.RootElement;

                foreach (JsonElement token in root.EnumerateArray())
                {
                    if (token.TryGetProperty("mint", out JsonElement addressElement) && addressElement.GetString() is string address)
                    {
                        if (!address.ToLower().Contains("pump"))
                        {
                            preLaunchAddressList.Add(address);
                        }
                    }
                }
            }

            return preLaunchAddressList;
        }
    }
}

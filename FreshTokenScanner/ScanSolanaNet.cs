using RestSharp;
using System.Collections.Generic;
using System.Configuration;
using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace FreshTokenScanner
{
    public class ScanSolanaNet
    {
        private readonly string? _apiKey;
        private readonly string? _apiUrl;
        private int _limit = default;
        private int _offset = default; 


        public ScanSolanaNet()
        {
            _apiKey = ConfigurationManager.AppSettings["birdeye_token"];
            _apiUrl = ConfigurationManager.AppSettings["birdeye_url"];
        }

        /// <summary>
        /// Gets the latest coins from Solana network
        /// </summary>
        /// <param name="limit"></param>
        /// <returns>List of latest 50 Solana coins</returns>
        /// <exception cref="Exception"></exception>
        public async Task<List<string>> GetPreLaunchCoins(int limit = 50)
        {
            if(limit > 50)
            {
                throw new Exception("Limit can not exceed 50");
            }

            if(!string.IsNullOrWhiteSpace(_apiKey) && !string.IsNullOrWhiteSpace(_apiUrl))
            {
                var preLaunchAddressList = new List<string>();
                _limit = 50;

                string reqUrl = $"{_apiUrl}defi/tokenlist?sort_by=v24hUSD&sort_type=desc&offset={_offset}&limit={limit}";
                reqUrl = await UpdateUrlOffset(reqUrl);

                if(reqUrl == string.Empty)
                {
                    throw new Exception("Offset value was not set successfully");
                }

                var options = new RestClientOptions(reqUrl);
                var client = new RestClient(options);
                var request = new RestRequest("");
                request.AddHeader("X-API-KEY", _apiKey);
                var response = await client.GetAsync(request);

                if(response.Content != null)
                {
                    return ExtractCoinsAddress(response.Content);
                }
                else
                {
                    throw new Exception("Response from api was null/empty");
                }
            }
            else
            {
                throw new Exception("Required auth values not found");
            }
        }

        /// <summary>
        /// Updates the api url link to have the latest offset values. 
        /// </summary>
        /// <param name="reqUrl"></param>
        /// <returns>Api Url with updated offset</returns>
        private async Task<string> UpdateUrlOffset(string reqUrl)
        {
            var options = new RestClientOptions(reqUrl);
            var client = new RestClient(options);
            var request = new RestRequest("");
            request.AddHeader("X-API-KEY", _apiKey);
            var response = await client.GetAsync(request);

            using (var resBody = JsonDocument.Parse(response.Content))
            {
                JsonElement root = resBody.RootElement;
                JsonElement offsetAmt = root.GetProperty("data").GetProperty("total");

                if(int.TryParse(offsetAmt.ToString(), out _offset))
                {
                    _offset = _offset - 51;
                    return $"{_apiUrl}defi/tokenlist?sort_by=v24hUSD&sort_type=desc&offset={_offset}&limit={_limit}";
                }
            }
            return String.Empty;
        }

        private List<string> ExtractCoinsAddress(string rawAddress)
        {
            List<string> preLaunchAddressList = new List<string>();
            using (JsonDocument document = JsonDocument.Parse(rawAddress))
            {
                JsonElement root = document.RootElement;
                JsonElement tokensElement = root.GetProperty("data").GetProperty("tokens");

                foreach (JsonElement token in tokensElement.EnumerateArray())
                {
                    if (token.TryGetProperty("address", out JsonElement addressElement) && addressElement.GetString() is string address)
                    {
                        preLaunchAddressList.Add(address);
                    }
                }
            }
            return preLaunchAddressList;
        }
    }
}

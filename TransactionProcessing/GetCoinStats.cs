﻿using RestSharp;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http.Headers;
using System.Text.Json;

namespace TransactionProcessing
{
    public class GetCoinStats
    {
        private readonly string? _apiKey;
        private readonly string? _apiUrl;
        private readonly string? _rugUrl;

        public GetCoinStats()
        {
            _apiKey = ConfigurationManager.AppSettings["birdeye_token"];
            _apiUrl = ConfigurationManager.AppSettings["birdeye_url"];
            _rugUrl = ConfigurationManager.AppSettings["api_rugCheck"];
        }

        public async Task<decimal> MarketCap(string mintAddress)
        {
            ulong circulatingSupply = await CirculatingSupply(mintAddress);
            decimal currentPrice = await CurrentPrice(mintAddress);
            decimal mc = default;

            if (currentPrice > 0 && circulatingSupply > 0)
            {
                mc = currentPrice * circulatingSupply;
            }

            return mc;
        }

        public async Task<decimal> CurrentPrice(string mintAddress)
        {
            decimal currentPrice = default;

            if (!String.IsNullOrEmpty(_apiUrl))
            {
                string reqUrl = $"{_apiUrl}defi/price?address={mintAddress}";

                var options = new RestClientOptions(reqUrl);
                var client = new RestClient(options);
                var request = new RestRequest("");
                request.AddHeader("X-API-KEY", _apiKey);

                var response = await client.GetAsync(request);

                var responseMsg = response.Content;

                if (!String.IsNullOrEmpty(responseMsg))
                {
                    using (JsonDocument document = JsonDocument.Parse(responseMsg))
                    {
                        JsonElement root = document.RootElement;
                        
                        if (root.TryGetProperty("data", out JsonElement data) && data.ValueKind != JsonValueKind.Null)
                            data.GetProperty("value").TryGetDecimal(out currentPrice);
                    }
                }
            }

            return currentPrice;
        }

        public async Task<UInt64> CirculatingSupply(string mintAddress)
        {
            UInt64 supply = default;
            try
            {
                if (!String.IsNullOrEmpty(_rugUrl))
                {
                    string reqUrl = $"{_rugUrl.TrimEnd('/')}/v1/tokens/{mintAddress}/report";

                    var options = new RestClientOptions(reqUrl);
                    var client = new RestClient(options);
                    var request = new RestRequest("");

                    var response = await client.GetAsync(request);

                    var responseMsg = response.Content;

                    if (!String.IsNullOrEmpty(responseMsg))
                    {
                        using (JsonDocument document = JsonDocument.Parse(responseMsg))
                        {
                            JsonElement root = document.RootElement;

                            var tokenInfo = root.GetProperty("token");

                            if (tokenInfo.ValueKind != JsonValueKind.Null)
                                supply = tokenInfo.GetProperty("supply").GetUInt64();
                        }
                    }
                }

                return supply;
            }
            catch (Exception)
            {
                return supply;
            }
        }
    }
}

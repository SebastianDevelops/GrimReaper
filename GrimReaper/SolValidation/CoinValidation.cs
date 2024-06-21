using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using String = System.String;

namespace GrimReaper.SolValidation
{
    public class CoinValidation
    {
        private readonly string? _apiRugChecker;

        public CoinValidation()
        {
            _apiRugChecker = ConfigurationManager.AppSettings["api_rugCheck"];
        }

        public async Task<bool> IsCoinValidAsync(string mintAddress)
        {
            string rugBodyResult = await GetLiquidityPriceAsync(mintAddress);
            if (!String.IsNullOrEmpty(rugBodyResult))
            {
                using JsonDocument document = JsonDocument.Parse(rugBodyResult);
                JsonElement root = document.RootElement;
                if (root.TryGetProperty("markets", out JsonElement markets) && markets.ValueKind == JsonValueKind.Array && markets.GetArrayLength() > 0)
                {
                    JsonElement firstMarket = markets[0];
                    JsonElement lp = firstMarket.GetProperty("lp");

                    double basePrice = lp.GetProperty("basePrice").GetDouble();
                    double liquidityPrice = lp.GetProperty("lpLockedUSD").GetDouble();
                    double roundedLiquidityPrice = Math.Round(liquidityPrice, 1, MidpointRounding.AwayFromZero);

                    int score = root.GetProperty("score").GetInt32();
                    string? freezeAuthority = root.GetProperty("freezeAuthority").GetString();
                    bool hasFreezeAuthority = !String.IsNullOrEmpty(freezeAuthority);

                    return basePrice == 0 && score < 20410 && roundedLiquidityPrice >= 1000 && !hasFreezeAuthority;
                }
            }
            return false;
        }

        private async Task<string> GetLiquidityPriceAsync(string mintAddress)
        {
            string responseMsg = String.Empty;
            
            if(!String.IsNullOrEmpty(_apiRugChecker))
            {
                string fullUrl = $"{_apiRugChecker.TrimEnd('/')}/v1/tokens/{mintAddress}/report";
                using HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage response = await client.GetAsync(fullUrl);
                response.EnsureSuccessStatusCode();
                responseMsg = await response.Content.ReadAsStringAsync();
            }
            else
            {
                throw new Exception("Rug check api url is null");
            }

            return responseMsg;
        }

    }
}

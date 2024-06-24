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
        const string hotAuthAddress = "TSLvdd1pWpHVjahSpsvCXUbgwsL3JAcvokwaKt1eokM";

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

                var tokenMeta = root.GetProperty("tokenMeta");
                string? tokenAuthAddress = tokenMeta.GetProperty("updateAuthority").GetString();

                int score = root.GetProperty("score").GetInt32();

                root.TryGetProperty("freezeAuthority", out JsonElement freezeAuth);
                root.TryGetProperty("mintAuthority", out JsonElement mintAuth);


                bool hasFreezeAuthority = freezeAuth.ValueKind != JsonValueKind.Null;
                bool hasMintAuthority = mintAuth.ValueKind != JsonValueKind.Null;

                return score < 20410 && !hasMintAuthority && !hasFreezeAuthority;

            }
            return false;
        }

        private async Task<string> GetLiquidityPriceAsync(string mintAddress)
        {
            string responseMsg = String.Empty;

            if (!String.IsNullOrEmpty(_apiRugChecker))
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
                Console.WriteLine("Rug check api url is null");
            }

            return responseMsg;
        }

        public async Task<bool> IsPumpFun(string mintAddress)
        {
            string rugBodyResult = await GetLiquidityPriceAsync(mintAddress);
            if (!String.IsNullOrEmpty(rugBodyResult))
            {

                using JsonDocument document = JsonDocument.Parse(rugBodyResult);
                JsonElement root = document.RootElement;

                var tokenMeta = root.GetProperty("tokenMeta");
                string? tokenAuthAddress = tokenMeta.GetProperty("updateAuthority").GetString();

                return hotAuthAddress == tokenAuthAddress;
            }
            return false;
        }

    }
}

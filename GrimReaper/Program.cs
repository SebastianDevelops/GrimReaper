using FreshTokenScanner;
using System.Collections.Concurrent;
using System.Configuration;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using TelegramBot;
using TL;
using WTelegram;

class Program
{
    private readonly string? _apiId;
    private readonly string? _apiHash;
    private readonly string? _phoneNumber;
    private readonly string? _apiLiquidity;

    private static readonly Program _program = new Program();
    private static readonly ScanSolanaNet _scanSolana = new ScanSolanaNet();
    private static readonly MrMeeSeeks _mrMeeSeeks = new MrMeeSeeks();

    private static readonly ConcurrentDictionary<string, bool> _invalidMintAddresses = new ConcurrentDictionary<string, bool>();
    private static readonly ConcurrentDictionary<string, bool> _validMintAddresses = new ConcurrentDictionary<string, bool>();
    private static List<string> _mintAddresses = new List<string>();

    public Program()
    {
        _apiId = ConfigurationManager.AppSettings["app_id"];
        _apiHash = ConfigurationManager.AppSettings["api_hash"];
        _phoneNumber = ConfigurationManager.AppSettings["phone_number"];
        _apiLiquidity = ConfigurationManager.AppSettings["api_liquidity"];
    }



    static async Task Main(string[] args)
    {
        while (true)
        {
            await _program.Run();
            await Task.Delay(TimeSpan.FromMinutes(5));
        }
    }

    private async Task Run()
    {
        _mintAddresses = await _scanSolana.GetPreLaunchCoins();

        await ReapSol();
    }

    private static async Task ReapSol()
    {
        await CheckIfPreviousAddressValid();
        foreach (var address in _mintAddresses)
        {
            if (!String.IsNullOrEmpty(address) && 
                !_invalidMintAddresses.ContainsKey(address) && 
                !_validMintAddresses.ContainsKey(address))
            {
                await ProcessMintAddressAsync(address);
            }
            else if(String.IsNullOrEmpty(address))
            {
                throw new Exception("Address values cannot be null");
            }
            else if(_validMintAddresses.ContainsKey(address))
            {
                //TODO: do the actual limit order here
            }
        }
    }

    private async Task<string> GetLiquidityPriceAsync(string mintAddress)
    {
        string responseMsg = String.Empty;
        try
        {
            string fullUrl = $"{_apiLiquidity.TrimEnd('/')}/v1/tokens/{mintAddress}/report";
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpResponseMessage response = await client.GetAsync(fullUrl);
            response.EnsureSuccessStatusCode();
            responseMsg = await response.Content.ReadAsStringAsync();

        }
        catch (Exception)
        {
        //error: 400
        }
        return responseMsg;
    }

    private static async Task<bool> IsRugSafeAddressAsync(string mintAddress)
    {
        string rugBodyResult = await _program.GetLiquidityPriceAsync(mintAddress);
        if(!String.IsNullOrEmpty(rugBodyResult))
        {
            using JsonDocument document = JsonDocument.Parse(rugBodyResult);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("markets", out JsonElement markets) && markets.ValueKind == JsonValueKind.Array && markets.GetArrayLength() > 0)
            {
                JsonElement firstMarket = markets[0];
                JsonElement lp = firstMarket.GetProperty("lp");
                double basePrice = lp.GetProperty("basePrice").GetDouble();
                double liquidityPrice = lp.GetProperty("baseUSD").GetDouble();
                int score = root.GetProperty("score").GetInt32();
                return basePrice == 0 && score < 20410 && liquidityPrice > 1000;
            }
        }
        return false;
    }

    private static async Task ProcessMintAddressAsync(string mintAddress)
    {
        // Double check if the mintAddress is not already processed as invalid or valid
        if (!_invalidMintAddresses.ContainsKey(mintAddress) && !_validMintAddresses.ContainsKey(mintAddress))
        {
            bool isSafe = await SetSafetyLevel(mintAddress); // Determine safety level of the mintAddress

            Console.WriteLine($"{mintAddress} is {(isSafe ? "Safe" : "Not Safe")}");

            if (isSafe)
            {
                //todo: place the actual limit order
            }
        }
    }

    private static async Task CheckIfPreviousAddressValid()
    {
        var tasks = new List<Task>();

        // Iterate through previously invalid addresses
        foreach (var key in _invalidMintAddresses.Keys)
        {
            if (!_invalidMintAddresses[key])
            {
                tasks.Add(SetSafetyLevel(key));
            }
            else
            {
                Console.WriteLine($"{key} is now true");
            }
        }

        await Task.WhenAll(tasks); // Wait for all tasks to complete
    }

    private static async Task<bool> SetSafetyLevel(string mintAddress)
    {
        bool isSafe = false;

        if (!string.IsNullOrEmpty(mintAddress))
        {
            isSafe = await IsRugSafeAddressAsync(mintAddress);

            if (!isSafe)
            {
                _invalidMintAddresses.TryAdd(mintAddress, isSafe);
            }
            else
            {
                _validMintAddresses.TryAdd(mintAddress, isSafe);
                Console.WriteLine($"{mintAddress} is now safe");
            }
        }

        return isSafe;
    }
}

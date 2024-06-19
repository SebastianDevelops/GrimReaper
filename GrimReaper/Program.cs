using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TL;
using WTelegram;

class Program
{
    private readonly string _apiId;
    private readonly string _apiHash;
    private readonly string _phoneNumber;
    private readonly string _apiLiquidity;
    private static readonly Program _program = new Program();
    private static readonly Dictionary<string, bool> _mintAddresses = new Dictionary<string, bool>();
    private Client _client;

    public Program()
    {
        _apiId = ConfigurationManager.AppSettings["app_id"];
        _apiHash = ConfigurationManager.AppSettings["api_hash"];
        _phoneNumber = ConfigurationManager.AppSettings["phone_number"];
        _apiLiquidity = ConfigurationManager.AppSettings["api_liquidity"];
        _client = new Client(Config);
    }

    
    static async Task Main(string[] args)
    {
        await _program.Run();
    }

    private async Task Run()
    {
        var cancellationTokenSource = new CancellationTokenSource();

        #region store custom session name
        /*
         // Generate a unique session file name based on the process ID or other unique identifier
         string sessionFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"WTelegram_{Guid.NewGuid()}.session");

          using var client = new Client(Config, sessionStore: new FileStream(sessionFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
         */
        #endregion

        //_client = new Client(Config);
        try
        {
            await _client.LoginUserIfNeeded();

            // Interact with the bot
            var botUsername = "solanascanner";
            var botPeer = await _client.Contacts_ResolveUsername(botUsername);

            // Fetch and print recent messages from the bot
            await FetchAndPrintMessagesAsync(_client, botPeer);

            // Handle bot responses
            await ListenToBotResponsesAsync(_client, botPeer, cancellationTokenSource.Token);
        }
        finally
        {
            _client.Dispose();
        }
    }

    private string Config(string what)
    {
        return what switch
        {
            "api_id" => _apiId,
            "api_hash" => _apiHash,
            "phone_number" => _phoneNumber,
            "verification_code" => Console.ReadLine(),
            _ => null,
        };
    }

    private static async Task FetchAndPrintMessagesAsync(Client client, InputPeer botPeer)
    {
        var history = await client.Messages_GetHistory(botPeer, limit: 40);
        foreach (var messageBase in history.Messages)
        {
            if (messageBase is Message message)
            {
                string mintAddress = GetAddressFromBot(message.message);
                if (!string.IsNullOrEmpty(mintAddress))
                {
                    await ProcessMintAddressAsync(mintAddress);
                }
            }
        }
    }

    private static async Task ListenToBotResponsesAsync(Client client, InputPeer botPeer, CancellationToken cancellationToken)
    {
        var updateManager = new UpdateManager(client, update => HandleUpdateAsync(update, botPeer));

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (TaskCanceledException) { }
    }

    private static async Task HandleUpdateAsync(Update update, InputPeer botPeer)
    {
        if (update is UpdateNewChannelMessage updateNewMessage && updateNewMessage.message.Peer.ID == botPeer.ID)
        {
            if (updateNewMessage.message is Message message)
            {
                string mintAddress = GetAddressFromBot(message.message);
                if (!string.IsNullOrEmpty(mintAddress))
                {
                    await ProcessMintAddressAsync(mintAddress);
                }
            }
        }
        else if (update is UpdateUserStatus getLatestMessages)
        {
            await TryFetchALatesttMessagesAsync(_program._client, botPeer);
        }
    }

    private static async Task TryFetchALatesttMessagesAsync(Client client, InputPeer botPeer)
    {
        var history = await client.Messages_GetHistory(botPeer, limit: 40);
        foreach (var messageBase in history.Messages)
        {
            if (messageBase is Message message)
            {
                string mintAddress = GetAddressFromBot(message.message);
                if (!string.IsNullOrEmpty(mintAddress) && !_mintAddresses.ContainsKey(mintAddress))
                {
                    await ProcessMintAddressAsync(mintAddress);
                }
            }
        }
    }



    private static string GetAddressFromBot(string botMessage)
    {
        string address = string.Empty;
        if (isValidSnipe(botMessage))
        {
            address = ParseCoinAddress(botMessage);
        }
        return address;
    }

    private static bool isValidSnipe(string preCoin)
    {
        return Regex.Matches(preCoin.ToLower(), "revoked").Count >= 3;
    }

    private static string ParseCoinAddress(string preCoin)
    {
        string pattern = @"🏠 Address:\s*(?<address>.+)\s*";
        Match match = Regex.Match(preCoin, pattern);
        return match.Success ? match.Groups["address"].Value.Trim() : string.Empty;
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
        if (!_mintAddresses.ContainsKey(mintAddress))
        {
            bool isSafe = await IsRugSafeAddressAsync(mintAddress);
            _mintAddresses[mintAddress] = isSafe;
            Console.WriteLine($"{mintAddress} is {(isSafe ? "Safe" : "Not Safe")}");
            if (isSafe)
            {
                // Stop further processing if address is safe
                Environment.Exit(0);
            }
        }
    }
}

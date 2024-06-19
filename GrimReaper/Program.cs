using System.Collections.Concurrent;
using System.Configuration;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using TL;
using WTelegram;

class Program
{
    private readonly string _apiId;
    private readonly string _apiHash;
    private readonly string _phoneNumber;
    private readonly string _apiLiquidity;
    private static readonly Program _program = new Program();
    private static readonly ConcurrentDictionary<string, bool> _invalidMintAddresses = new ConcurrentDictionary<string, bool>();
    private static readonly ConcurrentDictionary<string, bool> _validMintAddresses = new ConcurrentDictionary<string, bool>();

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
        while (true)
        {
            await _program.Run();
            await Task.Delay(TimeSpan.FromMinutes(5));
        }
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
        await CheckIfPreviousAddressValid(); // Check and update previously invalid addresses
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
        var history = await client.Messages_GetHistory(botPeer, limit: 25);
        foreach (var messageBase in history.Messages)
        {
            if (messageBase is Message message)
            {
                string mintAddress = GetAddressFromBot(message.message);
                if (!string.IsNullOrEmpty(mintAddress) && !_invalidMintAddresses.ContainsKey(mintAddress))
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
        // Check if the mintAddress is not already processed as invalid or valid
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

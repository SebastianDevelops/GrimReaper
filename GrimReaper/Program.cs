using System;
using System.Collections.Generic;
using System.Configuration;
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

    public Program()
    {
        _apiId = ConfigurationManager.AppSettings["app_id"];
        _apiHash = ConfigurationManager.AppSettings["api_hash"];
        _phoneNumber = ConfigurationManager.AppSettings["phone_number"];
        _apiLiquidity = ConfigurationManager.AppSettings["api_liquidity"];
    }

    [Obsolete]
    static async Task Main(string[] args)
    {
        var timer = new System.Threading.Timer(async _ => await _program.Run(), null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        await Task.Delay(Timeout.Infinite);
    }

    [Obsolete]
    private async Task Run()
    {
        var cancellationTokenSource = new CancellationTokenSource();

        // Generate a unique session file name based on the process ID or other unique identifier
        string sessionFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"WTelegram_{Guid.NewGuid()}.session");

        using var client = new Client(Config, sessionStore: new FileStream(sessionFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
        try
        {
            await client.LoginUserIfNeeded();

            // Interact with the bot
            var botUsername = "solanascanner";
            var botPeer = await client.Contacts_ResolveUsername(botUsername);

            // Fetch and print recent messages from the bot
            await FetchAndPrintMessagesAsync(client, botPeer);

            // Handle bot responses
            await ListenToBotResponsesAsync(client, botPeer, cancellationTokenSource.Token);
        }
        finally
        {
            client.Dispose();
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

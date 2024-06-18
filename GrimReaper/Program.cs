using System;
using System.Collections.Generic;
using System.Configuration;
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
    private static readonly Dictionary<string, bool> _addressDictionary = new Dictionary<string, bool>();
    private static readonly Program _program = new Program();
    private static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

    public Program()
    {
        _apiId = ConfigurationManager.AppSettings["api_id"];
        _apiHash = ConfigurationManager.AppSettings["api_hash"];
        _phoneNumber = ConfigurationManager.AppSettings["phone_number"];
        _apiLiquidity = ConfigurationManager.AppSettings["api_liquidity"];
    }

    static async Task Main(string[] args)
    {
        await RunPeriodicallyAsync(TimeSpan.FromMinutes(5), _cancellationTokenSource.Token);
    }

    private static async Task RunPeriodicallyAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _program.Run();
            await Task.Delay(interval, cancellationToken);
        }
    }

    [Obsolete]
    private async Task Run()
    {
        using var client = new Client(Config);
        try
        {
            await client.LoginUserIfNeeded();

            // Interact with the bot
            var botUsername = "solanascanner";
            var botPeer = await client.Contacts_ResolveUsername(botUsername);

            // Fetch and print recent messages from the bot
            await FetchAndPrintMessagesAsync(client, botPeer);

            // Handle bot responses
            await ListenToBotResponsesAsync(client, botPeer, _cancellationTokenSource.Token);
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
        var history = await client.Messages_GetHistory(botPeer, limit: 10);
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

    [Obsolete]
    private static async Task ListenToBotResponsesAsync(Client client, InputPeer botPeer, CancellationToken cancellationToken)
    {
        client.OnUpdate += async (updatesBase) =>
        {
            if (updatesBase is Updates updates)
            {
                foreach (var update in updates.UpdateList)
                {
                    if (update is UpdateNewMessage updateNewMessage)
                    {
                        if (updateNewMessage.message.Peer.ID == botPeer.ID)
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
                }
            }
            await Task.CompletedTask;
        };

        // Keep the application running to listen to updates
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            // Task was canceled, handle cleanup if necessary
        }
    }

    private static string GetAddressFromBot(string botMessage)
    {
        return isValidSnipe(botMessage) ? ParseCoinAddress(botMessage) : string.Empty;
    }

    private static bool isValidSnipe(string preCoin)
    {
        return Regex.Matches(preCoin.ToLower(), "revoked").Count >= 3;
    }

    private static string ParseCoinAddress(string preCoin)
    {
        string pattern = @"🏠 Address:\s*(?<address>.+)\s*";
        var match = Regex.Match(preCoin, pattern);

        return match.Success ? match.Groups["address"].Value.Trim() : string.Empty;
    }

    private async Task<string> GetLiquidityPriceAsync(string mintAddress)
    {
        string fullUrl = $"{_apiLiquidity}/v1/tokens/{mintAddress}/report";

        using var client = new HttpClient();

        try
        {
            HttpResponseMessage response = await client.GetAsync(fullUrl);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"\nException Caught! Message: {e.Message} Stack Trace: {e.StackTrace}");
            return string.Empty;
        }
    }

    private static async Task<bool> IsRugSafeAddressAsync(string mintAddress)
    {
        bool isSafe = false;
        string rugBodyResult = await _program.GetLiquidityPriceAsync(mintAddress);

        using (JsonDocument document = JsonDocument.Parse(rugBodyResult))
        {
            JsonElement root = document.RootElement;

            if (root.TryGetProperty("markets", out JsonElement markets) && markets.ValueKind == JsonValueKind.Array && markets.GetArrayLength() > 0)
            {
                JsonElement firstMarket = markets[0];
                JsonElement lp = firstMarket.GetProperty("lp");
                double basePrice = lp.GetProperty("basePrice").GetDouble();
                double liquidityPrice = lp.GetProperty("baseUSD").GetDouble();
                int score = root.GetProperty("score").GetInt32();

                isSafe = basePrice == 0 && score < 20410 && liquidityPrice > 1000;
            }
        }

        return isSafe;
    }

    private static async Task ProcessMintAddressAsync(string mintAddress)
    {
        if (!_addressDictionary.ContainsKey(mintAddress))
        {
            _addressDictionary[mintAddress] = false;
            bool isSafe = await IsRugSafeAddressAsync(mintAddress);
            if (isSafe)
            {
                _addressDictionary[mintAddress] = true;
                Console.WriteLine($"Safe address found: {mintAddress}");
                _cancellationTokenSource.Cancel(); // Stop the periodic task
            }
            else
            {
                Console.WriteLine($"Address processed: {mintAddress}");
            }
        }
    }
}
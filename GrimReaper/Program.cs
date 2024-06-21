using FreshTokenScanner;
using GrimReaper.SolValidation;
using System.Collections.Concurrent;
using TelegramBot;

class Program
{
    private static readonly Program _program = new Program();
    private static readonly ScanSolanaNet _scanSolana = new ScanSolanaNet();
    private static readonly MrMeeSeeks _mrMeeSeeks = new MrMeeSeeks();
    private static readonly CoinValidation _coinCheck = new CoinValidation();

    private static readonly ConcurrentDictionary<string, bool> _invalidMintAddresses = new ConcurrentDictionary<string, bool>();
    private static readonly ConcurrentDictionary<string, bool> _validMintAddresses = new ConcurrentDictionary<string, bool>();
    private static List<string> _mintAddresses = new List<string>();


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

        foreach (var key in _invalidMintAddresses.Keys)
        {
            _mintAddresses.Add(key);
        }

        await ReapSol();
    }

    private static async Task ReapSol()
    {
        if(_mintAddresses.Count > 0) 
        {
            foreach (var address in _mintAddresses)
            {
                if (!_validMintAddresses.ContainsKey(address))
                {
                    await ProcessMintAddressAsync(address);
                }
                else if (String.IsNullOrEmpty(address))
                {
                    Console.WriteLine("Address values cannot be null");
                }
                else if (_validMintAddresses.ContainsKey(address))
                {
                    //TODO: do the actual limit order here
                }
            }
        }
    }

    private static async Task ProcessMintAddressAsync(string mintAddress)
    {
        // Double check if the mintAddress is not valid already
        if (!_validMintAddresses.ContainsKey(mintAddress))
        {
            bool isSafe = await SetSafetyLevel(mintAddress); // Determine safety level of the mintAddress

            Console.WriteLine($"{mintAddress} is {(isSafe ? "Safe" : "Not Safe")}");

            if (isSafe)
            {
                //todo: place the actual limit order
            }
        }
    }

    private static async Task<bool> SetSafetyLevel(string mintAddress)
    {
        bool isSafe = false;
        try
        {
            if (!string.IsNullOrEmpty(mintAddress))
            {
                isSafe = await _coinCheck.IsCoinValidAsync(mintAddress);

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
        }
        catch
        {
            _invalidMintAddresses[mintAddress] = isSafe;
        }
        
        return isSafe;
    }
}

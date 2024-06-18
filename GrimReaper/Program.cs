using System;
using System.Configuration;
using System.Threading.Tasks;
using TL;
using WTelegram;

class Program
{
    private readonly string _apiId;
    private readonly string _apiHash;
    private readonly string _phoneNumber;

    public Program()
    {
        _apiId = ConfigurationManager.AppSettings["api_id"];
        _apiHash = ConfigurationManager.AppSettings["api_hash"];
        _phoneNumber = ConfigurationManager.AppSettings["phone_number"];
    }

    static async Task Main(string[] args)
    {
        var program = new Program();
        await program.Run();
    }

    private async Task Run()
    {
        using var client = new Client(Config);
        try
        {
            await client.LoginUserIfNeeded();

            Console.WriteLine("Logged in!");

            // Interact with the bot
            var botUsername = "solanascanner";
            var botPeer = await client.Contacts_ResolveUsername(botUsername);

            // Fetch and print recent messages from the bot
            await FetchAndPrintMessages(client, botPeer);

            // Handle bot responses
            await ListenToBotResponses(client, botPeer);
        }
        finally
        {
            client.Dispose();
        }
    }

    private string Config(string what)
    {
        switch (what)
        {
            case "api_id": return _apiId;
            case "api_hash": return _apiHash;
            case "phone_number": return _phoneNumber;
            case "verification_code": Console.Write("Code: "); return Console.ReadLine();
            default: return null;
        }
    }

    private static async Task FetchAndPrintMessages(Client client, InputPeer botPeer)
    {
        var history = await client.Messages_GetHistory(botPeer, limit: 10);
        foreach (var messageBase in history.Messages)
        {
            if (messageBase is Message message)
            {
                Console.WriteLine($"Bot: {message.message}");
            }
        }
    }

    [Obsolete]
    private static async Task ListenToBotResponses(Client client, InputPeer botPeer)
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
                                Console.WriteLine($"Bot: {message.message}");
                            }
                        }
                    }
                }
            }
            await Task.CompletedTask;
        };

        // Keep the application running to listen to updates
        await Task.Delay(-1);
    }
}
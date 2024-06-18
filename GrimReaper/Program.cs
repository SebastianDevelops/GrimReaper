using System;
using System.Threading.Tasks;
using TL;
using WTelegram;

class Program
{
    static async Task Main(string[] args)
    {
        var client = new Client(Config);
        try
        {
            await client.ConnectAsync();
            var user = await client.LoginUserIfNeeded();
            Console.WriteLine($"Hello, {user.first_name}!");

            // Interact with the bot
            var botUsername = "solanascanner"; // Replace with your bot's username
            var botPeer = await client.Contacts_ResolveUsername(botUsername);

            // Send a message to the bot
            await client.SendMessageAsync(botPeer, "/start");

            // Fetch and print recent messages from the bot
            await FetchAndPrintMessages(client, botPeer);

            // Handle bot response
            await ListenToBotResponses(client, botPeer);
        }
        finally
        {
            client.Dispose();
        }
    }

    static string Config(string what)
    {
        switch (what)
        {
            case "api_id": return "20667176";
            case "api_hash": return "c70aacde2f8b9fd677ebf540de92cc30";
            case "phone_number": return "+27609276793";
            case "verification_code": Console.Write("Code: "); return Console.ReadLine();
            default: return null;
        }
    }

    static async Task FetchAndPrintMessages(Client client, InputPeer botPeer)
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

    static async Task ListenToBotResponses(Client client, InputPeer botPeer)
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

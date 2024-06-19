using System;
using System.Configuration;
using Telegram.Bot;


namespace TelegramBot
{
    public class MrMeeSeeks
    {
        private readonly string? _botToken;

        public MrMeeSeeks()
        {
            _botToken = ConfigurationManager.AppSettings["bot_token"];
        }

        public async Task Speak()
        {
            if(_botToken != null)
            {
                var botClient = new TelegramBotClient(_botToken);

                var me = await botClient.GetMeAsync();
                Console.WriteLine($"Hello, World! I am user {me.Id} and my name is {me.FirstName}.");
            }
        }
    }
}

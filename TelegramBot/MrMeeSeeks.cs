using System.Configuration;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Polling;


namespace TelegramBot
{
    public class MrMeeSeeks
    {
        private static readonly string? _botToken = ConfigurationManager.AppSettings["bot_token"];

        TelegramBotClient botClient = new TelegramBotClient(_botToken);

        public async Task SaySafeAddress(string mintAddress)
        {
            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            ReceiverOptions receiverOptions = new()
            {
                AllowedUpdates = Array.Empty<UpdateType>() 
            };

            await botClient.SendTextMessageAsync(
                chatId: -4265759290,
                disableNotification: true,
                text: $"{mintAddress}");
        }
    }
}

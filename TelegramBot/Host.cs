using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace TelegramBot
{
    public class Host
    {
        public Action<ITelegramBotClient, Update>? OnMessage; // Event
        private readonly TelegramBotClient _bot; // Bot instance
        private readonly ConsoleLogger _consoleLogger;

        // Ctor
        public Host(string token, ConsoleLogger consoleLogger)
        {
            _bot = new TelegramBotClient(token);
            _consoleLogger = consoleLogger;
        }

        public void Start()
        {
            _bot.StartReceiving(UpdateHandler, ErrorHandler);
            Console.WriteLine("Start receiving");
        }

        // Handlers update methods
        private async Task UpdateHandler(ITelegramBotClient client, Update update, CancellationToken token)
        {
            // Logging
            string logText = $"User message: {update.Message?.Text}" +
                $"\t Username: {update.Message.Chat.Username}" +
                $"\t UserID: {update.Message.Chat.Id}";
            _consoleLogger.Log(logText);

            // Event calling
            OnMessage?.Invoke(client, update);


            await Task.CompletedTask;
        }
        private async Task ErrorHandler(ITelegramBotClient client, Exception exception, HandleErrorSource source, CancellationToken token)
        {
            _consoleLogger.Log(exception.Message, LogStatus.Error);
            await Task.CompletedTask;
        }

        // Public methods
        public async Task SendMessage(ChatId chatId, string message)
        {
            _bot?.SendMessage(chatId, message);
            await Task.CompletedTask;
        }

    }
}

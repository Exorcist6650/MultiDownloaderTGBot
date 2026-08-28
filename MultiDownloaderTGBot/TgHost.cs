using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Utils;
using Services;

namespace TelegramBot
{
    public class TgHost(string token, ILogger logger)
    {
        // Events
        public Action<ITelegramBotClient, Update>? OnMessage;
        public Action<ITelegramBotClient, CallbackQuery>? OnCallback;

        // Fields
        public User Me { get; private set; } // Bot info
        private readonly TelegramBotClient _bot = new (token); // Bot instance
        private readonly ILogger _logger = logger;

        public async Task Init()
        {
            Me = await _bot.GetMe(); // Get bot info
            _bot.StartReceiving(UpdateHandler, ErrorHandler);

            Console.WriteLine("Start receiving"); // Log
        }

        // Update handler
        private async Task UpdateHandler(ITelegramBotClient client, Update update, CancellationToken token)
        {
            var message = update.Message;

            // Buttons callback
            if (update?.Type == UpdateType.CallbackQuery)
            {
                if (update.CallbackQuery is { } callback)
                {
                    OnCallback?.Invoke(client, callback); // Event calling

                    _logger.Log(
                        $"Button: {callback.Data} | ID: {callback.Message?.Chat.Id} | User: {callback.From.Username}"); // Log
                }
            }
            // Standart message
            else
            {
                // Log only for text message
                if (message?.Text is not null)
                    _logger.Log(
                        $"Message: {message?.Text} | ID: {message?.Chat.Id} | User: {message?.Chat.Username}"); // Log


                OnMessage?.Invoke(client, update); // Event calling
            }
            await Task.CompletedTask;
        }
        private async Task ErrorHandler(ITelegramBotClient client, Exception exception, HandleErrorSource source, CancellationToken token)
        {
            _logger.Log(exception.Message, ELogStatus.Error);
            await Task.CompletedTask;
        }
    }
}
using System;
using AngleSharp.Dom;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TelegramBot
{
    public class Host
    {
        // Events
        public Action<ITelegramBotClient, Update>? OnMessage;
        public Action<ITelegramBotClient, CallbackQuery>? OnCallback;

        // Fields
        private readonly TelegramBotClient _bot; // Bot instance
        private readonly ConsoleLogger _consoleLogger;

        public User Me { get; private set; } // Bot info

        public Host(string token, ConsoleLogger consoleLogger)
        {
            _bot = new TelegramBotClient(token);
            _consoleLogger = consoleLogger;
        }

        public async void Start()
        {
            _bot.StartReceiving(UpdateHandler, ErrorHandler);
            Me = await _bot.GetMe();
            _consoleLogger.Log("Start receiving");
        }

        // Handlers update methods
        private async Task UpdateHandler(ITelegramBotClient client, Update update, CancellationToken token)
        {
            var message = update.Message;
            var chatId = update.Message?.Chat.Id ?? 0;

            // Buttons callback
            if (update?.Type == Telegram.Bot.Types.Enums.UpdateType.CallbackQuery)
            {
                var callback = update?.CallbackQuery;
                if (callback != null)
                {
                    OnCallback?.Invoke(client, callback);
                }
                await Task.CompletedTask;
            }
            // Standart message
            else
            {
                // Logging
                string logText = $"User message: {message?.Text}" +
                    $"\t Username: {message?.Chat.Username}" +
                    $"\t UserID: {message?.Chat.Id}";

                _consoleLogger.Log(logText);

                // Event calling
                if (update != null)
                    OnMessage?.Invoke(client, update);

                // Delete user message
                await client.DeleteMessage(chatId, message?.Id ?? 0);
            }

        }
        private async Task ErrorHandler(ITelegramBotClient client, Exception exception, HandleErrorSource source, CancellationToken token)
        {
            _consoleLogger.Log(exception.Message, LogStatus.Error);
            await Task.CompletedTask;
        }

        public async Task<User> GetMe() => await _bot.GetMe();

    }
}
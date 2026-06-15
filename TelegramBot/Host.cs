using System;
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

        public User Me { get; private set; } // Bot info

        public Host(string token, ConsoleLogger consoleLogger)
        {
            _bot = new TelegramBotClient(token);
        }

        public async void Start()
        {
            _bot.StartReceiving(UpdateHandler, ErrorHandler);
            Me = await _bot.GetMe();

            ConsoleLogger.Log("Start receiving");
        }

        // Update handler
        private async Task UpdateHandler(ITelegramBotClient client, Update update, CancellationToken token)
        {
            var message = update.Message;
            var chatId = update.Message?.Chat.Id;

            // Buttons callback
            if (update?.Type == Telegram.Bot.Types.Enums.UpdateType.CallbackQuery)
            {
                var callback = update?.CallbackQuery;
                if (callback != null)
                {
                    // Logging
                    ConsoleLogger.Log($"User click button: {callback.Data}");

                    // Event calling
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

                ConsoleLogger.Log(logText);

                // Event calling
                if (update != null)
                    OnMessage?.Invoke(client, update);

                // Delete user message
                try
                {
                    await client.DeleteMessage(chatId, message?.Id ?? 0);
                }
                catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 403)
                {
                    ConsoleLogger.Log($"Exception: {ex.Message}", LogStatus.Error);
                }
                catch (Exception ex)
                {
                    ConsoleLogger.Log($"Exception: {ex.Message}", LogStatus.Error);
                }
            }

        }
        private async Task ErrorHandler(ITelegramBotClient client, Exception exception, HandleErrorSource source, CancellationToken token)
        {
            ConsoleLogger.Log(exception.Message, LogStatus.Error);
            await Task.CompletedTask;
        }
    }
}
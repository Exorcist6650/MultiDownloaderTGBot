using System.Collections.Concurrent;
using Managers;
using Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Utils;

namespace TelegramBot
{
    public class Bot
    {
        private readonly TgHost _host;
        private readonly TelegramDownloadService _telegramDownloadService;
        private readonly ILogger _logger;

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _downloadBlockers = new();

        public Bot(
            TgHost host,
            TelegramDownloadService telegramDownloadService,
            ILogger logger)
        {
            // Dependencies
            _host = host;
            _telegramDownloadService = telegramDownloadService;
            _logger = logger;

            // Events
            _host.OnMessage += OnMessage;
            _host.OnCallback += OnCallback;
        }

        public async Task Init()
        {
            await _host.Init(); // Start the bot
            await _telegramDownloadService.Init(_host.Me.Username!); // Init download service
            Console.WriteLine("Bot is started");
        }

        // DELEGATES


        private async void OnMessage(ITelegramBotClient client, Update update)
        {
            // Drop message while services is not init
            if (!_telegramDownloadService.IsInit) return;

            // Variables
            if (update?.Message is not { } message) return;
            if (update?.Message.Chat.Id is not { } chatId) return;

            // User language
            var lang = message.From?.LanguageCode?.ToLowerInvariant() switch
            {
                "en" or "en-us" or "en-gb" => ELanguage.En,
                "ru" or "ru-ru" => ELanguage.Ru,
                _ => ELanguage.En
            };

            // Delete user message
            await MessageService.Remove(client, message.Chat.Id, message, _logger);

            // Default commands
            if (await DispachCommands(client, chatId, message, lang)) return;

            // Handle logic
            await HandleFlowAsync(client, chatId, message, lang);
        }

        private async void OnCallback(ITelegramBotClient client, CallbackQuery callback)
        {
            // Drop message while services is not init
            if (!_telegramDownloadService.IsInit) return;

            // Variables
            if (callback?.Message is not { } message) return;
            if (callback?.From.Id is not { } chatId) return;
            if (message.Text is not { } caption) return;

            // User language
            var lang = callback.From.LanguageCode?.ToLowerInvariant() switch
            {
                "en" or "en-us" or "en-gb" => ELanguage.En,
                "ru" or "ru-ru" => ELanguage.Ru,
                _ => ELanguage.En
            };

            // URL parse key
            string keyToParse = "\nLINK: ";
            var prefixParse = caption.IndexOf(keyToParse);

            if (prefixParse >= 0)
            {
                // URL parse from caption
                var videoUrl = caption[(prefixParse + keyToParse.Length)..];

                var lockKey = $"{chatId}:{videoUrl}"; // Key to lock download

                // Lock object
                var semaphore = _downloadBlockers.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
                var semaphoreAcquired = false;

                try
                {
                    switch (callback?.Data)
                    {
                        // Download video
                        case "action:video":

                            // Try to lock
                            if (!semaphore.Wait(0))
                            {
                                // Case download is locked
                                await LockAnswerAsync(client, callback.Id, lang);
                                return;
                            }
                            semaphoreAcquired = true; // Lock 

                            // Answer for UI
                            await client.AnswerCallbackQuery(callback.Id, "✨");

                            // Run process
                            await _telegramDownloadService.DownloadSendVideoProcess(
                                client,
                                chatId,
                                videoUrl,
                                lang);
                            break;

                        // Download audio
                        case "action:audio":

                            // Try to lock
                            if (!semaphore.Wait(0))
                            {
                                // Case download is locked
                                await LockAnswerAsync(client, callback.Id, lang);
                                return;
                            }
                            semaphoreAcquired = true; // Lock 

                            // Answer for UI
                            await client.AnswerCallbackQuery(callback.Id, "✨");

                            // Run process
                            await _telegramDownloadService.DownloadSendAudioProcess(
                                client,
                                chatId,
                                videoUrl,
                                lang);
                            break;


                        // Deleting info message
                        case "action:cancel":
                            await MessageService.Remove(client, chatId, callback.Message, _logger);
                            break;
                    }

                }
                catch (Exception ex)
                {
                    _logger.Log(ex.ToString(), ELogStatus.Error);
                }
                finally
                {
                    // Unlock downloading
                    if (semaphoreAcquired)
                    {
                        semaphore.Release(); 

                        // Clear dictionary (GC will clear an object automaticly)
                        _downloadBlockers.TryRemove(new KeyValuePair<string, SemaphoreSlim>(
                            lockKey, semaphore));
                    }    

                }
            }
        }

        // METHODS

        private async Task HandleFlowAsync(
            ITelegramBotClient client,
            ChatId chatId,
            Message message,
            ELanguage language)
        {
            // Checking message a link
            if (string.IsNullOrEmpty(message?.Text) ||
                !message.Text.Contains("http", StringComparison.OrdinalIgnoreCase) ||
                !message.Text.Contains("https", StringComparison.OrdinalIgnoreCase))
            {
                // Bot answer to null link
                await MessageService.Send(client, chatId, new Message
                { Text = ReplyReadService.GetReply("NotALink", language) }, _logger);

                return;
            }

            // Sending searching message
            var searchMessage = await MessageService.Send(client, chatId, new Message
            { Text = ReplyReadService.GetReply("Search", language) }, _logger);

            if (searchMessage is not null)
            {
                // Sending load menu
                await _telegramDownloadService.SendLoadingMenuAsync(
                    client, chatId, message.Text, language);

                // Delete searching message
                await MessageService.Remove(client, chatId, searchMessage, _logger);
            }
        }

        private async Task<bool> DispachCommands(
            ITelegramBotClient client,
            ChatId chatId,
            Message message,
            ELanguage language)
        {
            if (message.Text is not string text) return false;

            if (text == "/start")
                await MessageService.Send(client, chatId, new Message
                { Text = ReplyReadService.GetReply("Greeting", language) }, _logger);
            else
                return false; // If is not a command

            return true; // If text is a command
        }

        private async Task LockAnswerAsync(ITelegramBotClient client, string queryId, ELanguage language)
        {
            try
            {
                // Answer for UI
                await client.AnswerCallbackQuery(
                    queryId,
                    ReplyReadService.GetReply("ButtonLock", language),
                    showAlert: true);

            }
            catch (Exception ex)
            {
                _logger.Log(ex.ToString(), ELogStatus.Warning);
            }
        }
    };
}